using System;
using System.Reflection;
using NWH.VehiclePhysics2;
using UnityEngine;
using UnityEngine.UI;

namespace ApocaChaseCamera
{
    internal static class HudRegressionChecks
    {
        private static int count;
        private static VehicleController vehicle;
        private static GameObject canvas, inventory;
        private static Texture2D metal;

        private static void Check(bool pass, string why)
        { count++; if (!pass) throw new Exception("HUD regression: " + why); }
        private static T Field<T>(string name)
        { return (T)typeof(DrivingHud).GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null); }
        private static GameObject Child(string name, GameObject parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent.transform, false); return child;
        }
        private static GameObject Slot(GameObject host, string name, float x)
        {
            GameObject slot = Child(name, host); RectTransform rect = slot.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(70f, 80f); rect.anchoredPosition = new Vector2(x, 0f);
            slot.AddComponent<Image>(); return slot;
        }
        private static GameObject MakeInventory(GameObject host)
        {
            GameObject slots = Child("WeaponSlots_UI", host);
            slots.GetComponent<RectTransform>().anchoredPosition = new Vector2(650f, -425f);
            for (int i = 0; i < 3; i++) Slot(slots, "Regression slot " + i, -80f + i * 80f);
            return slots;
        }
        private static GameObject MakeCanvas(string name, bool font)
        {
            GameObject host = new GameObject(name, typeof(RectTransform));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            Child("weapons_bg", Child("Design_UI", host)).AddComponent<RawImage>().texture = metal;
            if (font) Child("NutsLable", host).AddComponent<Text>().font = new Font();
            return host;
        }
        private static void Tick(float delta)
        { Time.unscaledTime += delta; Time.frameCount++; DrivingHud.LateTick(); }
        private static void Setup(bool font)
        {
            DrivingHud.Reset(); ChaseView.Reset(); CameraBinding.Reset(); GameObject.Registry.Clear();
            Plugin.Enabled.Value = Plugin.HudEnabled.Value = true;
            Apocasetter.GameMenu.InGame = true; Apocasetter.GameMenu.Paused = Apocasetter.InputBlocker.Active = false;
            Application.isFocused = true; Time.timeScale = 1f;
            Apocaplayer.ThirdPerson.On = Apocaplayer.ThirdPerson.Peek = false; Apocaplayer.Game.Weapon = "";
            GameObject car = new GameObject("HUD regression vehicle"); vehicle = car.AddComponent<VehicleController>();
            vehicle.SpeedSigned = 20f; vehicle.powertrain.engine.OutputRPM = 3000f; vehicle.powertrain.transmission.Gear = 3;
            GameObject player = Child("Player", Child("sitPos", car));
            player.Add(new PlayMakerFSM { FsmName = "InCar", ActiveStateName = "InCar" });
            player.Add(new PlayMakerFSM { FsmName = "Health", ActiveStateName = "Idle" });
            Child("Camera", Child("3rdCamera", Child("DriveTrigger", car))).AddComponent<Camera>();
            Child("PlayerCamera", Child("PlayerCameraHolder", player)).AddComponent<Camera>().enabled = false;
            metal = new Texture2D(); canvas = MakeCanvas("HUD regression canvas", font); inventory = MakeInventory(canvas);
            Tick(0.02f);
        }
        private static VertexHelper Populate(GaugeFace gauge)
        {
            VertexHelper mesh = new VertexHelper();
            typeof(GaugeFace).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(gauge, new object[] { mesh });
            return mesh;
        }
        private static bool NeedleColor(VertexHelper mesh, float red, float green)
        {
            foreach (Color color in mesh.colors)
                if (Math.Abs(color.r - red) < 0.001f && Math.Abs(color.g - green) < 0.001f) return true;
            return false;
        }

        internal static int Run()
        {
            count = 0; Setup(true);
            RectTransform original = Field<RectTransform>("root");
            Check(original != null && original.gameObject.activeSelf, "baseline third-person HUD is visible");
            int arrays = GameObject.HierarchyArrayScans, lists = GameObject.HierarchyListScans;
            int corners = RectTransform.WorldCornerQueries;
            for (int i = 0; i < 120; i++) Tick(0.001f);
            Check(GameObject.HierarchyArrayScans == arrays, "steady HUD frames allocate no hierarchy result arrays");
            Check(GameObject.HierarchyListScans == lists, "steady hierarchy is retained between refreshes");
            Check(RectTransform.WorldCornerQueries - corners <= 6, "120 frames do not repeat every graphic corner query");

            float before = original.anchoredPosition.x;
            GameObject extra = Slot(inventory, "Direct added slot", -400f); Tick(0.001f);
            Check(original.anchoredPosition.x < before - 100f, "direct extra slots update bounds on the next frame");
            GameObject group = Child("Nested slot group", inventory); Tick(0.001f);
            before = original.anchoredPosition.x; GameObject nested = Slot(group, "Nested added slot", -800f);
            Tick(0.51f);
            Check(original.anchoredPosition.x < before - 100f, "nested hierarchy changes appear within the bounded refresh");
            nested.SetActive(false); Tick(0.11f);
            Check(Math.Abs(original.anchoredPosition.x - before) < 0.01f, "inactive nested slots stop affecting bounds");
            before = original.anchoredPosition.x;
            RectTransform slotsRect = inventory.GetComponent<RectTransform>();
            slotsRect.anchoredPosition = new Vector2(slotsRect.anchoredPosition.x - 50f, slotsRect.anchoredPosition.y);
            Tick(0.001f);
            Check(Math.Abs(original.anchoredPosition.x - before + 50f) < 0.01f, "cached bounds follow live inventory movement each frame");
            before = original.anchoredPosition.x; extra.GetComponent<RectTransform>().sizeDelta = new Vector2(120f, 80f);
            Tick(0.11f);
            Check(original.anchoredPosition.x < before - 10f, "graphic resizing updates cached bounds at the text cadence");

            DrivingSnapshot reading; Check(DrivingReadings.Capture(vehicle, out reading), "live numeric snapshot is available");
            string gear = DrivingReadings.Gear(vehicle, reading.SelectedGear);
            Check(System.Object.ReferenceEquals(gear, DrivingReadings.Gear(vehicle, reading.SelectedGear)), "unchanged gear reuses formatted text");
            int length = vehicle.powertrain.transmission.gears.Count;
            vehicle.powertrain.transmission.gears[length - 1] = -0.8f; Tick(0.11f);
            Check(vehicle.powertrain.transmission.gears.Count == length && Field<Text>("gear").text == "3 / 4",
                "same-length ratio edits update the forward-gear count");
            vehicle.powertrain.transmission.Gear = -2; Tick(0.11f);
            Check(Field<Text>("gear").text == "R2 / 4", "selected gear changes refresh normally");

            Sprite previousSprite = Field<Sprite>("rustFrame"); inventory.SetActive(false); inventory = MakeInventory(canvas);
            Tick(0.51f); RectTransform replacement = Field<RectTransform>("root");
            Check(replacement != null && replacement.gameObject.activeSelf && !System.Object.ReferenceEquals(original, replacement),
                "replaced native inventory gets a visible replacement HUD");
            Check(System.Object.ReferenceEquals(Field<RectTransform>("inventory"), inventory.GetComponent<RectTransform>()),
                "HUD binds to the replacement inventory");
            Check(previousSprite.destroyed && !metal.destroyed, "inventory reconstruction frees its wrapper while retaining native texture");

            previousSprite = Field<Sprite>("rustFrame"); UnityEngine.Object.Destroy(replacement.gameObject); Tick(0.001f);
            Check(Field<RectTransform>("root") != null && Field<RectTransform>("root").gameObject.activeSelf,
                "externally destroyed HUD root is rebuilt immediately");
            Check(previousSprite.destroyed && !metal.destroyed, "external root destruction does not leak the owned Sprite");
            replacement = Field<RectTransform>("root"); previousSprite = Field<Sprite>("rustFrame");
            UnityEngine.Object.Destroy(inventory); inventory = MakeInventory(canvas); Tick(0.001f);
            Check(Field<RectTransform>("root") != null && Field<RectTransform>("root").gameObject.activeSelf,
                "destroyed native inventory recovers without a scene reload");
            Check(previousSprite.destroyed, "native inventory destruction disposes the old wrapper");
            GameObject newParent = MakeCanvas("Reparented HUD canvas", true);
            inventory.transform.SetParent(newParent.transform, false); Tick(0.001f);
            Check(Field<RectTransform>("root").parent == newParent.transform, "inventory reparenting moves HUD ownership to its current parent");

            GaugeFace gauge = Field<GaugeFace>("rpmDial"); gauge.Fraction = 0.25f; VertexHelper first = Populate(gauge);
            int builds = gauge.ArtworkBuilds;
            for (int i = 0; i < 100; i++) { gauge.Fraction = i / 100f; Populate(gauge); }
            Check(gauge.ArtworkBuilds == builds, "100 needle changes reuse the static dial geometry");
            gauge.Fraction = 0.95f; VertexHelper red = Populate(gauge);
            Check(NeedleColor(first, 0.94f, 0.70f) && NeedleColor(red, 0.96f, 0.25f), "cached artwork preserves the near-redline needle warning");
            RectTransform dialRect = gauge.rectTransform; dialRect.sizeDelta = new Vector2(20f, 0f); Populate(gauge);
            Check(gauge.ArtworkBuilds == builds + 1, "resizing invalidates the static artwork once");
            gauge.GearFace = true; VertexHelper gearMesh = Populate(gauge);
            Check(gauge.ArtworkBuilds == builds + 2 && !NeedleColor(gearMesh, 0.96f, 0.25f), "changing face type rebuilds artwork and removes the needle");

            Setup(false); arrays = GameObject.HierarchyArrayScans;
            for (int i = 0; i < 100; i++) Tick(0.001f);
            Check(Field<RectTransform>("root") == null && GameObject.HierarchyArrayScans == arrays,
                "missing font template retries remain bounded after partial binding");
            Child("NutsLable", canvas).AddComponent<Text>().font = new Font(); Tick(1.01f);
            Check(Field<RectTransform>("root") != null && Field<RectTransform>("root").gameObject.activeSelf,
                "HUD recovers when the delayed native font arrives");
            DrivingHud.Reset();
            Console.WriteLine("Driving HUD regressions: " + count + " checks for cache cadence, live UI changes, gearing edits, reconstruction and artwork reuse.");
            return count;
        }
    }
}
