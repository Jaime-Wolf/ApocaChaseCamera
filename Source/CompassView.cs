using System;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace ApocaChaseCamera
{
    // Use the stock on-foot compass actions for both driving views. Only their
    // heading input changes; the native axis/layout settings remain authoritative.
    internal static class CompassView
    {
        private static Transform arrow;
        private static Quaternion nativeRotation, displayedRotation;
        private static bool applied;
        private static float nextLookup, nextNativeLookup, nextError;
        private static PlayMakerFSM compass;
        private static GetRotation read;
        private static FloatMultiply multiply;
        private static SetRotation write;

        // Native Player/Compass UI and DriveTrigger/Camera both write this
        // arrow. Run after their Update/LateUpdate and before canvas geometry
        // is prepared. First person must use the actual eye, not the holder's
        // stale mouse-look heading left over from third person.
        internal static void Sync()
        {
            try { SyncDrivingView(); }
            catch (Exception ex)
            {
                Reset();
                if (Time.unscaledTime < nextError) return;
                nextError = Time.unscaledTime + 10f;
                Plugin.Log.LogWarning("Compass view suspended: " + ex);
            }
        }

        private static void SyncDrivingView()
        {
            if (!Plugin.Enabled.Value || !Apocasetter.GameMenu.InGame ||
                Apocasetter.GameMenu.Paused || Apocasetter.InputBlocker.Active ||
                !Application.isFocused || Time.timeScale <= 0f ||
                !CameraBinding.Resolve() || CameraBinding.SpecialView())
            { Release(); return; }
            float yaw;
            if (ChaseView.CompassHeading(out yaw)) { Apply(yaw); return; }
            Camera native = CameraBinding.NativeCamera;
            if (native == null || !native.isActiveAndEnabled)
            {
                // Another camera mod owns this eye's custom render matrices.
                // Only our successful view above can supply its third-person yaw.
                if (ApocaplayerBridge.ThirdPersonView) { Release(); return; }
                native = CameraBinding.FirstCamera;
            }
            if (native == null || !native.isActiveAndEnabled) { Release(); return; }
            Vector3 flat = Vector3.ProjectOnPlane(native.transform.forward, Vector3.up);
            if (flat.sqrMagnitude <= 0.000001f) { Release(); return; }
            Apply(Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg);
        }

        internal static void Apply(float yaw)
        {
            if (arrow == null)
            {
                applied = false;
                if (Time.unscaledTime < nextLookup) return;
                nextLookup = Time.unscaledTime + 1f;
                GameObject target = GameObject.Find("Canvas/Compass/Compass Arrow");
                if (target == null) return;
                arrow = target.transform;
            }
            if (!arrow.gameObject.activeInHierarchy || !BindNative()) return;
            // A native FSM can write again between multiple canvas/render
            // callbacks. Capture that newer value instead of an older snapshot.
            if (!applied || !OwnsRotation()) nativeRotation = arrow.rotation;
            // Always replace the input before multiplying, including repeated
            // canvas callbacks. Restore the shared variable even on failure.
            float previous = read.yAngle.Value;
            try
            {
                read.yAngle.Value = CameraMath.Wrap(yaw);
                multiply.OnUpdate();
                if (write.lateUpdate) write.OnLateUpdate(); else write.OnUpdate();
            }
            finally { read.yAngle.Value = previous; }
            displayedRotation = arrow.rotation;
            applied = true;
        }

        private static bool BindNative()
        {
            if (compass != null && compass.gameObject == CameraBinding.Player &&
                write != null && compass.Fsm.GetOwnerDefaultTarget(write.gameObject) == arrow.gameObject)
                return true;
            compass = null; read = null; multiply = null; write = null;
            if (Time.unscaledTime < nextNativeLookup || CameraBinding.Player == null) return false;
            nextNativeLookup = Time.unscaledTime + 1f;
            foreach (PlayMakerFSM candidate in CameraBinding.Player.GetComponents<PlayMakerFSM>())
            {
                if (candidate.FsmName != "Compass UI" || !candidate.Fsm.Initialized) continue;
                FsmState state = candidate.Fsm.GetState("compass fps");
                FsmStateAction[] actions = state == null ? null : state.Actions;
                if (actions == null || actions.Length != 3) continue;
                GetRotation r = actions[0] as GetRotation;
                FloatMultiply m = actions[1] as FloatMultiply;
                SetRotation w = actions[2] as SetRotation;
                // Refuse an unfamiliar pipeline rather than modifying another
                // UI/controller. All three native actions operate on variable y.
                if (r == null || m == null || w == null || !r.Enabled || !m.Enabled || !w.Enabled ||
                    r.yAngle == null || m.floatVariable == null || w.zAngle == null ||
                    r.yAngle.IsNone || r.yAngle.Name != "y" || m.floatVariable.Name != "y" ||
                    w.zAngle.Name != "y" || r.space != Space.World || w.space != Space.World ||
                    candidate.Fsm.GetOwnerDefaultTarget(w.gameObject) != arrow.gameObject) continue;
                compass = candidate; read = r; multiply = m; write = w;
                Plugin.Log.LogInfo("Vehicle compass bound to the on-foot Compass UI actions.");
                return true;
            }
            return false;
        }

        // Keep the heading through post-render for the overlay canvas. Before
        // the next Update or when yielding the view, restore only our own write;
        // the native compass may already have supplied a newer rotation.
        internal static void Release()
        {
            if (applied && arrow != null && OwnsRotation()) arrow.rotation = nativeRotation;
            applied = false;
        }

        private static bool OwnsRotation()
        {
            Quaternion current = arrow.rotation;
            float dot = current.x * displayedRotation.x + current.y * displayedRotation.y +
                current.z * displayedRotation.z + current.w * displayedRotation.w;
            return Math.Abs(dot) > 0.999999f;
        }

        internal static void Reset()
        { Release(); arrow = null; compass = null; read = null; multiply = null; write = null; nextLookup = nextNativeLookup = 0f; }
    }
}
