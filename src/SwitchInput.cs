using System;

namespace ScreenCare
{
    internal static class SwitchInput
    {
        internal static bool IsDevice(uint vendor, uint product)
        { return vendor == 0x057E && product == 0x2009; }

        // Pro Controller layouts: SDL HIDAPI Switch (see docs/CONTROLES.md).
        // Read only: never change report mode or interfere with Steam/SDL.
        internal static bool TryActivity(byte[] report, out bool active)
        {
            active = false;
            if (report == null || report.Length < 12) return false;
            if (report[0] == 0x3F)
            {
                active = report[1] != 0 || (report[2] & 0xBF) != 0 || report[3] < 8;
                for (int i = 4; i < 12; i += 2)
                    active |= Math.Abs((report[i] | report[i + 1] << 8) - 32768) > 8000;
                return true;
            }
            if ((report[0] != 0x30 && report[0] != 0x31 && report[0] != 0x21)
                || report.Length < (report[0] == 0x21 ? 15 : 49)) return false;
            active = (report[3] & 0xCF) != 0 || (report[4] & 0x3F) != 0 || (report[5] & 0xCF) != 0;
            for (int i = 6; i <= 9; i += 3)
            {
                int x = report[i] | (report[i + 1] & 15) << 8;
                int y = report[i + 1] >> 4 | report[i + 2] << 4;
                active |= Math.Abs(x - 2048) > 500 || Math.Abs(y - 2048) > 500;
            }
            return true;
        }
    }

    internal sealed class SwitchActivity
    {
        private bool held, simple;
        private uint received;
        internal void Observe(byte reportId, bool active, uint now)
        { held = active; simple = reportId == 0x3F; received = now; }
        // Simple reports can be sent only on change; preserve a held command
        // until release/removal. Streaming reports expire if the stream stops.
        internal bool IsActive(uint now)
        { return held && (simple || unchecked(now - received) < 1000); }
    }
}
