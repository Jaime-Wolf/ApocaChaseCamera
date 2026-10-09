using System;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;

namespace ApocaChaseCamera
{
    internal static class BindingRegressionChecks
    {
        private static int checks;
        private static GameObject player, car, third;
        private static Camera native, eye;
        private static PlayMakerFSM inCar, health, ads, binoculars;
        private static void Check(bool condition, string why)
        { checks++; if (!condition) throw new Exception("Binding regression: " + why); }
        private static GameObject Child(string name, GameObject parent)
        { GameObject go = new GameObject(name); go.transform.SetParent(parent.transform, false); return go; }
        private static void Setup(bool cameraPresent)
        {
            ChaseView.Reset(); CameraBinding.Reset(); GameObject.Registry.Clear();
            car = new GameObject("Binding car"); GameObject drive = Child("DriveTrigger", car);
            third = Child("3rdCamera", drive); third.transform.localPosition = new Vector3(0, 0.7f, 0);
            native = cameraPresent ? Child("Camera", third).Add(new Camera()) : null;
            player = Child("Player", Child("sitPos", car));
            inCar = player.Add(new PlayMakerFSM { FsmName = "InCar", ActiveStateName = "InCar" });
            health = player.Add(new PlayMakerFSM { FsmName = "Health", ActiveStateName = "Idle" });
            GameObject eyeGo = Child("PlayerCamera", Child("PlayerCameraHolder", player));
            eye = eyeGo.Add(new Camera()); eye.enabled = false;
            ads = eyeGo.Add(new PlayMakerFSM { FsmName = "AimDownSIghts_Hold", ActiveStateName = "idle" });
            binoculars = Child("Binocular Anim", Child("ItemAnim", eyeGo)).Add(new PlayMakerFSM { FsmName = "Animation", ActiveStateName = "off" });
            Time.unscaledTime += 1f; Time.frameCount++;
            Plugin.Enabled.Value = true; Plugin.HudEnabled.Value = true;
            Application.isFocused = true; Apocasetter.GameMenu.InGame = true; Apocasetter.GameMenu.Paused = false;
            Apocasetter.InputBlocker.Active = false; Time.timeScale = 1f;
        }

