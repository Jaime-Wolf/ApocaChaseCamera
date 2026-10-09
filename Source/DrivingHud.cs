using System;
using System.Collections.Generic;
using System.Globalization;
using NWH.VehiclePhysics2;
using UnityEngine;
using UnityEngine.UI;

namespace ApocaChaseCamera
{
    internal static class DrivingHud
    {
        private static RectTransform root, inventory, parent;
        private static Text speed, rpm, gear, speedLabel, rpmLabel, gearLabel;
        private static Image rpmFill;
        private static Sprite rustFrame;
        private static GaugeFace speedDial, rpmDial;
        private static Transform owner;
        private static VehicleController vehicle;
        private static float nextSearch, nextText, nextError, lastScale = -1f;
        private static float nextBindingCheck, nextGraphicsRefresh, nextBoundsRefresh;
        private static float nextVehicleSearch;
        private static int inventoryChildren = -1;
        private static bool hasBounds;
        private static float boundsLeft, boundsRight, boundsBottom, boundsTop;
        private static readonly List<Graphic> inventoryGraphics = new List<Graphic>(32);
        private static readonly Vector3[] corners = new Vector3[4];

        internal static void Reset()
        {
            ReleaseHud();
            vehicle = null; owner = null;
            DrivingReadings.Reset();
            nextSearch = nextText = nextVehicleSearch = 0f;
        }

        private static void ReleaseHud()
        {
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            if (rustFrame != null) UnityEngine.Object.Destroy(rustFrame);
            rustFrame = null; speedDial = rpmDial = null;
            root = inventory = parent = null;
            speed = rpm = gear = speedLabel = rpmLabel = gearLabel = null; rpmFill = null;
            inventoryGraphics.Clear(); inventoryChildren = -1;
            hasBounds = false;
            nextBindingCheck = nextGraphicsRefresh = nextBoundsRefresh = nextText = 0f; lastScale = -1f;
        }

        private static void Hide() { if (root != null) root.gameObject.SetActive(false); }

        internal static void LateTick()
        {
            try
            {
                if (!Plugin.Enabled.Value || !Plugin.HudEnabled.Value || !Application.isFocused ||
                    !Apocasetter.GameMenu.InGame || Apocasetter.GameMenu.Paused || Apocasetter.InputBlocker.Active ||
                    Time.timeScale <= 0f || !CameraBinding.Resolve() || CameraBinding.SpecialView() ||
                    !CameraBinding.ThirdPersonVisible()) { Hide(); return; }
                if (owner != CameraBinding.Car)
                {
                    owner = CameraBinding.Car; vehicle = null;
                    nextVehicleSearch = nextText = 0f;
                    DrivingReadings.Reset();
                }
                if (vehicle == null && Time.unscaledTime >= nextVehicleSearch)
                {
                    nextVehicleSearch = Time.unscaledTime + 0.5f;
                    vehicle = owner.GetComponent<VehicleController>();
                    if (vehicle == null) vehicle = owner.GetComponentInChildren<VehicleController>(true);
                    nextText = 0f;
                }
                DrivingSnapshot reading;
                if (!DrivingReadings.Capture(vehicle, out reading)) { Hide(); return; }
                EnsureHud();
                if (root == null || inventory == null || !inventory.gameObject.activeInHierarchy || parent == null)
                { Hide(); return; }
                HudLayout layout;
                if (!Place(out layout)) { Hide(); return; }
                root.gameObject.SetActive(true);
                root.anchoredPosition = new Vector2(layout.CenterX, layout.Bottom);
                root.sizeDelta = new Vector2(layout.Width, layout.Height);
                if (Math.Abs(lastScale - layout.Scale) > 0.001f)
                {
                    lastScale = layout.Scale;
                    speed.fontSize = Mathf.Max(12, Mathf.RoundToInt(23f * layout.Scale));
                    rpm.fontSize = gear.fontSize = Mathf.Max(12, Mathf.RoundToInt(23f * layout.Scale));
                    speedLabel.fontSize = rpmLabel.fontSize = gearLabel.fontSize = Mathf.Max(10, Mathf.RoundToInt(13f * layout.Scale));
                }
                rpmFill.rectTransform.anchorMax = new Vector2(reading.RpmFraction, 1f);
                speedDial.Fraction = reading.SpeedKph / 240f;
                rpmDial.Fraction = reading.RpmFraction;
                rpmFill.color = reading.RpmFraction >= 0.9f ? new Color(0.95f, 0.22f, 0.12f, 1f) :
                    new Color(0.91f, 0.68f, 0.28f, 1f);
                if (Time.unscaledTime >= nextText)
                {
                    nextText = Time.unscaledTime + 0.1f;
                    speed.text = Math.Round(reading.SpeedKph).ToString("0", CultureInfo.InvariantCulture);
                    rpm.text = (Math.Round(reading.Rpm / 10f) * 10f).ToString("0", CultureInfo.InvariantCulture);
                    gear.text = DrivingReadings.Gear(vehicle, reading.SelectedGear);
                }
            }
            catch (Exception ex)
            {
                // A partially built or externally changed hierarchy must not
                // leave a permanent non-null root that prevents reconstruction.
                ReleaseHud(); nextSearch = Time.unscaledTime + 1f;
                if (Time.unscaledTime >= nextError)
                { nextError = Time.unscaledTime + 10f; Plugin.Log.LogWarning("Driving HUD suspended: " + ex.Message); }
            }
        }

