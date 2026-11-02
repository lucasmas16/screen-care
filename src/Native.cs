using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenCare
{
    internal static class Native
    {
        internal const int WM_INPUT = 0x00FF, WM_HOTKEY = 0x0312, WM_POWERBROADCAST = 0x0218, WM_WTSSESSION_CHANGE = 0x02B1;
        internal const int ErrorHotKeyAlreadyRegistered = 1409;
        internal static readonly Guid DisplayState = new Guid("2B84C20E-AD23-4DDF-93DB-05FFBD7EFCA5");
        [StructLayout(LayoutKind.Sequential)] internal struct RawDevice { internal ushort Page, Usage; internal uint Flags; internal IntPtr Target; }
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterRawInputDevices(RawDevice[] devices, uint count, uint size);
        [DllImport("user32.dll", SetLastError = true)] internal static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
        [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", SetLastError = true)] internal static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, IntPtr data, ref uint size);
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr RegisterPowerSettingNotification(IntPtr hwnd, ref Guid setting, uint flags);
        [DllImport("user32.dll")] internal static extern bool UnregisterPowerSettingNotification(IntPtr handle);
        [DllImport("wtsapi32.dll", SetLastError = true)] internal static extern bool WTSRegisterSessionNotification(IntPtr hwnd, uint flags);
        [DllImport("wtsapi32.dll")] internal static extern bool WTSUnRegisterSessionNotification(IntPtr hwnd);
        [DllImport("kernel32.dll")] internal static extern uint SetThreadExecutionState(uint flags);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
        [DllImport("user32.dll")] internal static extern bool CloseDesktop(IntPtr desktop);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("shcore.dll")] internal static extern int SetProcessDpiAwareness(int value);
        [StructLayout(LayoutKind.Sequential)] private struct LastInput { internal uint Size, Time; }
        [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LastInput info);
        internal static bool TryLastInput(out uint time)
        {
            var info = new LastInput { Size = 8 };
            bool success = GetLastInputInfo(ref info); time = info.Time; return success;
        }

        internal static bool InteractiveDesktop()
        {
            IntPtr desktop = OpenInputDesktop(0, false, 0x0100); // DESKTOP_SWITCHDESKTOP
            if (desktop == IntPtr.Zero) return false;
            CloseDesktop(desktop);
            return true;
        }
        internal static void EnableDpi()
        {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } catch (EntryPointNotFoundException) { }
            try { SetProcessDpiAwareness(2); } catch (DllNotFoundException) { }
        }
    }

    internal sealed class InputWindow : Form
    {
        internal event Action DirectInput;
        internal event Action<bool> DisplayChanged;
        internal event Action<int> SessionChanged;
        internal event Action Suspended;
        internal event Action Resumed;
        internal event Action DisplayLayoutChanged;
        internal event Action ShowRequested;
        internal event Action ExitRequested;
        internal event Action ToggleRequested;
        internal event Action MonitorOffRequested;
        internal Func<bool> AllowMonitorOff;
        internal const int MonitorOffMessage = 0x8000 + 20;
        internal static readonly uint ShowMessage = Native.RegisterWindowMessage("MonitorPausa.Show.1");
        internal static readonly uint ExitMessage = Native.RegisterWindowMessage("MonitorPausa.Exit.1");
        internal const int ToggleHotKeyId = 0x4D50;
        internal const int MonitorOffHotKeyId = 0x4D51;
        internal bool HotKeyRegistered { get; private set; }
        internal bool MonitorOffHotKeyRegistered { get; private set; }
        internal int MonitorOffHotKeyRegistrationError { get; private set; }
        internal int HotKeyRegistrationError { get; private set; }
        private IntPtr rawBuffer, powerNotification;
        private uint rawCapacity = 256;
        private readonly Dictionary<IntPtr, bool> dualSenseDevices = new Dictionary<IntPtr, bool>();
        private uint lastGamepadActivity;
        internal int DualSenseReports { get; private set; }
        internal int DualSenseActivityReports { get; private set; }
        internal string DualSenseReportFormat { get; private set; }
        private readonly uint headerSize = (uint)(8 + 2 * IntPtr.Size);
        private bool registered;
        private bool haveAbsoluteMouse;
        private int absoluteX, absoluteY;

        internal InputWindow()
        {
            Text = "ScreenCare.InputWindow";
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            rawBuffer = Marshal.AllocHGlobal(256);
            IntPtr hwnd = Handle;
            var devices = new[] {
                new Native.RawDevice { Page = 1, Usage = 2, Flags = 0x100, Target = hwnd },
                new Native.RawDevice { Page = 1, Usage = 6, Flags = 0x100, Target = hwnd },
                new Native.RawDevice { Page = 1, Usage = 4, Flags = 0x2100, Target = hwnd },
                new Native.RawDevice { Page = 1, Usage = 5, Flags = 0x2100, Target = hwnd }
            };
            if (!Native.RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf(typeof(Native.RawDevice))))
            { int error = Marshal.GetLastWin32Error(); Dispose(); throw new Win32Exception(error, L10n.T("Não foi possível monitorar teclado e mouse.")); }
            registered = true;
            // MOD_ALT | MOD_CONTROL | MOD_NOREPEAT, VK_F12. A collision must not prevent the tray app from starting.
            HotKeyRegistered = Native.RegisterHotKey(hwnd, ToggleHotKeyId, 0x0001 | 0x0002 | 0x4000, 0x7B);
            if (!HotKeyRegistered) HotKeyRegistrationError = Marshal.GetLastWin32Error();
            MonitorOffHotKeyRegistered = Native.RegisterHotKey(hwnd, MonitorOffHotKeyId, 0x0001 | 0x0002 | 0x4000, 0x7A);
            if (!MonitorOffHotKeyRegistered) MonitorOffHotKeyRegistrationError = Marshal.GetLastWin32Error();
            Guid guid = Native.DisplayState;
            powerNotification = Native.RegisterPowerSettingNotification(hwnd, ref guid, 0);
            if (powerNotification == IntPtr.Zero || !Native.WTSRegisterSessionNotification(hwnd, 0))
            { int error = Marshal.GetLastWin32Error(); Dispose(); throw new Win32Exception(error, L10n.T("Não foi possível acompanhar a sessão e os monitores.")); }
        }

        internal static bool MouseIsActivity(ushort flags, ushort buttons, int x, int y, ref bool haveAbsolute, ref int oldX, ref int oldY)
        {
            if ((flags & 1) == 0) return buttons != 0 || x != 0 || y != 0;
            bool changed = !haveAbsolute || oldX != x || oldY != y;
            haveAbsolute = true; oldX = x; oldY = y;
            return buttons != 0 || changed;
        }
        private void ReadInput(IntPtr handle)
        {
            uint size = 0;
            if (Native.GetRawInputData(handle, 0x10000003, IntPtr.Zero, ref size, headerSize) == UInt32.MaxValue || size > 65536) return;
            if (size > rawCapacity) { rawBuffer = Marshal.ReAllocHGlobal(rawBuffer, new IntPtr(size)); rawCapacity = size; }
            uint read = Native.GetRawInputData(handle, 0x10000003, rawBuffer, ref size, headerSize);
            if (read == UInt32.MaxValue || read < headerSize + 8) return;
            int type = Marshal.ReadInt32(rawBuffer);
            bool activity = type == 1;
            if (type == 2)
            {
                IntPtr device = Marshal.ReadIntPtr(rawBuffer, 8);
                if (!IsDualSense(device)) return;
                uint reportSize = (uint)Marshal.ReadInt32(rawBuffer, (int)headerSize);
                uint count = (uint)Marshal.ReadInt32(rawBuffer, (int)headerSize + 4);
                if (reportSize < 10 || reportSize > 512 || count > (read - headerSize - 8) / reportSize) return;
                byte[] report = new byte[reportSize];
                for (uint i = 0; i < count; i++)
                {
                    Marshal.Copy(IntPtr.Add(rawBuffer, (int)(headerSize + 8 + i * reportSize)), report, 0, report.Length);
                    bool active;
                    if (DualSenseInput.TryActivity(report, out active))
                    {
                        DualSenseReports++; DualSenseReportFormat = "0x" + report[0].ToString("X2") + "/" + report.Length;
                        if (active) { DualSenseActivityReports++; activity = true; }
                    }
                }
                uint tick = unchecked((uint)Environment.TickCount);
                if (activity && unchecked(tick - lastGamepadActivity) < 100) activity = false;
                if (activity) lastGamepadActivity = tick;
            }
            if (type == 0 && read >= headerSize + 24)
            {
                int offset = (int)headerSize;
                activity = MouseIsActivity((ushort)Marshal.ReadInt16(rawBuffer, offset), (ushort)Marshal.ReadInt16(rawBuffer, offset + 4),
                    Marshal.ReadInt32(rawBuffer, offset + 12), Marshal.ReadInt32(rawBuffer, offset + 16),
                    ref haveAbsoluteMouse, ref absoluteX, ref absoluteY);
            }
            if (activity && DirectInput != null) DirectInput();
        }
        private bool IsDualSense(IntPtr device)
        {
            bool found;
            if (dualSenseDevices.TryGetValue(device, out found)) return found;
            IntPtr info = Marshal.AllocHGlobal(32);
            try
            {
                uint size = 32; Marshal.WriteInt32(info, 32);
                found = Native.GetRawInputDeviceInfo(device, 0x2000000B, info, ref size) != UInt32.MaxValue
                    && Marshal.ReadInt32(info, 4) == 2
                    && DualSenseInput.IsDevice((uint)Marshal.ReadInt32(info, 8), (uint)Marshal.ReadInt32(info, 12));
                dualSenseDevices[device] = found; return found;
            }
            finally { Marshal.FreeHGlobal(info); }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == MonitorOffMessage)
            {
                // Recheck after queued input: a canceled request must not blank an active user.
                if (AllowMonitorOff != null && AllowMonitorOff())
                { var off = Message.Create(Handle, 0x0112, new IntPtr(0xF170), new IntPtr(2)); base.WndProc(ref off); }
                return;
            }
            if (m.Msg == Native.WM_INPUT) ReadInput(m.LParam);
            else if (m.Msg == 0x00FE) dualSenseDevices.Remove(m.LParam); // arrival/removal: handles can be reused
            else if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == ToggleHotKeyId)
            { if (ToggleRequested != null) ToggleRequested(); }
            else if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == MonitorOffHotKeyId)
            { if (MonitorOffRequested != null) MonitorOffRequested(); }
            else if ((uint)m.Msg == ShowMessage) { if (ShowRequested != null) ShowRequested(); }
            else if ((uint)m.Msg == ExitMessage) { if (ExitRequested != null) ExitRequested(); }
            else if (m.Msg == 0x007E) { if (DisplayLayoutChanged != null) DisplayLayoutChanged(); }
            else if (m.Msg == Native.WM_WTSSESSION_CHANGE)
            {
                int state = m.WParam.ToInt32();
                if (SessionChanged != null) SessionChanged(state);
            }
            else if (m.Msg == Native.WM_POWERBROADCAST)
            {
                int kind = m.WParam.ToInt32();
                if (kind == 4) { if (Suspended != null) Suspended(); }
                else if (kind == 7 || kind == 18) { if (Resumed != null) Resumed(); }
                else if (kind == 0x8013 && m.LParam != IntPtr.Zero)
                {
                    Guid setting = (Guid)Marshal.PtrToStructure(m.LParam, typeof(Guid));
                    if (setting == Native.DisplayState && Marshal.ReadInt32(m.LParam, 16) >= 4 && DisplayChanged != null)
                        DisplayChanged(Marshal.ReadInt32(m.LParam, 20) != 0);
                }
            }
            // DefWindowProc performs SC_MONITORPOWER and mandatory WM_INPUT cleanup.
            base.WndProc(ref m);
        }
        protected override void Dispose(bool disposing)
        {
            if (HotKeyRegistered) { Native.UnregisterHotKey(Handle, ToggleHotKeyId); HotKeyRegistered = false; }
            if (MonitorOffHotKeyRegistered) { Native.UnregisterHotKey(Handle, MonitorOffHotKeyId); MonitorOffHotKeyRegistered = false; }
            if (registered)
            {
                Native.RegisterRawInputDevices(new[] {
                    new Native.RawDevice { Page = 1, Usage = 2, Flags = 1 },
                    new Native.RawDevice { Page = 1, Usage = 6, Flags = 1 },
                    new Native.RawDevice { Page = 1, Usage = 4, Flags = 1 },
                    new Native.RawDevice { Page = 1, Usage = 5, Flags = 1 }
                }, 4, (uint)Marshal.SizeOf(typeof(Native.RawDevice)));
                Native.WTSUnRegisterSessionNotification(Handle);
                registered = false;
            }
            if (powerNotification != IntPtr.Zero) { Native.UnregisterPowerSettingNotification(powerNotification); powerNotification = IntPtr.Zero; }
            if (rawBuffer != IntPtr.Zero) { Marshal.FreeHGlobal(rawBuffer); rawBuffer = IntPtr.Zero; }
            base.Dispose(disposing);
        }
    }
}
