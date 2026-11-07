using System;
using System.Runtime.InteropServices;

namespace ScreenCare
{
    // XInput for Xbox/Steam Input; WinMM for legacy HID controllers.
    // Merely being connected is not activity. Held controls do count, and small
    // analog drift is filtered. Disconnected slots are probed only every 2 seconds.
    // Any device whose engaged controls stay identical for StuckFilter.LimitMs is
    // treated as stuck (driver/report misreads), not as a person holding them.
    internal sealed class GamepadMonitor
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct XboxState
        {
            internal uint Packet;
            internal ushort Buttons;
            internal byte LeftTrigger, RightTrigger;
            internal short LX, LY, RX, RY;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct JoyState
        {
            internal uint Size, Flags, X, Y, Z, R, U, V, Buttons, ButtonNumber, Pov, Reserved1, Reserved2;
            internal uint[] Axes { get { return new[] { X, Y, Z, R, U, V }; } }
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct JoyCaps
        {
            internal ushort Manufacturer, Product;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string Name;
            internal uint XMin, XMax, YMin, YMax, ZMin, ZMax, Buttons, PeriodMin, PeriodMax;
            internal uint RMin, RMax, UMin, UMax, VMin, VMax, Caps, MaxAxes, Axes, MaxButtons;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string RegistryKey;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] internal string Driver;
            internal uint[] Minimum { get { return new[] { XMin, YMin, ZMin, RMin, UMin, VMin }; } }
            internal uint[] Maximum { get { return new[] { XMax, YMax, ZMax, RMax, UMax, VMax }; } }
        }
        [DllImport("xinput1_4.dll")] private static extern uint XInputGetState(uint index, out XboxState state);
        [DllImport("winmm.dll")] private static extern uint joyGetNumDevs();
        [DllImport("winmm.dll")] private static extern uint joyGetPosEx(uint index, ref JoyState state);
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] private static extern uint joyGetDevCaps(UIntPtr index, out JoyCaps caps, uint size);
        private readonly double[] xboxRetry = new double[4];
        private readonly double[] joyRetry = new double[16];
        private readonly LegacyActivity[] legacy = new LegacyActivity[16];
        private readonly StuckFilter[] xboxStuck = new StuckFilter[4];
        private readonly StuckFilter[] joyStuck = new StuckFilter[16];

        internal static bool XboxActive(XboxState s) { return XboxControls(s) != 0; }
        internal static long XboxControls(XboxState s)
        {
            long controls = s.Buttons & 0xF3FF;
            if (s.LeftTrigger > 30) controls |= 1L << 16;
            if (s.RightTrigger > 30) controls |= 1L << 17;
            controls |= Direction(s.LX, 7849, 18) | Direction(s.LY, 7849, 20)
                | Direction(s.RX, 8689, 22) | Direction(s.RY, 8689, 24);
            return controls;
        }
        private static long Direction(int value, int deadzone, int bit)
        { return value > deadzone ? 1L << bit : value < -deadzone ? 1L << (bit + 1) : 0; }
        internal bool Poll(double now)
        {
            bool active = false;
            uint ms = unchecked((uint)(long)(now * 1000));
            for (uint i = 0; i < 4; i++)
            {
                if (now < xboxRetry[i]) continue;
                XboxState state;
                if (XInputGetState(i, out state) == 0)
                {
                    if (xboxStuck[i] == null) xboxStuck[i] = new StuckFilter();
                    active |= xboxStuck[i].Observe(XboxControls(state), ms);
                }
                else { xboxStuck[i] = null; xboxRetry[i] = now + 2; }
            }
            uint count = Math.Min(joyGetNumDevs(), 16);
            for (uint i = 0; i < count; i++)
            {
                if (now < joyRetry[i]) continue;
                JoyState state = NewJoyState();
                if (joyGetPosEx(i, ref state) != 0)
                { legacy[i] = null; joyStuck[i] = null; joyRetry[i] = now + 2; continue; }
                if (legacy[i] == null)
                {
                    JoyCaps caps;
                    if (joyGetDevCaps(new UIntPtr(i), out caps, (uint)Marshal.SizeOf(typeof(JoyCaps))) != 0)
                    { joyRetry[i] = now + 2; continue; }
                    legacy[i] = new LegacyActivity(caps, state); joyStuck[i] = new StuckFilter();
                }
                active |= joyStuck[i].Observe(legacy[i].Controls(state), ms);
            }
            return active;
        }
        internal static JoyState NewJoyState()
        { return new JoyState { Size = (uint)Marshal.SizeOf(typeof(JoyState)), Flags = 0xFF, Pov = 0xFFFF }; }