        internal static int Run()
        {
            checks = 0;
            Setup(false);
            Check(CameraBinding.Resolve() && CameraBinding.NativeCamera == null, "a late camera does not prevent initial vehicle binding");
            native = Child("Camera", third).Add(new Camera());
            Time.unscaledTime += 0.51f;
            Check(CameraBinding.Resolve() && CameraBinding.NativeCamera == native, "late-created camera is found without leaving the car");
            Check(Math.Abs(CameraBinding.LocalAnchor.y - 0.7f) < 0.0001f, "late camera uses the real anchor");
            UnityEngine.Object.Destroy(native);
            Camera replacement = third.Add(new Camera()); Time.unscaledTime += 0.51f;
            Check(CameraBinding.Resolve() && CameraBinding.NativeCamera == replacement, "destroyed camera component can be replaced on the same anchor");
            Transform drive = third.transform.parent; UnityEngine.Object.Destroy(third);
            third = Child("3rdCamera", drive.gameObject); third.transform.localPosition = new Vector3(0, 3f, 0);
            replacement = third.Add(new Camera()); Time.unscaledTime += 0.51f;
            Check(CameraBinding.Resolve() && CameraBinding.NativeCamera == replacement, "replacement anchor is rediscovered on the same vehicle");
            Check(Math.Abs(CameraBinding.LocalAnchor.y - 3f) < 0.0001f, "replacement anchor updates LocalAnchor");

            Setup(true); Check(CameraBinding.Resolve(), "stable binding warms up");
            Transform.FindCalls = 0; FsmVariables.Searches = 0;
            for (int i = 0; i < 120; i++) Check(CameraBinding.Resolve(), "repeated same-frame binding remains valid");
            Check(Transform.FindCalls == 0, "Update and LateUpdate reuse the hierarchy binding within one frame");
            Check(FsmVariables.Searches == 0, "the bound Health variable is reused without repeated named searches");
            health.FsmVariables.Health.Value = 0.1f;
            Check(!CameraBinding.Resolve(), "same-frame health change hides the camera immediately");
            health.FsmVariables.Health.Value = 100f;
            Check(CameraBinding.Resolve(), "same-frame health recovery is not hidden by a cached Resolve result");
            health.ActiveStateName = "playerDeath"; Check(!CameraBinding.Resolve(), "death state remains a live guard");
            health.ActiveStateName = "Idle";
            ads.ActiveStateName = "ads"; Check(CameraBinding.SpecialView(), "ADS state remains live"); ads.ActiveStateName = "idle";
            binoculars.ActiveStateName = "animOn"; Check(CameraBinding.SpecialView(), "binocular state remains live"); binoculars.ActiveStateName = "off";
            inCar.ActiveStateName = "OutCar"; Check(!CameraBinding.Resolve(), "same-frame vehicle exit is immediate"); inCar.ActiveStateName = "InCar";
            native.enabled = false; eye.enabled = true; Apocaplayer.ThirdPerson.On = false;
            Check(CameraBinding.SelectedCamera() == null, "first-person selection is not mistaken for native third person");
            native.enabled = true; Check(CameraBinding.SelectedCamera() == native, "camera enabled state is observed immediately");
            GameObject secondCar = new GameObject("Second binding car"), secondDrive = Child("DriveTrigger", secondCar);
            Camera secondCamera = Child("Camera", Child("3rdCamera", secondDrive)).Add(new Camera());
            // The direct player parent stays the same: its seat is moved to another car.
            player.transform.parent.SetParent(secondCar.transform, false);
            Check(CameraBinding.Resolve() && CameraBinding.Car == secondCar.transform && CameraBinding.NativeCamera == secondCamera,
                "moving an ancestor seat changes cars in the same frame");
            Time.unscaledTime += 0.51f; CameraBinding.Resolve(); Transform.FindCalls = 0;
            for (int i = 0; i < 60; i++) { Time.unscaledTime += 1f / 60f; Time.frameCount++; CameraBinding.Resolve(); CameraBinding.Resolve(); }
            Check(Transform.FindCalls <= 3, "steady driving uses bounded camera probes rather than frame-rate hierarchy discovery");
            GameObject missingCameraCar = new GameObject("Camera-less car"); Child("DriveTrigger", missingCameraCar);
            player.transform.parent.SetParent(missingCameraCar.transform, false);
            Check(CameraBinding.Resolve() && CameraBinding.NativeCamera == null, "camera-less vehicle can retain optional first-camera support");
            Vector3 fallback = missingCameraCar.transform.InverseTransformPoint(player.transform.position);
            Check(Vector3.Distance(CameraBinding.LocalAnchor, fallback) < 0.0001f, "camera-less vehicle uses its own player anchor rather than the previous vehicle anchor");
            GameObject previousEye = eye.gameObject;
            UnityEngine.Object.Destroy(ads);
            ads = previousEye.Add(new PlayMakerFSM { FsmName = "AimDownSIghts_Hold", ActiveStateName = "ads" });
            Time.unscaledTime += 0.51f; CameraBinding.Resolve();
            Check(CameraBinding.SpecialView(), "replaced ADS component is rediscovered without camera replacement");
            ads.ActiveStateName = "idle";

            ValidateInterface();
            bool oldThrow = Plugin.Log.ThrowWarnings;
            try
            {
                Plugin.Log.ThrowWarnings = false;
                ApocaplayerBridge.Shutdown(); ApocaplayerBridge.Discover();
                Apocaplayer.ThirdPerson.On = true; Apocaplayer.ThirdPerson.Peek = false; Apocaplayer.ThirdPerson.AimZoom = false;
                Apocaplayer.Game.Weapon = "";
                Check(ApocaplayerBridge.CruisingView, "typed interface getters preserve the cruising view");
                Apocaplayer.ThirdPerson.AimZoom = true; Check(!ApocaplayerBridge.CruisingView, "typed getters observe same-frame aiming changes");
                Apocaplayer.ThirdPerson.AimZoom = false;
                Apocaplayer.Game.Weapon = "Rifle"; Check(!ApocaplayerBridge.CruisingView, "typed property getter preserves weapon handoff"); Apocaplayer.Game.Weapon = "";
                int warnings = Plugin.Log.Warnings;
                BridgeField("drawnWeapon").SetValue(null, (Func<string>)delegate { throw new InvalidOperationException("test getter failure"); });
                Check(!ApocaplayerBridge.CruisingView, "a throwing optional property fails closed");
                for (int i = 0; i < 100; i++) { ApocaplayerBridge.Refresh(); ApocaplayerBridge.Discover(); Check(!ApocaplayerBridge.ThirdPersonView, "failed bridge stays disabled"); }
                Check(Plugin.Log.Warnings == warnings + 1, "persistent bridge failure emits only one warning");
                ApocaplayerBridge.Shutdown(); ApocaplayerBridge.Discover();
                CameraBinding.FirstCamera = eye; Apocaplayer.Game.Cam = eye; Apocaplayer.Plugin.OcclusionPrototype.Value = true;
                Apocaplayer.Plugin.OcclusionInVehicle.Value = true;
                native.enabled = false; eye.enabled = true; Time.frameCount++; Time.unscaledTime += .016f;
                ChaseView.Tick(); Vector3 preparedPosition; Quaternion preparedRotation;
                Check(ChaseView.TryPose(eye, out preparedPosition, out preparedRotation), "eligible pose prepared before cutaway failure");
                BridgeField("cutaway").SetValue(null, (Action<Camera, Vector3, Quaternion, Transform>)delegate { throw new InvalidOperationException("test cutaway failure"); });
                warnings = Plugin.Log.Warnings;
                Check(!ApocaplayerBridge.Publish(eye, new Vector3(1, 2, 3), Quaternion.Euler(0, 0, 0)), "cutaway failure reports unsuccessful publication for native fallback");
                Check(Plugin.Log.Warnings == warnings + 1 && !ApocaplayerBridge.ThirdPersonView, "cutaway failure disables the bridge with a single warning");
                Check(ApocaplayerBridge.Publish(secondCamera, Vector3.one, Quaternion.Euler(0, 0, 0)), "non-owned native camera publication remains harmless");
            }
            finally
            {
                Apocaplayer.Plugin.OcclusionPrototype.Value = false; Apocaplayer.ThirdPerson.On = false;
                Apocaplayer.Plugin.OcclusionInVehicle.Value = false;
                Apocaplayer.ThirdPerson.Peek = Apocaplayer.ThirdPerson.AimZoom = false; Apocaplayer.Game.Weapon = "";
                ApocaplayerBridge.Shutdown(); ApocaplayerBridge.Discover(); Plugin.Log.ThrowWarnings = oldThrow;
                ChaseView.Reset(); CameraBinding.Reset();
            }
            Console.WriteLine("Binding/interface regressions: " + checks + " checks; bounded discovery, late/replaced cameras, live guards and typed bridge failure handling.");
            return checks;
        }

