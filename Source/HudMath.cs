using System;
using System.Globalization;

namespace ApocaChaseCamera
{
    internal struct HudLayout
    {
        internal float CenterX, Bottom, Width, Height, Scale;
    }

    internal static class HudMath
    {
        internal static float Reading(float value)
        { return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Abs(value); }

        internal static string Gear(int selected, int forwardCount)
        {
            string name = selected == 0 ? "N" : selected < 0 ?
                (selected == -1 ? "R" : "R" + (-(long)selected).ToString(CultureInfo.InvariantCulture)) :
                selected.ToString(CultureInfo.InvariantCulture);
            return forwardCount > 0 ? name + " / " + forwardCount.ToString(CultureInfo.InvariantCulture) : name;
        }

        internal static float RpmFraction(float rpm, float limit)
        { return limit <= 0f || float.IsNaN(limit) || float.IsInfinity(limit) ? 0f : CameraMath.Clamp(Reading(rpm) / limit, 0f, 1f); }

        internal static HudLayout Layout(float canvasLeft, float canvasRight, float canvasBottom, float canvasTop,
            float inventoryLeft, float inventoryRight, float inventoryTop, float scale, float gap)
        {
            float width = Math.Min(440f * scale, Math.Max(1f, canvasRight - canvasLeft - 24f));
            float actualScale = Math.Min(width / 440f, Math.Max(0.01f, (canvasTop - canvasBottom - 24f) / 100f));
            width = 440f * actualScale;
            float height = 100f * actualScale;
            return new HudLayout {
                CenterX = CameraMath.Clamp((inventoryLeft + inventoryRight) * 0.5f,
                    canvasLeft + 12f + width * 0.5f, canvasRight - 12f - width * 0.5f),
                Bottom = CameraMath.Clamp(inventoryTop + gap, canvasBottom + 12f, canvasTop - 12f - height),
                Width = width, Height = height, Scale = actualScale
            };
        }
    }
}
