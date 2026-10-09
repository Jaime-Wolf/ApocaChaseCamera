using NWH.VehiclePhysics2;

namespace ApocaChaseCamera
{
    internal struct DrivingSnapshot
    {
        internal float SpeedKph, Rpm, RpmFraction;
        internal int SelectedGear;
    }

    internal static class DrivingReadings
    {
        private static VehicleController gearVehicle;
        private static int lastGear, lastForwardCount;
        private static string gearText;

        internal static void Reset()
        { gearVehicle = null; gearText = null; lastGear = lastForwardCount = 0; }

        internal static bool Capture(VehicleController vehicle, out DrivingSnapshot readings)
        {
            readings = new DrivingSnapshot();
            if (vehicle == null || vehicle.powertrain == null) return false;
            var transmission = vehicle.powertrain.transmission;
            if (transmission == null || transmission.gears == null) return false;
            var engine = vehicle.powertrain.engine;
            float rpm = engine == null ? 0f : HudMath.Reading(engine.OutputRPM);
            readings.SpeedKph = HudMath.Reading(vehicle.SpeedSigned) * 3.6f;
            readings.Rpm = rpm;
            readings.RpmFraction = engine == null ? 0f : HudMath.RpmFraction(rpm, engine.revLimiterRPM);
            readings.SelectedGear = transmission.Gear;
            return true;
        }

        // The caller's text cadence bounds the scan. Read the ratios again even
        // when list length is unchanged: a gearbox mod can replace a ratio in place.
        internal static string Gear(VehicleController vehicle, int selected)
        {
            int forwards = 0;
            if (vehicle != null && vehicle.powertrain != null && vehicle.powertrain.transmission != null &&
                vehicle.powertrain.transmission.gears != null)
                foreach (float ratio in vehicle.powertrain.transmission.gears)
                    if (ratio > 0f && !float.IsInfinity(ratio)) forwards++;
            if (gearText == null || gearVehicle != vehicle || lastGear != selected || lastForwardCount != forwards)
            {
                gearVehicle = vehicle; lastGear = selected; lastForwardCount = forwards;
                gearText = HudMath.Gear(selected, forwards);
            }
            return gearText;
        }
    }
}
