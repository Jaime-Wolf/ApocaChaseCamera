using System;
using System.Reflection;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using UnityEngine;

namespace ApocaChaseCamera
{
    // The dynamic cursor projects the native eye's aim, not just the rendered
    // chase view. Passive recenter must move that aim and its input caches too.
    internal static class DrivingAim
    {
        private static GameObject player;
        private static PlayMakerFSM look;
        private static Action<MouseLook, float> yaw, pitch;
        private static float nextSearch;
        private static bool failed, reported;

        internal static void Reset()
        { player = null; look = null; nextSearch = 0f; reported = false; }

        private static Action<MouseLook, float> Writer(string name)
        {
            FieldInfo field = typeof(MouseLook).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(float) || field.IsInitOnly)
                throw new MissingFieldException(typeof(MouseLook).FullName, name);
            using (DynamicMethodDefinition method = new DynamicMethodDefinition(
                "ApocaChaseCamera.Aim." + name, typeof(void), new Type[] { typeof(MouseLook), typeof(float) }))
            {
                ILProcessor code = method.Definition.Body.GetILProcessor();
                code.Emit(OpCodes.Ldarg_0); code.Emit(OpCodes.Ldarg_1);
                code.Emit(OpCodes.Stfld, method.Definition.Module.ImportReference(field));
                code.Emit(OpCodes.Ret);
                return (Action<MouseLook, float>)Delegate.CreateDelegate(typeof(Action<MouseLook, float>),
                    DMDCecilGenerator.Generate(method, null));
            }
        }

        internal static void Recenter(Camera camera, Quaternion rotation)
        {
            if (!ApocaplayerBridge.Driving(camera)) return;
            // The eye defines the real interaction ray even when its native
            // Look FSM is dormant. Never make aim correction depend on finding
            // an active input action, or on private-cache writer availability.
            camera.transform.rotation = rotation;
            if (failed) return;
            try
            {
                if (player != CameraBinding.Player) { Reset(); player = CameraBinding.Player; }
                if (look == null && player != null && Time.unscaledTime >= nextSearch)
                {
                    nextSearch = Time.unscaledTime + 0.5f;
                    foreach (PlayMakerFSM fsm in player.GetComponents<PlayMakerFSM>())
                        if (fsm.FsmName == "Look") { look = fsm; break; }
                }
                int matched = 0;
                if (look != null && look.Fsm.Initialized)
                {
                    FsmState active = look.Fsm.GetState(look.ActiveStateName);
                    FsmState normal = look.Fsm.GetState("Look"), steering = look.Fsm.GetState("MouseSteering");
                    matched += Sync(active, camera, rotation);
                    // The game may park the controller in an empty state while
                    // seated. Keep its camera/holder caches ready for resume.
                    if (normal != active) matched += Sync(normal, camera, rotation);
                    if (steering != active && steering != normal) matched += Sync(steering, camera, rotation);
                }
                if (!reported)
                {
                    reported = true;
                    Plugin.Log.LogInfo("Driving aim recenter active: eye=" + camera.gameObject.name +
                        "; native Look=" + (look == null ? "not found" : look.ActiveStateName + ", enabled=" + look.enabled) +
                        "; camera input actions=" + matched + ". Aim follows the chase view before cursor projection.");
                }
            }
            catch (Exception ex)
            {
                failed = true;
                Plugin.Log.LogWarning("Driving aim cache synchronization unavailable; eye recenter remains active: " + ex);
            }
            finally
            {
                // Rotating the holder also rotates an attached eye in Unity.
                // Keep the established cursor-time eye correction authoritative.
                camera.transform.rotation = rotation;
            }
        }

        private static int Sync(FsmState state, Camera camera, Quaternion rotation)
        {
            if (state == null || state.Actions == null) return 0;
            int count = 0;
            foreach (FsmStateAction action in state.Actions)
            {
                MouseLook input = action as MouseLook;
                if (input == null || !input.Enabled || input.gameObject == null) continue;
                GameObject target = look.Fsm.GetOwnerDefaultTarget(input.gameObject);
                if (target == null) continue;
                // Identity plus the player's own Look action binds the native
                // scene-root holder; do not require it to be under the capsule.
                bool holder = CameraBinding.CameraHolder != null && target.transform == CameraBinding.CameraHolder;
                if (target != camera.gameObject && !holder) continue;
                if (yaw == null) { yaw = Writer("rotationX"); pitch = Writer("rotationY"); }
                Quaternion local = target.transform.parent == null ? rotation :
                    Quaternion.Inverse(target.transform.parent.rotation) * rotation;
                Vector3 angles = local.eulerAngles;
                float x = CameraMath.Wrap(angles.x), y = CameraMath.Wrap(angles.y);
                // The native controller uses this holder, even when InCar has
                // moved the eye to the seat. Never rotate the player/steering.
                if (holder) target.transform.rotation = rotation;
                int axes = (int)input.axes;
                if (axes == 0 || axes == 1) yaw(input, y);
                // Native GetYRotation(-1) negates new input, not its cached
                // angle; both axis modes store the actual local pitch.
                if (axes == 0 || axes == 2) pitch(input, x);
                count++;
            }
            return count;
        }
    }
}
