using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ApocaChaseCamera
{
    // Observe the game's completed translation, without changing its origin,
    // transforms or physics. A pose may already have been consumed this frame
    // by the cursor/compass before the native LateUpdate moves the world.
    internal static class OriginShift
    {
        private static Harmony harmony;
        private struct Snapshot
        {
            internal Transform Car;
            internal Vector3 Position;
        }

        internal static void Initialize()
        {
            if (harmony != null) return;
            Harmony patch = new Harmony(Plugin.GUID + ".origin");
            try
            {
                foreach (Type type in new Type[] { typeof(FloatingOrigin), typeof(FloatingOriginAdvanced), typeof(FloatingOriginNew) })
                {
                    MethodInfo method = type.GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, Type.EmptyTypes, null);
                    if (method == null || method.ReturnType != typeof(void)) throw new MissingMethodException(type.FullName, "LateUpdate");
                    patch.Patch(method, new HarmonyMethod(typeof(OriginShift), "Before"), new HarmonyMethod(typeof(OriginShift), "After"));
                }
                harmony = patch;
                Plugin.Log.LogInfo("World-origin camera synchronization ready.");
            }
            catch (Exception ex)
            {
                patch.UnpatchSelf();
                Plugin.Log.LogWarning("World-origin camera synchronization unavailable: " + ex);
            }
        }

        private static void Before(out Snapshot __state)
        {
            Transform car = CameraBinding.Car;
            __state = new Snapshot { Car = car, Position = car != null ? car.position : new Vector3() };
        }

        private static void After(Snapshot __state)
        {
            if (__state.Car == null || __state.Car != CameraBinding.Car) return;
            Vector3 delta = __state.Car.position - __state.Position;
            if (delta.sqrMagnitude <= 0f) return; // Ordinary frames do no work.
            if (!Finite(delta.x) || !Finite(delta.y) || !Finite(delta.z)) return;
            ChaseView.ShiftOrigin(__state.Car, delta);
            ApocaplayerBridge.ShiftOrigin(__state.Car, delta);
        }

        private static bool Finite(float value) { return !Single.IsNaN(value) && !Single.IsInfinity(value); }

        internal static void Shutdown()
        {
            if (harmony != null) harmony.UnpatchSelf();
            harmony = null;
        }
    }
}
