using System;
using System.Reflection;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace ApocaChaseCamera
{
    internal static class CompassRegressionChecks
    {
        private static int checks;
        private static Transform arrow, holder;
        private static GameObject third;
        private static Camera camera, first;
        private static PlayMakerFSM inCar, ads;
        private static PlayMakerFSM nativeCompass;
        private static FsmFloat nativeYaw;
        private static FloatMultiply nativeMultiply;
        private static SetRotation nativeWrite;

        private static void Check(bool value, string why)
        { checks++; if (!value) throw new Exception("Compass regression: " + why); }
        private static void Near(float a, float b, string why)
        { Check(Math.Abs(CameraMath.Wrap(a - b)) < 0.001f, why + ": " + a + " vs " + b); }
        private static GameObject Child(string name, GameObject parent)
        { GameObject go = new GameObject(name); go.transform.Parent(parent.transform); return go; }
        private static float Field(string name)
        { return (float)typeof(ChaseView).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); }
        private static void Frame(float dt)
        {
            Time.unscaledTime += dt; Time.frameCount++; ChaseView.Tick();
            // The stock FSM reads a holder that keeps its mouse-look offset.
            arrow.rotation = Quaternion.Euler(0f, 0f, -holder.rotation.euler.y);
            CompassView.Sync();
            ChaseView.PreCull(camera); ChaseView.PostRender(camera);
        }
        private static void Setup()
        {
            ChaseView.Reset(); CameraBinding.Reset(); GameObject.Registry.Clear();
            GameObject car = new GameObject("Compass car"), drive = Child("DriveTrigger", car);
            third = Child("3rdCamera", drive); camera = Child("Camera", third).Add(new Camera());
            GameObject player = Child("Player", Child("Seat", car));
            inCar = player.Add(new PlayMakerFSM { FsmName = "InCar", ActiveStateName = "InCar" });
            player.Add(new PlayMakerFSM { FsmName = "Health", ActiveStateName = "Idle" });
            holder = Child("PlayerCameraHolder", player).transform;
            first = Child("PlayerCamera", holder.gameObject).Add(new Camera()); first.enabled = false;
            ads = first.gameObject.Add(new PlayMakerFSM { FsmName = "AimDownSIghts_Hold", ActiveStateName = "idle" });
            arrow = Child("Compass Arrow", Child("Compass", new GameObject("Canvas"))).transform;
            GameObject.Registry["Canvas/Compass/Compass Arrow"] = arrow.gameObject;
            nativeYaw = new FsmFloat {Name="y", Value=123f};
            nativeMultiply = new FloatMultiply {floatVariable=nativeYaw};
            nativeWrite = new SetRotation {zAngle=nativeYaw, space=Space.World,
                gameObject=new FsmOwnerDefault {Target=arrow.gameObject}};
            nativeCompass = player.Add(new PlayMakerFSM {FsmName="Compass UI", ActiveStateName="compass fps"});
            nativeCompass.Fsm.CompassState=new FsmState {Actions=new FsmStateAction[] {
                new GetRotation {yAngle=nativeYaw,space=Space.World},nativeMultiply,nativeWrite}};
            Physics.Hits = new RaycastHit[0]; Physics.Overlaps = new Collider[0]; Physics.GroundProbe = null;
            Terrain.activeTerrains = new Terrain[0]; Input.X = Input.Y = 0f;
            Plugin.Enabled.Value = true; Plugin.Recenter.Value = true;
            Plugin.RecenterDelay.Value = 1.5f; Plugin.RecenterTime.Value = .7f;
            Plugin.Sensitivity.Value = 2f; Plugin.Pitch.Value = 8f; Plugin.Distance.Value = 6.5f;
            Apocaplayer.ThirdPerson.On = false;
            Apocasetter.GameMenu.InGame = true; Apocasetter.GameMenu.Paused = false;
            Apocasetter.InputBlocker.Active = false; Application.isFocused = true; Time.timeScale = 1f;
        }

        internal static int Run()
        {
            checks = 0;
            Setup(); Frame(1f / 60f); Near(arrow.rotation.euler.z, 0f, "north initially");
            holder.rotation = Quaternion.Euler(0f, 90f, 0f);
            Input.X = 45f; Frame(1f / 60f); Input.X = 0f;
            Near(arrow.rotation.euler.z, -90f, "east after mouse orbit");
            for (int i = 0; i < 900; i++)
            {
                Frame(1f / 60f);
                Near(arrow.rotation.euler.z, -(Field("heading") + Field("orbit")), "compass follows every recenter frame");
            }
            Check(Math.Abs(arrow.rotation.euler.z) < .01f, "recenters to north while holder stays east");
            Near(holder.rotation.euler.y, 90f, "camera holder is not modified");
            holder.rotation = Quaternion.Euler(0f, 180f, 0f);
            Input.X = 45f; Frame(1f / 60f); Input.X = 0f;
            Check(Math.Abs(arrow.rotation.euler.z + 90f) < .01f, "second east orbit does not accumulate to south");
            float orbit = Field("orbit"); ChaseView.PreCull(camera); ChaseView.PostRender(camera);
            Near(Field("orbit"), orbit, "repeat renders do not apply input twice");
            Near(arrow.rotation.euler.z, -orbit, "overlay heading survives post-render");

            camera.transform.rotation = Quaternion.Euler(10f, 123f, 5f);
            Frame(1f / 60f); Near(camera.transform.rotation.euler.y, 123f, "native camera rotation untouched");
            Plugin.Enabled.Value = false; Frame(1f / 60f);
            Near(arrow.rotation.euler.z, -180f, "disabled camera leaves native heading");
            Plugin.Enabled.Value = true; Frame(1f / 60f);
            third.SetActive(false); first.enabled = true; Frame(1f / 60f);
            Near(arrow.rotation.euler.z, 0f, "first person uses actual eye instead of stale holder heading");
            third.SetActive(true); first.enabled = false; Frame(1f / 60f);
            ads.ActiveStateName = "ads"; Frame(1f / 60f);
            Near(arrow.rotation.euler.z, -180f, "aiming leaves native heading");
            ads.ActiveStateName = "idle"; Frame(1f / 60f);
            Apocasetter.GameMenu.Paused = true; Frame(1f / 60f);
            Near(arrow.rotation.euler.z, -180f, "pause yields compass");
            Apocasetter.GameMenu.Paused = false; Frame(1f / 60f); Frame(1f / 60f);
            Physics.Overlaps = new[] { new GameObject("solid").Add(new Collider()) }; Frame(1f / 60f);
            Near(arrow.rotation.euler.z, -123f, "unresolved collision fallback uses actual native camera heading");
            Physics.Overlaps = new Collider[0]; Frame(1f / 60f);
            inCar.ActiveStateName = "OutCar"; Frame(1f / 60f);
            Near(arrow.rotation.euler.z, -180f, "vehicle exit yields compass");

            CompassView.Reset(); arrow.rotation = Quaternion.Euler(0f, 0f, 17f);
            CompassView.Apply(450f); Near(arrow.rotation.euler.z, -90f, "absolute heading wraps");
            CompassView.Apply(90f); CompassView.Release();
            Near(arrow.rotation.euler.z, 17f, "repeated writes preserve native snapshot");
            CompassView.Apply(90f); arrow.rotation = Quaternion.Euler(0f, 0f, 33f); CompassView.Release();
            Near(arrow.rotation.euler.z, 33f, "release respects a newer native compass write");
            GameObject.FindCalls = 0;
            for (int i = 0; i < 600; i++) { CompassView.Release(); CompassView.Apply(i); }
            Check(GameObject.FindCalls == 0, "stable UI does not search the scene each frame");
            CompassView.Reset(); UnityEngine.Object.Destroy(arrow.gameObject);
            GameObject.Registry.Remove("Canvas/Compass/Compass Arrow"); GameObject.FindCalls = 0;
            for (int i = 0; i < 60; i++) { Time.unscaledTime += .01f; CompassView.Apply(0f); }
            Check(GameObject.FindCalls == 1, "missing compass discovery is throttled");
            arrow = new GameObject("Canvas/Compass/Compass Arrow").transform;
            nativeWrite.gameObject.Target=arrow.gameObject;
            Time.unscaledTime += 1.1f; CompassView.Apply(-90f);
            Near(arrow.rotation.euler.z, 90f, "late replacement UI is rediscovered");
            CompassView.Reset(); Near(arrow.rotation.euler.z, 0f, "scene reset restores native rotation");
            SwitchingChecks();
            NativePipelineChecks();
            ChaseView.Reset(); CameraBinding.Reset(); Input.X = Input.Y = 0f;
            Console.WriteLine("Compass: reported orbit/recenter drift, native handoff, overlay lifetime and cached discovery passed.");
            return checks;
        }

        private static void SwitchingChecks()
        {
            Setup();
            Transform car = camera.transform.parent.parent.parent;
            car.rotation = Quaternion.Euler(0f, 45f, 0f);
            holder.rotation = Quaternion.Euler(0f, -45f, 0f);
            first.transform.rotation = Quaternion.Euler(8f, 45f, 0f);
            camera.transform.rotation = Quaternion.Euler(8f, -45f, 0f);
            Frame(1f / 60f);
            Near(arrow.rotation.euler.z, -45f, "third-person heading differs from stale native holders");
            for (int i = 0; i < 12; i++)
            {
                third.SetActive(false); first.enabled = true; Frame(1f / 60f);
                Near(arrow.rotation.euler.z, -45f, "switching to same-facing first-person keeps compass bearing");
                third.SetActive(true); first.enabled = false; Frame(1f / 60f);
                Near(arrow.rotation.euler.z, -45f, "switching back to third-person keeps compass bearing");
            }
            // Native FSMs may run after our Update, and the canvas can be
            // prepared before any camera's pre-cull callback.
            Time.unscaledTime += 1f / 60f; Time.frameCount++; ChaseView.Tick();
            third.SetActive(false); first.enabled = true;
            arrow.rotation = Quaternion.Euler(0f, 0f, 79f);
            CompassView.Sync(); Near(arrow.rotation.euler.z, -45f, "late native switch to first-person wins over old chase selection");
            float orbit = Field("orbit");
            CompassView.Sync(); Near(Field("orbit"), orbit, "repeated canvas callbacks do not apply mouse orbit again");
            first.transform.rotation = Quaternion.Euler(25f, 135f, 12f);
            CompassView.Sync(); Near(arrow.rotation.euler.z, -135f, "first-person free look follows actual world-facing direction");
            first.transform.rotation = Quaternion.Euler(89f, 135f, 0f);
            CompassView.Sync(); Near(arrow.rotation.euler.z, -135f, "steep first-person pitch retains its actual horizontal bearing");
            first.transform.rotation = Quaternion.Euler(8f, 45f, 0f);
            Frame(1f / 60f);
            third.SetActive(true); first.enabled = false;
            camera.transform.rotation = Quaternion.Euler(8f, 45f, 0f);
            CompassView.Sync(); Near(arrow.rotation.euler.z, -45f, "late switch to native third-person fallback uses that camera");
            Frame(1f / 60f);
            Input.X = 45f; Frame(1f / 60f); Input.X = 0f;
            Near(arrow.rotation.euler.z, -135f, "orbit still follows chase view before switching");
            third.SetActive(false); first.enabled = true; Frame(1f / 60f);
            Near(arrow.rotation.euler.z, -45f, "switch after orbit restores actual first-person heading without accumulated offset");
            third.SetActive(true); first.enabled = false; Frame(1f / 60f); Frame(1f / 60f);
            Physics.BufferedQueries = Physics.AllocatingQueries = 0;
            CompassView.Sync(); CompassView.Sync(); ChaseView.PreCull(camera); ChaseView.PostRender(camera);
            Check(Physics.BufferedQueries + Physics.AllocatingQueries == 0,
                "multiple canvas/render callbacks reuse the already-built pose and collision results");
            Time.unscaledTime += 1f / 60f; Time.frameCount++; ChaseView.Tick();
            Physics.BufferedQueries = Physics.AllocatingQueries = 0;
            CompassView.Sync(); CompassView.Sync(); ChaseView.PreCull(camera); ChaseView.PostRender(camera); CompassView.Sync();
            Check(Physics.BufferedQueries == 4 && Physics.AllocatingQueries == 0,
                "canvas preparation and rendering share exactly four buffered collision queries per fresh clear frame");
            CompassView.Apply(90f); arrow.rotation = Quaternion.Euler(0f, 0f, 21f);
            CompassView.Apply(45f); CompassView.Release();
            Near(arrow.rotation.euler.z, 21f, "reapplied compass captures native FSM writes between render callbacks");
        }

        private static void NativePipelineChecks()
        {
            Setup(); Frame(1f/60f);
            nativeCompass.enabled=false; // The game disables the on-foot FSM in third person.
            FloatMultiply.Calls=SetRotation.Calls=0;
            arrow.rotation=Quaternion.Euler(11f,17f,29f);
            CompassView.Apply(90f);
            Near(arrow.rotation.euler.x,11f,"native compass preserves its X layout axis");
            Near(arrow.rotation.euler.y,17f,"native compass preserves its Y layout axis");
            Near(arrow.rotation.euler.z,-90f,"disabled on-foot FSM actions still drive third person");
            Check(!nativeCompass.enabled,"compass adapter does not fight the vehicle FSM enable toggle");
            Near(nativeYaw.Value,123f,"shared on-foot yaw variable restored after drawing");
            Check(FloatMultiply.Calls==1 && SetRotation.Calls==1,"actual on-foot action pipeline used once");
            nativeMultiply.Factor=-2f; CompassView.Apply(30f);
            Near(arrow.rotation.euler.z,-60f,"native multiplier remains authoritative");
            nativeMultiply.Factor=-1f; nativeWrite.lateUpdate=true; CompassView.Apply(45f);
            Near(arrow.rotation.euler.z,-45f,"native late-update action executes without a missed needle write");
            for(int i=0;i<40;i++) CompassView.Apply(45f);
            Near(arrow.rotation.euler.z,-45f,"repeated callbacks cannot multiply the last displayed bearing");
            Near(nativeYaw.Value,123f,"repeated callbacks preserve native input state");
            CompassView.Reset(); nativeCompass.Fsm.CompassState.Actions=new FsmStateAction[0];
            arrow.rotation=Quaternion.Euler(0f,0f,71f); CompassView.Apply(90f);
            Near(arrow.rotation.euler.z,71f,"unknown on-foot action layout is left untouched");
            Setup(); Frame(1f/60f); CompassView.Reset(); nativeCompass.Fsm.Initialized=false;
            arrow.rotation=Quaternion.Euler(0f,0f,19f); CompassView.Apply(90f);
            Near(arrow.rotation.euler.z,19f,"uninitialized native compass is not forced to run");
            nativeCompass.Fsm.Initialized=true; Time.unscaledTime+=1.1f; CompassView.Apply(90f);
            Near(arrow.rotation.euler.z,-90f,"late native compass initialization is retried");
        }
    }
}
