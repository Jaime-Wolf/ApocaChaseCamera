using HutongGames.PlayMaker;
using UnityEngine;

namespace ApocaChaseCamera
{
    internal static class CameraBinding
    {
        internal static GameObject Player;
        internal static Transform Car;
        internal static Camera NativeCamera, FirstCamera;
        internal static Vector3 LocalAnchor;
        private static PlayMakerFSM inCar, health, ads, binoculars;
        private static FsmVariables healthVariables;
        private static FsmFloat healthAmount;
        private static Transform seatParent, thirdAnchor;
        private static float nextSearch, nextVehicleSearch, nextCameraSearch, nextSpecialSearch;

        internal static void Reset()
        {
            Player = null; Car = null; NativeCamera = null; FirstCamera = null;
            inCar = null; health = null; ads = null; binoculars = null;
            healthVariables = null; healthAmount = null; seatParent = thirdAnchor = null;
            nextSearch = nextVehicleSearch = nextCameraSearch = nextSpecialSearch = 0f;
        }

        private static PlayMakerFSM FindFsm(GameObject go, string name)
        {
            if (go == null) return null;
            foreach (PlayMakerFSM fsm in go.GetComponents<PlayMakerFSM>())
                if (fsm.FsmName == name) return fsm;
            return null;
        }

        internal static bool Resolve()
        {
            if ((Player == null || inCar == null || health == null || FirstCamera == null) &&
                Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 0.5f;
                if (Player == null)
                {
                    Player = GameObject.Find("Player");
                    inCar = health = null; healthVariables = null; healthAmount = null;
                    Car = null; NativeCamera = null; seatParent = thirdAnchor = null;
                    FirstCamera = null; ads = binoculars = null;
                    nextVehicleSearch = nextCameraSearch = nextSpecialSearch = 0f;
                }
                if (inCar == null) inCar = FindFsm(Player, "InCar");
                if (health == null) health = FindFsm(Player, "Health");
                if (FirstCamera == null)
                {
                    GameObject holder = GameObject.Find("PlayerCameraHolder");
                    Transform eye = holder == null ? null : holder.transform.Find("PlayerCamera");
                    FirstCamera = eye == null ? null : eye.GetComponent<Camera>();
                    ads = binoculars = null; nextSpecialSearch = 0f;
                }
            }
            RefreshSpecialViews();
            if (Player == null || inCar == null || inCar.ActiveStateName != "InCar") return false;
            if (health != null && health.Fsm.Initialized)
            {
                if (healthVariables != health.FsmVariables)
                {
                    healthVariables = health.FsmVariables;
                    healthAmount = healthVariables.FindFsmFloat("Health");
                }
                if (health.ActiveStateName == "playerDeath" || health.ActiveStateName == "backToMenu" ||
                    (healthAmount != null && healthAmount.Value < 0.4f)) return false;
            }
            Transform parent = Player.transform.parent;
            bool hierarchyChanged = seatParent != parent || (Car != null && !Player.transform.IsChildOf(Car));
            if (hierarchyChanged || (Car == null && Time.unscaledTime >= nextVehicleSearch))
            {
                seatParent = parent; nextVehicleSearch = Time.unscaledTime + 0.5f;
                Transform found = null;
                for (Transform ancestor = parent; ancestor != null; ancestor = ancestor.parent)
                    if (ancestor.Find("DriveTrigger") != null) { found = ancestor; break; }
                if (Car != found)
                {
                    Car = found; NativeCamera = null; thirdAnchor = null; nextCameraSearch = 0f;
                    ChaseView.Reset();
                    if (Car != null)
                    {
                        LocalAnchor = Car.InverseTransformPoint(Player.transform.position);
                        Plugin.Log.LogInfo("Driving camera bound to " + Car.name + ".");
                    }
                }
                else if (Car != null && thirdAnchor == null && hierarchyChanged)
                {
                    LocalAnchor = Car.InverseTransformPoint(Player.transform.position);
                    ChaseView.Reset();
                }
            }
            if (Car == null) return false;
            // A camera component or anchor may be installed after vehicle entry,
            // or replaced without replacing the vehicle. Probe at a bounded rate.
            if ((thirdAnchor != null && !thirdAnchor.IsChildOf(Car)) ||
                (NativeCamera != null && (thirdAnchor == null || !NativeCamera.transform.IsChildOf(thirdAnchor))))
            {
                NativeCamera = null; thirdAnchor = null; nextCameraSearch = 0f;
                ChaseView.Reset();
            }
            if (Time.unscaledTime >= nextCameraSearch)
            {
                nextCameraSearch = Time.unscaledTime + 0.5f;
                Transform third = Car.Find("DriveTrigger/3rdCamera");
                Camera camera = third == null ? null : third.GetComponentInChildren<Camera>(true);
                if (third != thirdAnchor || camera != NativeCamera)
                {
                    thirdAnchor = third; NativeCamera = camera;
                    LocalAnchor = third != null ? Car.InverseTransformPoint(third.position) :
                        Car.InverseTransformPoint(Player.transform.position);
                    ChaseView.Reset();
                }
            }
            return true;
        }

        private static void RefreshSpecialViews()
        {
            if (FirstCamera == null || (ads != null && binoculars != null) || Time.unscaledTime < nextSpecialSearch) return;
            nextSpecialSearch = Time.unscaledTime + 0.5f;
            Transform eye = FirstCamera.transform;
            if (ads == null)
            {
                ads = FindFsm(eye.gameObject, "AimDownSIghts_Hold");
                if (ads == null) ads = FindFsm(eye.gameObject, "AimDownSights_Hold");
            }
            if (binoculars == null)
            {
                Transform bino = eye.Find("ItemAnim/Binocular Anim");
                binoculars = bino == null ? null : FindFsm(bino.gameObject, "Animation");
            }
        }

        internal static bool SpecialView()
        {
            if (ads != null && ads.enabled && ads.ActiveStateName == "ads") return true;
            if (binoculars != null && binoculars.enabled)
            {
                string state = binoculars.ActiveStateName;
                if (state == "animOn" || state == "on" || state == "animOff") return true;
            }
            return false;
        }

        internal static Camera SelectedCamera()
        {
            if (NativeCamera != null && NativeCamera.isActiveAndEnabled) return NativeCamera;
            if (FirstCamera != null && FirstCamera.isActiveAndEnabled && ApocaplayerBridge.CruisingView)
                return FirstCamera;
            return null;
        }

        internal static bool ThirdPersonVisible()
        {
            return (NativeCamera != null && NativeCamera.isActiveAndEnabled) ||
                (FirstCamera != null && FirstCamera.isActiveAndEnabled && ApocaplayerBridge.ThirdPersonView);
        }

        internal static bool OwnCollider(Collider collider)
        {
            Transform t = collider.transform;
            return (Car != null && t.IsChildOf(Car)) || (Player != null && t.IsChildOf(Player.transform));
        }
    }
}
