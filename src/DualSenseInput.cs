using System;

namespace ScreenCare
{
    internal static class DualSenseInput
    {
        // Bits 0-7 are stick directions and 8-9 the triggers; the rest are buttons.
        internal const long Analog = 0x3FF;

        internal static bool IsDevice(uint vendor, uint product)
        { return vendor == 0x054C && (product == 0x0CE6 || product == 0x0DF2); }

        internal static bool TryActivity(byte[] report, out bool active)
        {
            long controls; bool parsed = TryControls(report, out controls);
            active = controls != 0; return parsed;
        }
        internal static bool TryControls(byte[] report, out long controls) { return TryControls(report, 0, out controls); }

        // Protocol field layout verified against Sony's Linux driver and SDL's
        // PS5 driver (links in docs/CONTROLES.md). No sensor/sequence/battery byte
        // is treated as user input. Bluetooth simple and enhanced modes differ.
        // Each engaged control sets its own bit, so a frozen set can be detected;
        // `previous` (the last result) gives sticks and triggers their hysteresis.
        internal static bool TryControls(byte[] report, long previous, out long controls)
        {
            controls = 0;
            if (report == null || report.Length < 10) return false;
            bool simple = report[0] == 1 && (report.Length == 10 || report.Length == 78);
            int start;
            if (simple || (report[0] == 1 && report.Length == 64)) start = 1;
            else if (report[0] == 0x31 && report.Length == 78) start = 2;
            else return false;
            for (int i = 0; i < 4; i++)
                controls |= DeadZone.Axis(report[start + i] - 127.5, 24, 2 * i, previous);
            int buttons = start + (simple ? 4 : 7);
            int triggers = start + (simple ? 7 : 4);
            controls |= DeadZone.Trigger(report[triggers], 30, 8, previous) | DeadZone.Trigger(report[triggers + 1], 30, 9, previous);
            int dpad = report[buttons] & 0x0F;
            if (dpad < 8) controls |= (long)(dpad + 1) << 10;
            controls |= (long)(report[buttons] & 0xF0) << 10;
            controls |= (long)report[buttons + 1] << 18;
            // Simple mode packs a changing counter in the upper six bits.
            controls |= (long)(report[buttons + 2] & (simple ? 3 : 7)) << 26;
            return true;
        }
    }
}