        private static void EnsureHud()
        {
            bool invalid = root == null || inventory == null || parent == null || inventory.parent != parent;
            if (!invalid && Time.unscaledTime >= nextBindingCheck)
            {
                nextBindingCheck = Time.unscaledTime + 0.5f;
                GameObject current = GameObject.Find("WeaponSlots_UI");
                invalid = current != null && current != inventory.gameObject;
            }
            if (!invalid) return;
            // Also frees our Sprite when Unity has already destroyed the root.
            bool replacing = !System.Object.ReferenceEquals(root, null) || rustFrame != null;
            ReleaseHud();
            if (replacing) nextSearch = 0f;
            if (Time.unscaledTime < nextSearch) return;
            nextSearch = Time.unscaledTime + 1f;
            Create();
            nextBindingCheck = Time.unscaledTime + 0.5f;
        }

        private static void Create()
        {
            GameObject native = GameObject.Find("WeaponSlots_UI");
            if (native == null) return;
            inventory = native.GetComponent<RectTransform>();
            parent = inventory == null ? null : inventory.parent as RectTransform;
            if (parent == null) return;
            // Native font/effects, but no cloned FSMs or inventory interaction.
            Text template = null;
            Transform label = parent.Find("NutsLable");
            if (label != null) template = label.GetComponent<Text>();
            if (template == null)
                foreach (Text text in inventory.GetComponentsInChildren<Text>(true))
                    if (text.font != null) { template = text; break; }
            if (template == null || template.font == null) return;
            root = NewRect("ApocaChaseCamera.DrivingHUD", parent);
            root.anchorMin = root.anchorMax = parent.pivot;
            root.pivot = new Vector2(0.5f, 0f);
            Image backing = root.gameObject.AddComponent<Image>();
            backing.color = new Color(0.07f, 0.05f, 0.035f, 0.78f);
            backing.raycastTarget = false;
            ApplyInventoryStyle(backing);
            speedDial = Dial("Speed dial", 0.045f, 0.335f, false, false);
            rpmDial = Dial("RPM dial", 0.375f, 0.695f, true, false);
            Dial("Gear dial", 0.745f, 0.955f, false, true);
            speedLabel = Label("Speed label", "KM/H", template, 0.02f, 0.35f, 0.80f, 0.97f);
            speed = Label("Speed", "0", template, 0.02f, 0.35f, 0.08f, 0.38f);
            rpmLabel = Label("RPM label", "RPM", template, 0.37f, 0.70f, 0.80f, 0.97f);
            rpm = Label("RPM", "0", template, 0.37f, 0.70f, 0.08f, 0.38f);
            gearLabel = Label("Gear label", "GEAR", template, 0.72f, 0.99f, 0.80f, 0.97f);
            gear = Label("Gear", "N", template, 0.72f, 0.99f, 0.12f, 0.74f);
            RectTransform bar = NewRect("RPM gauge", root);
            Stretch(bar, 0.40f, 0.67f, 0.06f, 0.09f);
            Image track = bar.gameObject.AddComponent<Image>();
            track.color = new Color(0.35f, 0.28f, 0.19f, 0.75f); track.raycastTarget = false;
            RectTransform fill = NewRect("RPM fill", bar);
            Stretch(fill, 0f, 1f, 0f, 1f);
            rpmFill = fill.gameObject.AddComponent<Image>();
            // A sprite-less Image ignores Image.fillAmount. Resize the fill's
            // anchors instead, which works with Unity's plain white UI image.
            rpmFill.raycastTarget = false;
            lastScale = -1f;
            root.gameObject.SetActive(false);
        }

