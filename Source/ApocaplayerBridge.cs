using System;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ApocaChaseCamera
{
    // Optional runtime integration. No Apocaplayer code or assembly is bundled.
    internal static class ApocaplayerBridge
    {
        private static Func<bool> on, peek, aimZoom, occlusionAvailable;
        private static Func<string> drawnWeapon;
        private static Action<Vector3> viewPos;
        private static Action<Quaternion> viewRot;
        private static Action<float> viewFov;
        private static Action<bool> hasView;
        private static Func<ConfigEntry<bool>> occlusionEnabled;
        private static Action<Camera, Vector3, Quaternion, Transform> cutaway;
        private static Harmony harmony;
        private static bool searched, compatible, failed, warned;

        private static Type FindType(string name)
        {
            foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {Type type=assembly.GetType(name,false);if(type!=null)return type;}
            return null;
        }
        internal static void Refresh(){if(!compatible && !failed)searched=false;}

        private static FieldInfo Field(Type owner, string name, Type type, bool writable)
        {
            FieldInfo field = owner == null ? null : AccessTools.Field(owner, name);
            if (field == null || !field.IsStatic || field.FieldType != type || field.IsLiteral ||
                (writable && field.IsInitOnly))
                throw new InvalidOperationException("The installed version has a different " + name + " interface.");
            return field;
        }

        private static MethodInfo Getter(Type owner, string name, Type type)
        {
            PropertyInfo property = owner == null ? null : AccessTools.Property(owner, name);
            MethodInfo getter = property == null ? null : property.GetGetMethod(true);
            if (property == null || property.PropertyType != type || property.GetIndexParameters().Length != 0 ||
                getter == null || !getter.IsStatic || getter.ContainsGenericParameters || getter.ReturnType != type)
                throw new InvalidOperationException("The installed version has a different " + name + " interface.");
            return getter;
        }

        private static bool MethodMatches(MethodInfo method, Type result, params Type[] arguments)
        {
            if (method == null || !method.IsStatic || method.ContainsGenericParameters || method.ReturnType != result) return false;
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != arguments.Length) return false;
            for (int i = 0; i < arguments.Length; i++) if (parameters[i].ParameterType != arguments[i]) return false;
            return true;
        }

        // Emitted typed accessors pay reflection's discovery cost once. Static
        // booleans and rendered pose structs then need no per-frame boxing.
        private static Func<T> Read<T>(FieldInfo field)
        {
            DynamicMethod method = new DynamicMethod("ApocaChaseCamera.Read." + field.Name, typeof(T), Type.EmptyTypes,
                field.DeclaringType.Module, true);
            ILGenerator code = method.GetILGenerator();
            code.Emit(OpCodes.Ldsfld, field); code.Emit(OpCodes.Ret);
            return (Func<T>)method.CreateDelegate(typeof(Func<T>));
        }

        private static Action<T> Write<T>(FieldInfo field)
        {
            DynamicMethod method = new DynamicMethod("ApocaChaseCamera.Write." + field.Name, typeof(void), new Type[] { typeof(T) },
                field.DeclaringType.Module, true);
            ILGenerator code = method.GetILGenerator();
            code.Emit(OpCodes.Ldarg_0); code.Emit(OpCodes.Stsfld, field); code.Emit(OpCodes.Ret);
            return (Action<T>)method.CreateDelegate(typeof(Action<T>));
        }

        internal static void Discover()
        {
            if (searched || failed) return;
            searched = true;
            // Harmony's missing-type lookup logs a warning. Optional absence is
            // normal: search quietly once per scene rather than every two seconds.
            try
            {
                Type third = FindType("Apocaplayer.ThirdPerson");
                if (third == null) return;
                Type game = FindType("Apocaplayer.Game");
                on = Read<bool>(Field(third, "On", typeof(bool), false));
                peek = Read<bool>(Field(third, "Peek", typeof(bool), false));
                aimZoom = Read<bool>(Field(third, "AimZoom", typeof(bool), false));
                viewPos = Write<Vector3>(Field(third, "ViewPos", typeof(Vector3), true));
                viewRot = Write<Quaternion>(Field(third, "ViewRot", typeof(Quaternion), true));
                viewFov = Write<float>(Field(third, "ViewFov", typeof(float), true));
                hasView = Write<bool>(Field(third, "HasView", typeof(bool), true));
                drawnWeapon = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), Getter(game, "DrawnWeapon", typeof(string)));
                MethodInfo method = AccessTools.Method(third, "PreCull", new Type[] { typeof(Camera) });
                if (!MethodMatches(method, typeof(void), typeof(Camera)))
                    throw new InvalidOperationException("The installed version has a different PreCull interface.");
                Type occlusion = FindType("Apocaplayer.OcclusionCutaway");
                Type plugin = FindType("Apocaplayer.Plugin");
                FieldInfo enabledField = plugin == null ? null : AccessTools.Field(plugin, "OcclusionPrototype");
                PropertyInfo availableProperty = occlusion == null ? null : AccessTools.Property(occlusion, "Available");
                MethodInfo prepare = occlusion == null ? null : AccessTools.Method(occlusion, "Prepare",
                    new Type[] { typeof(Camera), typeof(Vector3), typeof(Quaternion), typeof(Transform) });
                if (enabledField != null) occlusionEnabled = Read<ConfigEntry<bool>>(Field(plugin, "OcclusionPrototype", typeof(ConfigEntry<bool>), false));
                if (availableProperty != null) occlusionAvailable = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), Getter(occlusion, "Available", typeof(bool)));
                if (prepare != null)
                {
                    if (!MethodMatches(prepare, typeof(void), typeof(Camera), typeof(Vector3), typeof(Quaternion), typeof(Transform)))
                        throw new InvalidOperationException("The installed version has a different occlusion Prepare interface.");
                    cutaway = (Action<Camera, Vector3, Quaternion, Transform>)Delegate.CreateDelegate(
                        typeof(Action<Camera, Vector3, Quaternion, Transform>), prepare);
                }
                harmony = new Harmony(Plugin.GUID + ".apocaplayer");
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(ApocaplayerBridge), "BeforePreCull"));
                compatible = true;
                Plugin.Log.LogInfo("Apocaplayer driving-camera bridge ready. Weapon views stay with Apocaplayer.");
            }
            catch (Exception ex) { FailClosed(ex); }
        }

        internal static bool CruisingView
        {
            get
            {
                if (!compatible) return false;
                try { return on() && !peek() && !aimZoom() && String.IsNullOrEmpty(drawnWeapon()); }
                catch (Exception ex) { FailClosed(ex); return false; }
            }
        }
        internal static bool Owns(Camera camera)
        { return compatible && camera == CameraBinding.FirstCamera; }

        internal static bool ThirdPersonView
        {
            get
            {
                if (!compatible) return false;
                try { return on() && !peek(); }
                catch (Exception ex) { FailClosed(ex); return false; }
            }
        }

        private static bool BeforePreCull(Camera __0)
        { return !CruisingView || !ChaseView.Apply(__0); }

        internal static bool Publish(Camera camera, Vector3 position, Quaternion rotation)
        {
            if (!Owns(camera)) return true;
            try
            {
                viewPos(position); viewRot(rotation); viewFov(camera.fieldOfView); hasView(true);
                ConfigEntry<bool> enabled = occlusionEnabled == null ? null : occlusionEnabled();
                if (cutaway != null && enabled != null && enabled.Value && occlusionAvailable != null && occlusionAvailable())
                    cutaway(camera, position, rotation, CameraBinding.Car);
                return true;
            }
            catch (Exception ex) { FailClosed(ex); return false; }
        }

        private static void ClearAccessors()
        {
            on = peek = aimZoom = occlusionAvailable = null; drawnWeapon = null;
            viewPos = null; viewRot = null; viewFov = null; hasView = null; occlusionEnabled = null; cutaway = null;
        }

        private static void FailClosed(Exception ex)
        {
            compatible = false; failed = true;
            if (harmony != null) { try { harmony.UnpatchSelf(); } catch (Exception) { } harmony = null; }
            ClearAccessors();
            if (warned) return;
            warned = true;
            Plugin.Log.LogWarning("Apocaplayer bridge disabled: " + ex.Message);
        }

        internal static void Shutdown()
        {
            if (harmony != null) harmony.UnpatchSelf();
            harmony = null; ClearAccessors(); searched = compatible = failed = warned = false;
        }
    }
}
