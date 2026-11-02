using System;

namespace ScreenCare
{
    internal static class DualSenseInput
    {
        internal static bool IsDevice(uint vendor, uint product)
        { return vendor == 0x054C && (product == 0x0CE6 || product == 0x0DF2); }

        // Protocol field layout verified against Sony's Linux driver and SDL's
        // PS5 driver (links in docs/CONTROLES.md). No sensor/sequence/battery byte
        // is treated as user input. Bluetooth simple and enhanced modes differ.
        internal static bool TryActivity(byte[] report, out bool active)
        {
            active = false;
            if (report == null || report.Length < 10) return false;
            bool simple = report[0] == 1 && (report.Length == 10 || report.Length == 78);
            int start;
            if (simple || (report[0] == 1 && report.Length == 64)) start = 1;
            else if (report[0] == 0x31 && report.Length == 78) start = 2;
            else return false;
            for (int i = 0; i < 4; i++)
                active |= Math.Abs(report[start + i] - 127.5) > 24;
            int buttons = start + (simple ? 4 : 7);
            int triggers = start + (simple ? 7 : 4);
            active |= report[triggers] > 30 || report[triggers + 1] > 30;
            active |= (report[buttons] & 0x0F) < 8 || (report[buttons] & 0xF0) != 0 || report[buttons + 1] != 0;
            // Simple mode packs a changing counter in the upper six bits.
            active |= (report[buttons + 2] & (simple ? 3 : 7)) != 0;
            return true;
        }
    }
}