        private static RectTransform NewRect(string name, Transform parentTransform)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parentTransform, false);
            return rect;
        }

        private static void ApplyInventoryStyle(Image backing)
        {
            // Reuse artwork already loaded by the game. Only our Sprite wrappers
            // are owned by the mod; never destroy or redistribute native textures.
            Transform plate = parent.Find("Design_UI/weapons_bg");
            RawImage nativePlate = plate == null ? null : plate.GetComponent<RawImage>();
            Texture2D metal = nativePlate == null ? null : nativePlate.texture as Texture2D;
            if (metal != null)
            {
                // The inventory's bolted plate occupies the middle of its square
                // texture. Crop its transparent margins; sliced borders preserve
                // the rivets instead of stretching them across a wide HUD.
                rustFrame = Sprite.Create(metal, new Rect(0f, metal.height * 0.30f,
                    metal.width, metal.height * 0.45f), new Vector2(0.5f, 0.5f), 200f,
                    0, SpriteMeshType.FullRect, new Vector4(32f, 32f, 32f, 32f));
                backing.sprite = rustFrame; backing.type = Image.Type.Sliced;
                backing.color = new Color(1f, 1f, 1f, 1f);
            }
            // Inventory slot textures change to equipped weapons at runtime.
            // Only the static decorative metal plate is safe to reuse.
            Panel("Speed housing", 0.035f, 0.345f);
            Panel("RPM housing", 0.365f, 0.705f);
            Panel("Gear housing", 0.725f, 0.965f);
        }

        private static void Panel(string name, float left, float right)
        {
            RectTransform panel = NewRect(name, root); Stretch(panel, left, right, 0.08f, 0.92f);
            Image well = panel.gameObject.AddComponent<Image>();
            well.color = new Color(0.025f, 0.020f, 0.015f, 0.96f); well.raycastTarget = false;
            Outline edge = panel.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.46f, 0.29f, 0.15f, 1f);
            edge.effectDistance = new Vector2(1.5f, -1.5f); edge.useGraphicAlpha = false;
        }

        private static GaugeFace Dial(string name, float left, float right, bool tachometer, bool gearFace)
        {
            RectTransform rect = NewRect(name, root); Stretch(rect, left, right, 0.16f, 0.78f);
            GaugeFace dial = rect.gameObject.AddComponent<GaugeFace>();
            dial.Tachometer = tachometer; dial.GearFace = gearFace; dial.raycastTarget = false;
            return dial;
        }

        private static void Stretch(RectTransform rect, float left, float right, float bottom, float top)
        {
            rect.anchorMin = new Vector2(left, bottom); rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Text Label(string name, string initial, Text template, float left, float right, float bottom, float top)
        {
            RectTransform rect = NewRect(name, root); Stretch(rect, left, right, bottom, top);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = template.font; text.fontStyle = template.fontStyle;
            text.color = template.color; text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = false; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = initial;
            foreach (Shadow original in template.GetComponents<Shadow>())
            {
                Shadow effect = original is Outline ? (Shadow)rect.gameObject.AddComponent<Outline>() : rect.gameObject.AddComponent<Shadow>();
                effect.effectColor = original.effectColor; effect.effectDistance = original.effectDistance;
                effect.useGraphicAlpha = original.useGraphicAlpha;
            }
            return text;
        }

        private static bool Place(out HudLayout layout)
        {
            layout = new HudLayout();
            // Use visible inventory graphics, including Apocapocket's extra
            // slots, instead of assuming a fixed screen resolution or 3 slots.
            if (inventoryChildren != inventory.childCount || Time.unscaledTime >= nextGraphicsRefresh)
            {
                inventoryGraphics.Clear();
                inventory.GetComponentsInChildren<Graphic>(true, inventoryGraphics);
                inventoryChildren = inventory.childCount;
                nextGraphicsRefresh = Time.unscaledTime + 0.5f;
                hasBounds = false;
                nextBoundsRefresh = 0f;
            }
            if (Time.unscaledTime >= nextBoundsRefresh)
            {
                boundsLeft = boundsBottom = float.PositiveInfinity;
                boundsRight = boundsTop = float.NegativeInfinity;
                foreach (Graphic graphic in inventoryGraphics)
                {
                    if (graphic == null || !graphic.gameObject.activeInHierarchy || !graphic.enabled ||
                        graphic.color.a <= 0.01f || !graphic.transform.IsChildOf(inventory)) continue;
                    Text text = graphic as Text;
                    if (text != null && String.IsNullOrEmpty(text.text)) continue;
                    graphic.rectTransform.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Vector3 local = inventory.InverseTransformPoint(corner);
                        boundsLeft = Mathf.Min(boundsLeft, local.x); boundsRight = Mathf.Max(boundsRight, local.x);
                        boundsBottom = Mathf.Min(boundsBottom, local.y); boundsTop = Mathf.Max(boundsTop, local.y);
                    }
                }
                hasBounds = !float.IsInfinity(boundsLeft) && boundsRight > boundsLeft;
                nextBoundsRefresh = Time.unscaledTime + 0.1f;
            }
            if (!hasBounds) return false;
            // Bounds live in inventory space. Only four corners follow its live
            // pose each frame; the full graphic geometry is refreshed at 10 Hz.
            corners[0] = new Vector3(boundsLeft, boundsBottom, 0f);
            corners[1] = new Vector3(boundsLeft, boundsTop, 0f);
            corners[2] = new Vector3(boundsRight, boundsTop, 0f);
            corners[3] = new Vector3(boundsRight, boundsBottom, 0f);
            float left = float.PositiveInfinity, right = float.NegativeInfinity, top = float.NegativeInfinity;
            foreach (Vector3 corner in corners)
            {
                Vector3 local = parent.InverseTransformPoint(inventory.TransformPoint(corner));
                left = Mathf.Min(left, local.x); right = Mathf.Max(right, local.x); top = Mathf.Max(top, local.y);
            }
            Rect canvas = parent.rect;
            layout = HudMath.Layout(canvas.xMin, canvas.xMax, canvas.yMin, canvas.yMax,
                left, right, top, Plugin.HudScale.Value, Plugin.HudGap.Value);
            return true;
        }
    }
}
