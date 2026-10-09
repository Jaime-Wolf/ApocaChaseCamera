using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ApocaChaseCamera
{
    [BepInPlugin(GUID, "ApocaChaseCamera", VERSION)]
    [BepInDependency(Apocasetter.Plugin.GUID, BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string GUID = "local.apocalypter.chasecamera";
        public const string VERSION = "0.1.6";
        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled, Recenter, HudEnabled;
        internal static ConfigEntry<float> Height, Distance, Side, Pitch, BumpTime,
            TurnTime, RecenterDelay, RecenterTime, Sensitivity, RecoveryTime, MaxHeightLag, GroundClearance,
            HudScale, HudGap;
        private static GameObject runner;

        private void Awake()
        {
            Log = Logger;
            Config.Bind("General", "Apocasetter", true, "Show this mod in the MODS menu.");
            Enabled = Config.Bind("General", "Enabled", true, "Steady third-person driving camera for every vehicle using the standard driving system. Use the normal Change Camera control.");
            Height = Slider("Position", "Height", 1.7f, -0.5f, 5f, "Extra camera height above the vehicle's driving-camera anchor, in metres. Raise this to see more road.");
            Distance = Slider("Position", "Distance", 6.5f, 2f, 15f, "Distance behind the camera anchor, in metres. Increase for larger vehicles.");
            Side = Slider("Position", "Side offset", 0f, -3f, 3f, "Horizontal offset in metres. Negative moves left; positive moves right.");
            Pitch = Slider("Position", "Look down angle", 8f, -10f, 35f, "Default downward viewing angle in degrees. Lower values frame the vehicle lower and show more of the road ahead.");
            BumpTime = Slider("Comfort", "Bump smoothing", 0.35f, 0f, 1f, "Vertical smoothing in seconds. Higher values soften bumps more; zero follows the anchor directly. Vehicle roll and pitch never tilt the view.");
            MaxHeightLag = Slider("Comfort", "Maximum height lag", 0.6f, 0f, 2f, "Maximum vertical lag behind the vehicle in metres. This makes steep climbs and drops catch up quickly while small bumps stay smoothed. Lower values follow elevation changes more closely.");
            GroundClearance = Slider("Comfort", "Ground clearance", 0.6f, 0.1f, 2f, "Minimum clearance above ground in metres, in addition to the camera clipping radius. Terrain pushes the camera closer to the vehicle instead of lifting it far into the air.");
            TurnTime = Slider("Comfort", "Turn smoothing", 0.25f, 0f, 1f, "Seconds used to follow the vehicle's heading. Higher values turn the camera more gently; zero follows instantly.");
            RecoveryTime = Slider("Comfort", "Collision recovery", 0.45f, 0f, 2f, "Seconds used to pull back after a wall or obstacle. The camera moves inward immediately for clearance.");
            Recenter = Config.Bind("Controls", "Auto recenter", true, "Return behind the vehicle after mouse look. Disable to keep your chosen horizontal orbit.");
            RecenterDelay = Slider("Controls", "Recenter delay", 1.5f, 0f, 5f, "Seconds after mouse movement before returning behind the vehicle.");
            RecenterTime = Slider("Controls", "Recenter smoothing", 0.7f, 0.2f, 2f, "How gently mouse look returns behind the vehicle, in seconds. Higher values return more slowly. Return motion eases in and is limited to 120 degrees per second.");
            Sensitivity = Slider("Controls", "Mouse sensitivity", 2f, 0.1f, 8f, "Mouse-look sensitivity. Mouse movement changes the view immediately.");
            HudEnabled = Config.Bind("Driving HUD", "Enabled", true, "Show speed, RPM gauge, and current gear / total forward gears above the inventory while driving in third person. Reads the vehicle directly; ApocaGearbox is not required.");
            HudScale = Slider("Driving HUD", "Size", 1f, 0.65f, 1.75f, "Size of the driving display. It follows the inventory HUD and adapts to the screen.");
            HudGap = Slider("Driving HUD", "Height above inventory", 10f, 0f, 160f, "Gap above the inventory in UI units. Increase to move the driving display higher.");
            Enabled.SettingChanged += Changed;
            SceneManager.sceneLoaded += SceneLoaded;
            Camera.onPreCull += ChaseView.PreCull;
            Camera.onPostRender += ChaseView.PostRender;
            EnsureRunner();
            Log.LogInfo("ApocaChaseCamera " + VERSION + " ready. Use the normal third-person driving camera; adjust in MODS.");
        }

        private ConfigEntry<float> Slider(string section, string key, float initial, float min, float max, string description)
        { return Config.Bind(section, key, initial, new ConfigDescription(description, new AcceptableValueRange<float>(min, max))); }

        private static void Changed(object sender, EventArgs args) { ChaseView.Reset(); if (!Enabled.Value) DrivingHud.Reset(); }
        private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        { ChaseView.Reset(); DrivingHud.Reset(); CameraBinding.Reset(); ApocaplayerBridge.Refresh(); EnsureRunner(); }

        private static void EnsureRunner()
        {
            if (runner != null && runner.activeInHierarchy) return;
            runner = new GameObject("ApocaChaseCamera.Runner");
            runner.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(runner);
            runner.AddComponent<CameraRunner>();
        }

        // The game removes normal plugin hosts during loading. The hidden runner
        // and static callbacks survive that; shut down only on actual app exit.
        private void OnApplicationQuit()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            Camera.onPreCull -= ChaseView.PreCull;
            Camera.onPostRender -= ChaseView.PostRender;
            Enabled.SettingChanged -= Changed;
            ChaseView.Reset(); DrivingHud.Reset(); ApocaplayerBridge.Shutdown();
        }
    }

    internal sealed class CameraRunner : MonoBehaviour
    {
        private void Update() { ChaseView.Tick(); }
        private void LateUpdate() { DrivingHud.LateTick(); }
        private void OnDisable() { ChaseView.Reset(); DrivingHud.Reset(); }
    }
}
