using System;
using System.Runtime.InteropServices;

namespace ScreenCare
{
    // XInput for Xbox/Steam Input; WinMM for legacy HID controllers.
    // Merely being connected is not activity. Held controls do count, and small
    // analog drift is filtered. StuckFilter decides what still counts as a person:
    // latched, flapping and garbage readings stop counting after a few minutes.
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
        [StructLayout(LayoutKind.Sequential)] private struct RawDeviceEntry { internal IntPtr Device; internal uint Type; }
        [DllImport("user32.dll")] private static extern uint GetRawInputDeviceList([Out] RawDeviceEntry[] list, ref uint count, uint size);
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] private static extern bool GetProcessHandleCount(IntPtr process, out uint count);
        internal const long XboxAnalog = 0x3FFL << 16, LegacyAnalog = 0xFFFL << 36;
        private readonly SlotProbe probe = new SlotProbe();
        private readonly double[] xboxRetry = new double[4];
        private readonly double[] joyRetry = new double[16];
        private readonly LegacyActivity[] legacy = new LegacyActivity[16];
        private readonly StuckFilter[] xboxStuck = new StuckFilter[4];
        private readonly StuckFilter[] joyStuck = new StuckFilter[16];

        internal static bool XboxActive(XboxState s) { return XboxControls(s) != 0; }
        internal static long XboxControls(XboxState s) { return XboxControls(s, 0); }
        internal static long XboxControls(XboxState s, long previous)
        {
            long controls = s.Buttons & 0xF3FF;
            controls |= DeadZone.Trigger(s.LeftTrigger, 30, 16, previous) | DeadZone.Trigger(s.RightTrigger, 30, 17, previous)
                | DeadZone.Axis(s.LX, 7849, 18, previous) | DeadZone.Axis(s.LY, 7849, 20, previous)
                | DeadZone.Axis(s.RX, 8689, 22, previous) | DeadZone.Axis(s.RY, 8689, 24, previous);
            return controls;
        }
        // HID device handles known to Raw Input: cheap, and unlike a WinMM query it leaks nothing.
        internal static long DeviceSignature()
        {
            uint count = 0, size = (uint)Marshal.SizeOf(typeof(RawDeviceEntry));
            if (GetRawInputDeviceList(null, ref count, size) == UInt32.MaxValue || count == 0 || count > 4096) return 0;
            var list = new RawDeviceEntry[count];
            uint read = GetRawInputDeviceList(list, ref count, size);
            if (read == UInt32.MaxValue) return -1;
            long signature = 17;
            for (uint i = 0; i < read && i < list.Length; i++)
                if (list[i].Type == 2) signature = unchecked(signature * 31 + list[i].Device.ToInt64());
            return signature;
        }
        internal static uint HandleCount()
        { uint count; return GetProcessHandleCount(GetCurrentProcess(), out count) ? count : 0; }
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
                    if (xboxStuck[i] == null) xboxStuck[i] = new StuckFilter(XboxAnalog, true);
                    active |= xboxStuck[i].Observe(XboxControls(state, xboxStuck[i].Controls), ms);
                }
                else { xboxStuck[i] = null; xboxRetry[i] = now + 2; }
            }
            bool probeEmpty = probe.Allow(now, DeviceSignature);
            uint count = Math.Min(joyGetNumDevs(), 16);
            for (uint i = 0; i < count; i++)
            {
                if ((legacy[i] == null && !probeEmpty) || now < joyRetry[i]) continue;
                JoyState state = NewJoyState();
                if (joyGetPosEx(i, ref state) != 0)
                { legacy[i] = null; joyStuck[i] = null; joyRetry[i] = now + 2; continue; }
                if (legacy[i] == null)
                {
                    JoyCaps caps;
                    if (joyGetDevCaps(new UIntPtr(i), out caps, (uint)Marshal.SizeOf(typeof(JoyCaps))) != 0)
                    { joyRetry[i] = now + 2; continue; }
                    legacy[i] = new LegacyActivity(caps, state); joyStuck[i] = new StuckFilter(LegacyAnalog, true);
                }
                active |= joyStuck[i].Observe(legacy[i].Controls(state, joyStuck[i].Controls), ms);
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
            internal long Controls(JoyState s) { return Controls(s, 0); }
            internal long Controls(JoyState s, long previous)
            {
                if (rawInputOnly) return 0;
                long controls = s.Buttons;
                if (hasPov && s.Pov != 0xFFFF) controls |= (long)(s.Pov / 4500 % 8 + 1) << 32;
                uint[] axes = s.Axes;
                for (int a = 0; a < 6; a++)
                    if (range[a] > 0) controls |= DeadZone.Axis(axes[a] - rest[a], range[a] * 0.12, 36 + 2 * a, previous);
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

    internal static class DeadZone
    {
        // Engage past the dead zone, release only a quarter inside it: sensor noise
        // at the edge cannot keep toggling a control. `previous` is the last result.
        internal static long Axis(double offset, double deadZone, int bit, long previous)
        {
            long positive = 1L << bit, negative = 1L << (bit + 1);
            if (offset > ((previous & positive) != 0 ? deadZone * 0.75 : deadZone)) return positive;
            if (offset < -((previous & negative) != 0 ? deadZone * 0.75 : deadZone)) return negative;
            return 0;
        }
        internal static long Trigger(double value, double threshold, int bit, long previous)
        { long mask = 1L << bit; return value > ((previous & mask) != 0 ? threshold * 0.75 : threshold) ? mask : 0; }
    }

    // Every WinMM query of an empty joystick slot makes DirectInput rebuild its
    // device list, and Windows leaks registry handles on each rebuild (measured:
    // about 20 per pass, 1.4 million after two days). It is also the code path of
    // the heap corruption seen before 1.5.6. Empty slots are therefore probed only
    // for a few seconds after the set of HID devices changes, plus a slow safety net.
    internal sealed class SlotProbe
    {
        internal const double Window = 6, Fallback = 300;
        private long signature;
        private bool known;
        private double until, fallbackAt = Fallback, nextCheck;
        internal bool Allow(double now, Func<long> devices)
        {
            if (now >= nextCheck)
            {
                nextCheck = now + 1; long current = devices();
                if (!known || current != signature) { known = true; signature = current; until = now + Window; }
            }
            if (now >= fallbackAt) { fallbackAt = now + Fallback; return true; }
            return now < until;
        }
    }

    // Shared by every controller path: decides whether engaged controls still look
    // like a person. Buttons and directional pads are digital and always count when
    // they change. Analog controls only refresh the countdown when a new position
    // lasts DebounceMs (one-sample glitches do not). One analog control going on
    // and off by itself, with nothing else engaged, is what a worn stick does at
    // rest: it stops counting after LimitMs, as does any set left unchanged that
    // long. Polled sources whose reading changes on nearly every sample for two
    // minutes are garbage, not fingers.
    internal sealed class StuckFilter
    {
        internal const uint LimitMs = 300000, DebounceMs = 150, NoiseWindowMs = 30000;
        internal const int NoiseWindows = 4, NoiseSamples = 250;
        private readonly long analog;
        private readonly bool polled;
        private bool started, pending, noisy;
        private long controls, confirmed, patternMask;
        private uint since, pendingSince, patternStart, windowStart;
        private int samples, changes, hotWindows;
        internal StuckFilter() : this(0, false) { }
        internal StuckFilter(long analogMask, bool polledSource) { analog = analogMask; polled = polledSource; }
        internal long Controls { get { return controls; } }
        internal bool Noisy { get { return noisy; } }
        internal bool Observe(long engaged, uint now)
        {
            if (!started) { started = true; controls = engaged; confirmed = engaged & analog; since = windowStart = now; }
            else if (engaged != controls)
            {
                long changed = engaged ^ controls; controls = engaged; changes++;
                if ((changed & ~analog) != 0) since = now;
                if ((changed & analog) != 0) { pending = true; pendingSince = now; }
            }
            samples++;
            if (polled && unchecked(now - windowStart) >= NoiseWindowMs)
            {
                bool hot = samples >= NoiseSamples && changes * 10L >= samples * 9L;
                hotWindows = hot ? Math.Min(hotWindows + 1, NoiseWindows) : 0;
                noisy = hotWindows >= NoiseWindows;
                windowStart = now; samples = changes = 0;
            }
            return IsActive(now);
        }
        // Tick differences are unsigned, so Environment.TickCount wrap is safe.
        internal bool IsActive(uint now)
        {
            if (pending && unchecked(now - pendingSince) >= DebounceMs)
            {
                pending = false;
                long state = controls & analog, mask = state ^ confirmed;
                if (mask != 0)
                {
                    confirmed = state;
                    bool lone = (mask & (mask - 1)) == 0 && (controls & ~mask) == 0;
                    if (!lone) patternMask = 0;
                    else if (mask != patternMask) { patternMask = mask; patternStart = now; }
                    if (!lone || unchecked(now - patternStart) < LimitMs) since = now;
                }
            }
            // The lone repeating control has had its five minutes; it stays ignored
            // for as long as nothing else on the controller is engaged.
            bool worn = patternMask != 0 && (controls & ~patternMask) == 0 && unchecked(now - patternStart) >= LimitMs;
            return controls != 0 && !noisy && !worn && unchecked(now - since) < LimitMs;
        }
    }
}