        private static FieldInfo BridgeField(string name)
        { return typeof(ApocaplayerBridge).GetField(name, BindingFlags.Static | BindingFlags.NonPublic); }
        private static void Rejected(string method, object[] arguments, string why)
        {
            bool rejected = false;
            try { typeof(ApocaplayerBridge).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments); }
            catch (TargetInvocationException ex) { rejected = ex.InnerException is InvalidOperationException; }
            Check(rejected, why);
        }
        private static void ValidateInterface()
        {
            Rejected("Field", new object[] { typeof(WrongFields), "Peek", typeof(bool), false }, "changed Peek type is rejected at discovery");
            Rejected("Field", new object[] { typeof(WrongFields), "On", typeof(bool), false }, "instance fields are rejected at discovery");
            Rejected("Field", new object[] { typeof(WrongFields), "ViewPos", typeof(Vector3), true }, "readonly output fields are rejected at discovery");
            Rejected("Getter", new object[] { typeof(WrongProperties), "DrawnWeapon", typeof(string) }, "changed weapon property type is rejected at discovery");
            Rejected("Getter", new object[] { typeof(WrongProperties), "Available", typeof(bool) }, "instance property getters are rejected at discovery");
            MethodInfo matches = typeof(ApocaplayerBridge).GetMethod("MethodMatches", BindingFlags.Static | BindingFlags.NonPublic);
            Check(!(bool)matches.Invoke(null, new object[] { typeof(WrongMethods).GetMethod("PreCull"), typeof(void), new Type[] { typeof(Camera) } }), "non-void render callback is rejected");
            Check(!(bool)matches.Invoke(null, new object[] { typeof(WrongMethods).GetMethod("Prepare"), typeof(void), new Type[] { typeof(Camera), typeof(Vector3), typeof(Quaternion), typeof(Transform) } }), "instance cutaway callback is rejected");
            MethodInfo read = typeof(ApocaplayerBridge).GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(typeof(bool));
            Func<bool> flag = (Func<bool>)read.Invoke(null, new object[] { typeof(TypedFields).GetField("Flag") });
            TypedFields.Flag = false; Check(!flag(), "typed static field getter reads false"); TypedFields.Flag = true; Check(flag(), "typed static field getter reads live true");
            MethodInfo write = typeof(ApocaplayerBridge).GetMethod("Write", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(typeof(Vector3));
            Action<Vector3> setPose = (Action<Vector3>)write.Invoke(null, new object[] { typeof(TypedFields).GetField("Pose") });
            setPose(new Vector3(3, 4, 5)); Check(TypedFields.Pose.x == 3 && TypedFields.Pose.y == 4 && TypedFields.Pose.z == 5, "typed struct output setter preserves the rendered pose");
        }
        private sealed class WrongFields { public static int Peek = 0; public bool On = false; public static readonly Vector3 ViewPos = Vector3.one; }
        private sealed class WrongProperties { public static int DrawnWeapon { get { return 0; } } public bool Available { get { return true; } } }
        private sealed class WrongMethods { public static int PreCull(Camera camera) { return 0; } public void Prepare(Camera camera, Vector3 position, Quaternion rotation, Transform car) { } }
        private sealed class TypedFields { public static bool Flag; public static Vector3 Pose = new Vector3(); }
    }
}
