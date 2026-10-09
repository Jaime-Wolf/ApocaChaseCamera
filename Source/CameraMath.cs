using System;

namespace ApocaChaseCamera
{
    // Pure math, kept independent of Unity so camera behavior can be simulated.
    internal static class CameraMath
    {
        internal static float Clamp(float value, float min, float max)
        { return Math.Max(min, Math.Min(max, value)); }

        internal static float Follow(float value, float target, float seconds, float dt)
        {
            if (seconds <= 0f) return target;
            if (dt <= 0f) return value;
            return target + (value - target) * (float)Math.Exp(-dt / seconds);
        }

        internal static float FollowHeight(float value, float target, float seconds, float maxLag, float dt)
        {
            // Filter small suspension movements, but never leave the camera
            // metres below/above its vehicle during a sustained climb or drop.
            float followed = Follow(value, target, seconds, dt);
            return Clamp(followed, target - maxLag, target + maxLag);
        }

        internal static float Wrap(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            if (angle < -180f) angle += 360f;
            return angle;
        }

        internal static float FollowAngle(float value, float target, float seconds, float dt)
        { return Wrap(value + Follow(0f, Wrap(target - value), seconds, dt)); }

        internal static float RecenterAngle(float value, float seconds, float blend, float dt)
        {
            float change = Follow(0f, Wrap(-value), seconds, dt * blend);
            return Wrap(value + Clamp(change, -120f * dt, 120f * dt));
        }

        // Obstructions shorten the camera immediately. Returning to the chosen
        // distance is damped, so posts and uneven terrain do not cause zoom jolts.
        internal static float SafeDistance(float current, float clear, float seconds, float dt)
        { return clear < current ? clear : Follow(current, clear, seconds, dt); }

        internal static bool NeedsReset(float displacement, float elapsed)
        { return displacement > 40f || elapsed > 0.5f || elapsed < 0f; }
    }
}
