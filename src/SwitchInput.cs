using System;

namespace ScreenCare
{
    internal static class SwitchInput
    {
        // Both layouts put the stick directions in bits 24-31; lower bits are buttons.
        internal const long Analog = 0xFFL << 24;

        internal static bool IsDevice(uint vendor, uint product)
        { return vendor == 0x057E && product == 0x2009; }

        internal static bool TryActivity(byte[] report, out bool active)
        {
            long controls; bool parsed = TryControls(report, out controls);
            active = controls != 0; return parsed;
        }
        internal static bool TryControls(byte[] report, out long controls) { return TryControls(report, 0, out controls); }

        // Pro Controller layouts: SDL HIDAPI Switch (see docs/CONTROLES.md).
        // Read only: never change report mode or interfere with Steam/SDL.
        // `previous` (the last result) gives the sticks their hysteresis.
        internal static bool TryControls(byte[] report, long previous, out long controls)
        {
            controls = 0;
            if (report == null || report.Length < 12) return false;
            if (report[0] == 0x3F)
            {
                controls = report[1] | (report[2] & 0xBF) << 8;
                if (report[3] < 8) controls |= (long)(report[3] + 1) << 16;
                for (int i = 4, bit = 24; i < 12; i += 2, bit += 2)
                    controls |= DeadZone.Axis((report[i] | report[i + 1] << 8) - 32768, 8000, bit, previous);
                return true;
            }
            if ((report[0] != 0x30 && report[0] != 0x31 && report[0] != 0x21)
                || report.Length < (report[0] == 0x21 ? 15 : 49)) return false;
            controls = (report[3] & 0xCF) | (report[4] & 0x3F) << 8 | (report[5] & 0xCF) << 16;
            for (int i = 6, bit = 24; i <= 9; i += 3, bit += 4)
            {
                int x = report[i] | (report[i + 1] & 15) << 8;
                int y = report[i + 1] >> 4 | report[i + 2] << 4;
                controls |= DeadZone.Axis(x - 2048, 500, bit, previous) | DeadZone.Axis(y - 2048, 500, bit + 2, previous);
            }
            return true;
        }
    }

    internal sealed class SwitchActivity
    {
        private bool simple;
        private uint received;
        private readonly StuckFilter controls = new StuckFilter(SwitchInput.Analog, false);
        internal long Controls { get { return controls.Controls; } }
        internal void Observe(byte reportId, bool active, uint now) { Observe(reportId, active ? 1L : 0, now); }
        internal void Observe(byte reportId, long engaged, uint now)
        { controls.Observe(engaged, now); simple = reportId == 0x3F; received = now; }
        // Simple reports can be sent only on change; preserve a held command
        // until release/removal. Streaming reports expire if the stream stops.
        // StuckFilter stops counting frozen or repeating commands either way.
        internal bool IsActive(uint now)
        { return controls.IsActive(now) && (simple || unchecked(now - received) < 1000); }
    }
}