        internal sealed class LegacyActivity
        {
            private readonly double[] rest = new double[6], range = new double[6];
            private readonly bool hasPov;
            private readonly bool rawInputOnly;
            internal LegacyActivity(JoyCaps caps, JoyState first)
            {
                // Raw Input owns supported Sony/Nintendo devices. WinMM can
                // misread reports or change neutral axes after initialization;
                // localized generic names cannot reliably select their layout.
                rawInputOnly = SwitchInput.IsDevice(caps.Manufacturer, caps.Product)
                    || DualSenseInput.IsDevice(caps.Manufacturer, caps.Product);
                uint[] min = caps.Minimum, max = caps.Maximum, axes = first.Axes;
                hasPov = (caps.Caps & 16) != 0;
                // DualSense / DualShock expose X,Y,Z,R sticks and U,V triggers via WinMM.
                bool playstation = (caps.Name ?? "").IndexOf("Wireless Controller", StringComparison.OrdinalIgnoreCase) >= 0
                    || (caps.Name ?? "").IndexOf("DualSense", StringComparison.OrdinalIgnoreCase) >= 0
                    || (caps.Name ?? "").IndexOf("DualShock", StringComparison.OrdinalIgnoreCase) >= 0;
                for (int a = 0; a < 6; a++)
                {
                    bool supported = a < 2 || (caps.Caps & (1u << (a - 2))) != 0;
                    range[a] = supported ? (double)max[a] - min[a] : 0;
                    rest[a] = a < 2 || (playstation && a < 4) ? (min[a] + (double)max[a]) / 2
                        : playstation ? min[a] : axes[a];
                }
            }
            internal bool Observe(JoyState s) { return Controls(s) != 0; }
            internal long Controls(JoyState s)
            {
                if (rawInputOnly) return 0;
                long controls = s.Buttons;
                if (hasPov && s.Pov != 0xFFFF) controls |= (long)(s.Pov / 4500 % 8 + 1) << 32;
                uint[] axes = s.Axes;
                for (int a = 0; a < 6; a++)
                {
                    if (range[a] <= 0) continue;
                    double offset = axes[a] - rest[a];
                    if (offset > range[a] * 0.12) controls |= 1L << (36 + 2 * a);
                    else if (offset < -range[a] * 0.12) controls |= 1L << (37 + 2 * a);
                }
                return controls;
            }
        }

        // Read-only diagnostic: never sends input or power commands.
        internal static string DescribeConnected()
        {
            string result = "";
            for (uint i = 0; i < Math.Min(joyGetNumDevs(), 16); i++)
            {
                JoyState state = NewJoyState(); JoyCaps caps;
                if (joyGetPosEx(i, ref state) == 0 && joyGetDevCaps(new UIntPtr(i), out caps, (uint)Marshal.SizeOf(typeof(JoyCaps))) == 0)
                    result += "WinMM " + i + ": " + caps.Name + "; axes=" + caps.Axes + "; caps=" + caps.Caps
                        + "; id=" + caps.Manufacturer.ToString("X4") + ":" + caps.Product.ToString("X4")
                        + "; buttons=" + state.Buttons + "; pov=" + state.Pov
                        + "; active=" + new LegacyActivity(caps, state).Observe(state)
                        + "; values=" + String.Join(",", state.Axes) + "; ranges=" + String.Join(",", caps.Maximum) + Environment.NewLine;
            }
            for (uint i = 0; i < 4; i++) { XboxState s; if (XInputGetState(i, out s) == 0) result += "XInput " + i + Environment.NewLine; }
            return result.Length == 0 ? "No controller exposed to this process." : result;
        }
    }

    // Shared by every controller path. A person changes what they press; drivers
    // that misread reports or leave a control latched keep the same set forever.
    internal sealed class StuckFilter
    {
        internal const uint LimitMs = 300000;
        private long controls;
        private uint since;
        internal bool Observe(long engaged, uint now)
        {
            if (engaged != controls) { controls = engaged; since = now; }
            return IsActive(now);
        }
        // Tick differences are unsigned, so Environment.TickCount wrap is safe.
        internal bool IsActive(uint now) { return controls != 0 && unchecked(now - since) < LimitMs; }
    }
}
