using System;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ApocaChaseCamera
{
    // Optional runtime integration. No Apocaplayer code or assembly is bundled.
    internal static class ApocaplayerBridge
    {
        private static Func<bool> on, peek, aimZoom, occlusionAvailable, inCar, gameReady, dead, caveInside;
        private static Func<string> drawnWeapon;
        private static Func<Camera> playerCamera;
        private static Action<Vector3> viewPos;
        private static Action<Quaternion> viewRot;
        private static Action<float> viewFov;
        private static Action<bool> hasView;
        private static Func<ConfigEntry<bool>> occlusionEnabled;
        private static Func<ConfigEntry<bool>> playerEnabled, vehicleOcclusion;
        private static Func<ConfigEntry<float>> carHeight;
        private static Action<Camera, Vector3, Quaternion, Transform> cutaway;
        private static Action<Camera> computeView;
        private static Func<Vector3> cachedPosition;
        private static Func<Quaternion> cachedRotation;
        private static Func<int> cachedFrame;
        private static Action<Vector3> cachePosition;
        private static Action<Quaternion> cacheRotation;
        private static Action<float> cacheFov, gameFov, orbitYaw, orbitPitch, nativeDistance;
        private static Action<Transform> cacheCar;
        private static Action<bool> cacheCutaway, orbiting;
        private static Action<int> cacheFrame;
        private static Func<bool> projectionSet, fovSet;
        private static Func<float> nativeGameFov;
        private static Action<bool> clearProjectionFlag, clearFovFlag;
        private static Harmony harmony;
        private static bool searched, compatible, failed, warned;
        private static bool cruising, hasCruisePose, hasNativePose, cacheIsCruise;
        private static int cruisePoseFrame = -1, nativePoseFrame = -1, wheelFrame = -1;
        private static Vector3 lastCruisePosition, lastNativePosition;
        private static Quaternion lastCruiseRotation, lastNativeRotation;
        private static Transform cruiseCar, nativeCar;
        private struct FadeBinding
        {
            internal Renderer[] Renderers;
            internal Transform Root;
            internal float At;
        }
        private static readonly Renderer[] noRenderers = new Renderer[0];
        private static readonly Dictionary<Collider, FadeBinding> fadeBindings = new Dictionary<Collider, FadeBinding>();

        private static Type FindType(string name)
        {
            foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {Type type=assembly.GetType(name,false);if(type!=null)return type;}
            return null;
        }
        internal static void Refresh()
        {
            InvalidateCruiseCache();
            ClearFrameState();
            if (!compatible && !failed) searched = false;
        }

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

        private static Func<T> Property<T>(Type owner, string name)
        { return (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), Getter(owner, name, typeof(T))); }

        private static MethodInfo Method(Type owner, string name, params Type[] arguments)
        {
            MethodInfo method = owner == null ? null : AccessTools.Method(owner, name, arguments);
            if (!MethodMatches(method, typeof(void), arguments))
                throw new InvalidOperationException("The installed version has a different " + name + " interface.");
            return method;
        }

        private static bool MethodMatches(MethodInfo method, Type result, params Type[] arguments)
        {
            if (method == null || !method.IsStatic || method.ContainsGenericParameters || method.ReturnType != result) return false;
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != arguments.Length) return false;
            for (int i = 0; i < arguments.Length; i++) if (parameters[i].ParameterType != arguments[i]) return false;
            return true;
        }

        // Unity's shipped corlib stubs Reflection.Emit.DynamicMethod with a
        // NotSupportedException. Generate a tiny Cecil assembly through the
        // BepInEx runtime instead; typed access still avoids per-frame boxing.
        private static Func<T> Read<T>(FieldInfo field)
        {
            using (DynamicMethodDefinition method = new DynamicMethodDefinition("ApocaChaseCamera.Read." + field.Name, typeof(T), Type.EmptyTypes))
            {
                ILProcessor code = method.Definition.Body.GetILProcessor();
                code.Emit(OpCodes.Ldsfld, method.Definition.Module.ImportReference(field)); code.Emit(OpCodes.Ret);
                return (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), DMDCecilGenerator.Generate(method, null));
            }
        }

        private static Action<T> Write<T>(FieldInfo field)
        {
            using (DynamicMethodDefinition method = new DynamicMethodDefinition("ApocaChaseCamera.Write." + field.Name, typeof(void), new Type[] { typeof(T) }))
            {
                ILProcessor code = method.Definition.Body.GetILProcessor();
                code.Emit(OpCodes.Ldarg_0); code.Emit(OpCodes.Stsfld, method.Definition.Module.ImportReference(field)); code.Emit(OpCodes.Ret);
                return (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), DMDCecilGenerator.Generate(method, null));
            }
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
                Type plugin = FindType("Apocaplayer.Plugin");
                on = Read<bool>(Field(third, "On", typeof(bool), false));
                peek = Read<bool>(Field(third, "Peek", typeof(bool), false));
                aimZoom = Read<bool>(Field(third, "AimZoom", typeof(bool), false));
                viewPos = Write<Vector3>(Field(third, "ViewPos", typeof(Vector3), true));
                viewRot = Write<Quaternion>(Field(third, "ViewRot", typeof(Quaternion), true));
                viewFov = Write<float>(Field(third, "ViewFov", typeof(float), true));
                hasView = Write<bool>(Field(third, "HasView", typeof(bool), true));
                drawnWeapon = Property<string>(game, "DrawnWeapon");
                playerCamera = Read<Camera>(Field(game, "Cam", typeof(Camera), false));
                inCar = Property<bool>(game, "InCar"); gameReady = Property<bool>(game, "Ready"); dead = Property<bool>(game, "Dead");
                playerEnabled = Read<ConfigEntry<bool>>(Field(plugin, "Enabled", typeof(ConfigEntry<bool>), false));
                carHeight = Read<ConfigEntry<float>>(Field(plugin, "ThirdCarHeight", typeof(ConfigEntry<float>), false));
                // LateCrosshair reads these private caches, not the public ViewPos/ViewRot.
                // Validate the complete interface before installing any hook.
                cachedPosition = Read<Vector3>(Field(third, "_vPos", typeof(Vector3), false));
                cachedRotation = Read<Quaternion>(Field(third, "_vRot", typeof(Quaternion), false));
                cachedFrame = Read<int>(Field(third, "_viewFrame", typeof(int), false));
                cachePosition = Write<Vector3>(Field(third, "_vPos", typeof(Vector3), true));
                cacheRotation = Write<Quaternion>(Field(third, "_vRot", typeof(Quaternion), true));
                cacheFov = Write<float>(Field(third, "_vFov", typeof(float), true));
                gameFov = Write<float>(Field(third, "_gameFov", typeof(float), true));
                nativeGameFov = Read<float>(Field(third, "_gameFov", typeof(float), false));
                projectionSet = Read<bool>(Field(third, "_projSet", typeof(bool), false));
                fovSet = Read<bool>(Field(third, "_fovSet", typeof(bool), false));
                clearProjectionFlag = Write<bool>(Field(third, "_projSet", typeof(bool), true));
                clearFovFlag = Write<bool>(Field(third, "_fovSet", typeof(bool), true));
                cacheCar = Write<Transform>(Field(third, "_vCar", typeof(Transform), true));
                cacheCutaway = Write<bool>(Field(third, "_vCutaway", typeof(bool), true));
                cacheFrame = Write<int>(Field(third, "_viewFrame", typeof(int), true));
                orbitYaw = Write<float>(Field(third, "_orbitYaw", typeof(float), true));
                orbitPitch = Write<float>(Field(third, "_orbitPitch", typeof(float), true));
                orbiting = Write<bool>(Field(third, "Orbiting", typeof(bool), true));
                nativeDistance = Write<float>(Field(third, "_dist", typeof(float), true));
                MethodInfo method = Method(third, "PreCull", typeof(Camera));
                MethodInfo compute = Method(third, "ComputeView", typeof(Camera));
                computeView = (Action<Camera>)Delegate.CreateDelegate(typeof(Action<Camera>), compute);
                DiscoverCutaway(plugin);
                harmony = new Harmony(Plugin.GUID + ".apocaplayer");
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(ApocaplayerBridge), "BeforePreCull"));
                harmony.Patch(compute, prefix: new HarmonyMethod(typeof(ApocaplayerBridge), "BeforeComputeView"),
                    postfix: new HarmonyMethod(typeof(ApocaplayerBridge), "AfterComputeView"));
                harmony.Patch(Method(third, "Zoom"), prefix: new HarmonyMethod(typeof(ApocaplayerBridge), "BeforeZoom"));
                harmony.Patch(Method(third, "Tick"), postfix: new HarmonyMethod(typeof(ApocaplayerBridge), "AfterTick"));
                compatible = true;
                Plugin.Log.LogInfo("Apocaplayer compatibility patch ready: shared cursor pose, cutaway guards, driving controls and compass. Weapon views stay with Apocaplayer.");
            }
            catch (Exception ex) { FailClosed(ex); }
        }

        private static void DiscoverCutaway(Type plugin)
        {
            // A changed optional effect must not disable an otherwise valid camera bridge.
            try
            {
                Type occlusion = FindType("Apocaplayer.OcclusionCutaway");
                Type cave = FindType("Apocaplayer.Cave");
                if (occlusion == null || cave == null) return;
                occlusionEnabled = Read<ConfigEntry<bool>>(Field(plugin, "OcclusionPrototype", typeof(ConfigEntry<bool>), false));
                vehicleOcclusion = Read<ConfigEntry<bool>>(Field(plugin, "OcclusionInVehicle", typeof(ConfigEntry<bool>), false));
                caveInside = Read<bool>(Field(cave, "Inside", typeof(bool), false));
                occlusionAvailable = Property<bool>(occlusion, "Available");
                cutaway = (Action<Camera, Vector3, Quaternion, Transform>)Delegate.CreateDelegate(
                    typeof(Action<Camera, Vector3, Quaternion, Transform>),
                    Method(occlusion, "Prepare", typeof(Camera), typeof(Vector3), typeof(Quaternion), typeof(Transform)));
            }
            catch (Exception ex)
            {
                occlusionEnabled = vehicleOcclusion = null; caveInside = occlusionAvailable = null; cutaway = null;
                Plugin.Log.LogWarning("Apocaplayer cutaway adapter unavailable; safe collision camera retained: " + ex.Message);
            }
        }

        // Central eligibility. Native/on-foot/weapon/peek/death/menu views keep
        // their original behavior even if they change after our Update.
        internal static bool Driving(Camera camera)
        {
            if (!compatible || !Plugin.Enabled.Value || !Apocasetter.GameMenu.InGame ||
                Apocasetter.GameMenu.Paused || Apocasetter.InputBlocker.Active || !Application.isFocused || Time.timeScale <= 0f)
                return false;
            try
            {
                return playerEnabled() != null && playerEnabled().Value && gameReady() && inCar() && !dead() &&
                    CameraBinding.Resolve() && !CameraBinding.SpecialView() && CruisingView &&
                    camera != null && camera == CameraBinding.FirstCamera && camera == playerCamera() && camera.isActiveAndEnabled &&
                    (CameraBinding.NativeCamera == null || !CameraBinding.NativeCamera.isActiveAndEnabled);
            }
            catch (Exception ex) { FailClosed(ex); return false; }
        }

        internal static bool CutawayAllowed
        {
            get
            {
                if (!compatible || cutaway == null || !Driving(CameraBinding.FirstCamera)) return false;
                try
                {
                    ConfigEntry<bool> main = occlusionEnabled(), vehicle = vehicleOcclusion();
                    return main != null && main.Value && vehicle != null && vehicle.Value && !caveInside() && occlusionAvailable();
                }
                catch (Exception ex) { FailClosed(ex); return false; }
            }
        }

        internal static bool CanFade(Collider collider)
        {
            if (collider == null || !CutawayAllowed) return false;
            FadeBinding binding;
            if (!fadeBindings.TryGetValue(collider, out binding) ||
                binding.Root != collider.transform.root || Time.unscaledTime - binding.At >= 10f)
            {
                binding = BindFade(collider);
                if (fadeBindings.Count >= 1024) fadeBindings.Clear();
                fadeBindings[collider] = binding;
            }
            // Cache structural discovery, but check current visibility: a mesh
            // hidden earlier in this frame cannot be faded by the effect.
            foreach (Renderer renderer in binding.Renderers)
                if (renderer != null && renderer.enabled && !renderer.forceRenderingOff &&
                    renderer.gameObject.activeInHierarchy) return true;
            return false;
        }

        private static FadeBinding BindFade(Collider collider)
        {
            FadeBinding binding = new FadeBinding { Root = collider.transform.root,
                At = Time.unscaledTime, Renderers = noRenderers };
            if (collider is TerrainCollider || collider.GetComponent<Terrain>() != null) return binding;
            // Roads may use ordinary mesh colliders. Keep named road geometry
            // solid, including its sides; upward ground hits are also solid.
            for (Transform p = collider.transform; p != null; p = p.parent)
                if (p.name.IndexOf("road", StringComparison.OrdinalIgnoreCase) >= 0) return binding;
            Renderer own = collider.GetComponent<Renderer>();
            Renderer[] all;
            if (own != null) all = new Renderer[] { own };
            else
            {
                LODGroup lod = collider.GetComponentInParent<LODGroup>();
                Transform owner = lod != null ? lod.transform : collider.attachedRigidbody != null ?
                    collider.attachedRigidbody.transform : collider.transform.parent;
                if (owner == null) owner = collider.transform;
                all = owner.GetComponentsInChildren<Renderer>(true);
            }
            List<Renderer> eligible = new List<Renderer>();
            Bounds bounds = collider.bounds; bounds.Expand(0.1f);
            foreach (Renderer renderer in all)
            {
                if (renderer == null || (all.Length > 24 && !renderer.bounds.Intersects(bounds))) continue;
                bool modOwned = false;
                for (Transform p = renderer.transform; p != null; p = p.parent)
                    if (p.name.StartsWith("Apocaplayer", StringComparison.Ordinal)) { modOwned = true; break; }
                if (!modOwned) eligible.Add(renderer);
            }
            binding.Renderers = eligible.ToArray();
            return binding;
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
                try { ConfigEntry<bool> enabled = playerEnabled(); return enabled != null && enabled.Value && on() && !peek(); }
                catch (Exception ex) { FailClosed(ex); return false; }
            }
        }

        private static bool BeforePreCull(Camera __0)
        { UpdateOwner(); return !Driving(__0) || !ChaseView.Apply(__0); }

        private static bool BeforeComputeView(Camera __0)
        {
            UpdateOwner();
            if (!Driving(__0)) { InvalidateCruiseCache(); return true; }
            Vector3 position; Quaternion rotation;
            if (!ChaseView.TryPose(__0, out position, out rotation)) { InvalidateCruiseCache(); return true; }
            try
            {
                cachePosition(position); cacheRotation(rotation); cacheFov(__0.fieldOfView); gameFov(__0.fieldOfView);
                cacheCar(CameraBinding.Car); cacheCutaway(ChaseView.PoseCutaway); cacheFrame(Time.frameCount);
                cacheIsCruise = true;
                RememberCruise(position, rotation);
                viewPos(position); viewRot(rotation); viewFov(__0.fieldOfView); hasView(true);
                return false;
            }
            catch (Exception ex) { FailClosed(ex); return true; }
        }

        private static void AfterComputeView(Camera __0)
        {
            if (!compatible || __0 != CameraBinding.FirstCamera) return;
            try
            {
                if (cachedFrame() != Time.frameCount || cacheIsCruise || !inCar() || !ThirdPersonView) return;
                lastNativePosition = cachedPosition(); lastNativeRotation = cachedRotation();
                nativeCar = CameraBinding.Car;
                nativePoseFrame = Time.frameCount; hasNativePose = true;
            }
            catch (Exception ex) { FailClosed(ex); }
        }

        private static bool BeforeZoom()
        {
            if (!compatible) return true;
            try
            {
                // Apocaplayer may Update before our runner on the first entry frame.
                if (!CameraBinding.Resolve() || !Driving(CameraBinding.FirstCamera)) return true;
                if (wheelFrame != Time.frameCount)
                { wheelFrame = Time.frameCount; ChaseView.ScrollZoom(Input.mouseScrollDelta.y); }
                return false; // Do not change/save Apocaplayer's hidden driving distance.
            }
            catch (Exception ex) { FailClosed(ex); return true; }
        }

        private static void AfterTick() { UpdateOwner(); }

        private static void InvalidateCruiseCache()
        {
            if (!compatible || !cacheIsCruise) return;
            try { cacheFrame(-1); cacheIsCruise = false; }
            catch (Exception ex) { FailClosed(ex); }
        }

        private static void UpdateOwner()
        {
            if (!compatible) return;
            try
            {
                if (gameReady() && inCar()) CameraBinding.Resolve();
                bool driving = Driving(CameraBinding.FirstCamera);
                if (driving)
                {
                    // Recover the native owner's projection if its end-of-frame
                    // callback was interrupted. Never reset another owner's matrix.
                    Camera camera = CameraBinding.FirstCamera;
                    if (fovSet()) { camera.fieldOfView = nativeGameFov(); clearFovFlag(false); }
                    if (projectionSet()) { camera.ResetProjectionMatrix(); clearProjectionFlag(false); }
                    if (!cruising && hasNativePose && nativeCar == CameraBinding.Car && nativePoseFrame >= Time.frameCount - 1)
                        ChaseView.AcceptHandoff(lastNativePosition, lastNativeRotation);
                    // Tick still handles camera switching, view-model visibility,
                    // culling and body state. Only its hidden orbit is suppressed.
                    orbitYaw(0f); orbitPitch(0f); orbiting(false);
                }
                else if (cruising)
                {
                    InvalidateCruiseCache();
                    if (compatible && hasCruisePose && cruiseCar == CameraBinding.Car && cruisePoseFrame >= Time.frameCount - 1 &&
                        inCar() && ThirdPersonView && !aimZoom() && !dead() && !CameraBinding.SpecialView())
                    {
                        Camera camera = CameraBinding.FirstCamera;
                        if (camera != null)
                        {
                            float yaw, pitch; Angles(lastCruiseRotation, out yaw, out pitch);
                            float eyeYaw, eyePitch; Angles(camera.transform.rotation, out eyeYaw, out eyePitch);
                            orbitYaw(CameraMath.Wrap(yaw - eyeYaw)); orbitPitch(CameraMath.Clamp(pitch - eyePitch, -70f, 70f));
                            ConfigEntry<float> height = carHeight();
                            Vector3 pivot = camera.transform.position + Vector3.up * (height == null ? 0f : height.Value);
                            nativeDistance(Vector3.Distance(pivot, lastCruisePosition));
                            cacheFrame(-1);
                        }
                    }
                }
                cruising = driving;
            }
            catch (Exception ex) { FailClosed(ex); }
        }

        private static void RememberCruise(Vector3 position, Quaternion rotation)
        {
            lastCruisePosition = position; lastCruiseRotation = rotation;
            cruiseCar = CameraBinding.Car;
            cruisePoseFrame = Time.frameCount; hasCruisePose = true;
        }

        internal static void Angles(Quaternion rotation, out float yaw, out float pitch)
        {
            Vector3 forward = rotation * new Vector3(0f, 0f, 1f);
            yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            pitch = Mathf.Atan2(-forward.y, Mathf.Sqrt(forward.x * forward.x + forward.z * forward.z)) * Mathf.Rad2Deg;
        }

        internal static bool CompassHeading(out float yaw)
        {
            yaw = 0f;
            if (!compatible || !ThirdPersonView) return false;
            try
            {
                Camera camera = CameraBinding.FirstCamera;
                if (camera == null || camera != playerCamera() || !camera.isActiveAndEnabled || !inCar() || dead()) return false;
                computeView(camera); // Uses the real mod's current-frame cache, not last frame's published view.
                if (!compatible || cachedFrame() != Time.frameCount) return false;
                float pitch; Angles(cachedRotation(), out yaw, out pitch);
                return true;
            }
            catch (Exception ex) { FailClosed(ex); return false; }
        }

        internal static bool Publish(Camera camera, Vector3 position, Quaternion rotation)
        {
            if (!Owns(camera)) return true;
            try
            {
                viewPos(position); viewRot(rotation); viewFov(camera.fieldOfView); hasView(true);
                RememberCruise(position, rotation);
                if (ChaseView.PoseCutaway && CutawayAllowed)
                    cutaway(camera, position, rotation, CameraBinding.Car);
                return true;
            }
            catch (Exception ex) { FailClosed(ex); return false; }
        }

        private static void ClearAccessors()
        {
            on = peek = aimZoom = occlusionAvailable = inCar = gameReady = dead = caveInside = null; drawnWeapon = null;
            playerCamera = null;
            viewPos = null; viewRot = null; viewFov = null; hasView = null; occlusionEnabled = null; cutaway = null;
            playerEnabled = vehicleOcclusion = null; carHeight = null; computeView = null;
            cachedPosition = null; cachedRotation = null; cachedFrame = null;
            cachePosition = null; cacheRotation = null; cacheFov = gameFov = orbitYaw = orbitPitch = nativeDistance = null;
            cacheCar = null; cacheCutaway = orbiting = null; cacheFrame = null;
            projectionSet = fovSet = null; nativeGameFov = null; clearProjectionFlag = clearFovFlag = null;
            ClearFrameState();
        }

        private static void ClearFrameState()
        {
            cruising = hasCruisePose = hasNativePose = cacheIsCruise = false;
            cruisePoseFrame = nativePoseFrame = wheelFrame = -1; cruiseCar = nativeCar = null; fadeBindings.Clear();
        }

        private static void FailClosed(Exception ex)
        {
            // A failed prefix must not leave the native once-per-frame cache
            // marked valid with half-published ChaseCam values.
            if (cacheFrame != null) { try { cacheFrame(-1); } catch (Exception) { } }
            compatible = false; failed = true;
            if (harmony != null) { try { harmony.UnpatchSelf(); } catch (Exception) { } harmony = null; }
            ClearAccessors();
            if (warned) return;
            warned = true;
            Plugin.Log.LogWarning("Apocaplayer bridge disabled: " + ex);
        }

        internal static void Shutdown()
        {
            if (harmony != null) harmony.UnpatchSelf();
            harmony = null; ClearAccessors(); searched = compatible = failed = warned = false;
        }
    }
}

