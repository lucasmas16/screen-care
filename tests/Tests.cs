using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace ScreenCare
{
    internal static class Tests
    {
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, IntPtr process);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint from, uint to, bool attach);
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
        private sealed class FullscreenTarget : Form
        {
            internal const int Promote = 0x8000 + 88;
            internal int Frames { get; private set; }
            private readonly System.Windows.Forms.Timer animation = new System.Windows.Forms.Timer { Interval = 30 };
            internal FullscreenTarget()
            {
                Text = "Screen Care fullscreen test"; FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual; Bounds = Screen.PrimaryScreen.Bounds;
                TopMost = true; BackColor = Color.MidnightBlue;
                animation.Tick += delegate { ++Frames; Invalidate(); };
                animation.Start();
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.FillRectangle(Brushes.CornflowerBlue, Frames % Math.Max(1, Width - 100), 40, 100, 50);
            }
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == Promote) { Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x13); return; }
                base.WndProc(ref m);
            }
            protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
        }
        private static int passed;
        private static readonly List<string> results = new List<string>();
        private static void Check(bool condition, string name)
        { if (!condition) throw new Exception("FAIL: " + name); ++passed; results.Add("PASS: " + name); }
        [STAThread]
        internal static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--gamepad-worker") return GamepadProcess.RunWorker(args[1]);
            if (args.Length == 2 && args[0] == "--worker-fixture")
            {
                Console.WriteLine("1"); Console.Out.Flush();
                Thread.Sleep(args[1] == "exit" ? 300 : 30000);
                return 77;
            }
            if (args.Length == 2 && args[0] == "--check-path")
            {
                try { SetupProgram.ValidatePaths(args[1], args[1]); return 0; }
                catch (IOException) { return 2; }
            }
            if (args.Length == 1 && args[0] == "--check-native-dlls")
            {
                try
                {
                    Native.EnableDpi(); GamepadMonitor.DescribeConnected();
                    using (Process process = Process.GetCurrentProcess())
                    foreach (ProcessModule module in process.Modules)
                        if (module.ModuleName.Equals("xinput1_4.dll", StringComparison.OrdinalIgnoreCase)
                            || module.ModuleName.Equals("winmm.dll", StringComparison.OrdinalIgnoreCase))
                            Check(String.Equals(Path.GetDirectoryName(module.FileName), Environment.SystemDirectory, StringComparison.OrdinalIgnoreCase), "native library loaded from Windows: " + module.ModuleName);
                    Check(passed >= 2, "both gamepad native libraries inspected");
                    Console.WriteLine(String.Join(Environment.NewLine, results)); return 0;
                }
                catch (Exception ex) { Console.Error.WriteLine(ex.GetType().Name); return 1; }
            }
            Native.EnableDpi(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 2 && args[0] == "--fullscreen-target")
            {
                using (var target = new FullscreenTarget())
                {
                    target.Shown += delegate { File.WriteAllText(args[1], target.Handle.ToInt64().ToString()); };
                    Application.Run(target);
                    File.WriteAllText(args[1] + ".frames", target.Frames.ToString());
                }
                return 0;
            }
            if (args.Length == 1 && args[0] == "--controllers")
            { Console.WriteLine(GamepadMonitor.DescribeConnected()); return 0; }
            if (args.Length == 2 && args[0] == "--switch-probe")
            {
                using (var input = new InputWindow())
                {
                    var monitor = new GamepadMonitor(); var clock = Stopwatch.StartNew();
                    int polls = 0, activePolls = 0;
                    while (clock.ElapsedMilliseconds < 15000)
                    { if (monitor.Poll(clock.Elapsed.TotalSeconds)) activePolls++; polls++; Pump(100); }
                    string report = "Switch reports=" + input.SwitchReports + "; active reports=" + input.SwitchActivityReports
                        + "; format=" + input.SwitchReportFormat + "; legacy/XInput active polls=" + activePolls + "/" + polls;
                    File.WriteAllText(args[1], report); Console.WriteLine(report);
                    return input.SwitchReports > 0 ? 0 : 1;
                }
            }
            if (args.Length == 2 && args[0] == "--dualsense-probe")
            {
                using (var input = new InputWindow())
                {
                    Pump(15000);
                    string report = "DualSense reports=" + input.DualSenseReports + "; active reports=" + input.DualSenseActivityReports + "; format=" + input.DualSenseReportFormat;
                    File.WriteAllText(args[1], report); Console.WriteLine(report);
                    return input.DualSenseReports > 0 ? 0 : 1;
                }
            }
            if (args.Length == 2 && args[0] == "--write-icon")
            { using (var icon = Theme.MakeIcon()) using (var stream = File.Create(args[1])) icon.Save(stream); return 0; }
            if (args.Length == 2 && args[0] == "--smoke") return Smoke(args[1]);
            if (args.Length == 1 && args[0] == "--preview")
            { Application.Run(new SettingsForm(new Settings { Enabled = false, Action = IdleAction.Slideshow })); return 0; }
            string root = Path.GetFullPath(args.Length > 0 ? args[0] : "test-results");
            Directory.CreateDirectory(root);
            Diagnostics.DirectoryPath = Path.Combine(root, "synthetic-diagnostics");
            L10n.Current = Language.Portuguese;
            try
            {
                TestIdle(); TestPowerGuard(); TestGamepads(); TestSession(); TestPaint(); TestMouse(); TestSettings(root); TestSetupFiles(root); TestImages(root); TestWindows(root); TestStartupLayout(root); TestToasts(root); TestFullscreenToast(root); TestLocalization(root); TestLifetime(root);
                results.Add(passed + " checks passed.");
                File.WriteAllLines(Path.Combine(root, "results.txt"), results);
                Console.WriteLine(String.Join(Environment.NewLine, results));
                return 0;
            }
            catch (Exception ex) { results.Add(ex.ToString()); File.WriteAllLines(Path.Combine(root, "results.txt"), results); Console.Error.WriteLine(ex); return 1; }
        }
        private static void TestIdle()
        {
            var idle = new IdleController(100);
            Check(!idle.TryActivate(109.99, 10, true, false), "wait until threshold");
            Check(idle.TryActivate(110, 10, true, false), "activate at exact threshold");
            Check(!idle.TryActivate(200, 10, true, false), "one activation per idle period");
            Check(idle.Input(201), "real input dismisses active action");
            Check(!idle.Active && idle.LastActivity == 201, "input resets activity and state");
            Check(!idle.TryActivate(205, 10, true, false), "input rearms full wait interval");
            Check(idle.TryActivate(211, 10, true, false), "next idle period activates");
            idle.Reset(300);
            Check(!idle.TryActivate(500, 10, false, false), "disabled mode never activates");
            Check(!idle.TryActivate(500, 10, true, true), "settings or blocked session prevents activation");
            idle.Reset(600);
            Check(!idle.TryActivate(599, 10, true, false), "backward clock cannot trigger");
            Check(idle.TryActivate(610, 10, true, false), "elapsed clock only; media does not reset timer");
            idle.Failed(610);
            Check(!idle.TryActivate(669, 10, true, false), "unavailable images back off for 60 seconds");
            Check(idle.TryActivate(670, 10, true, false), "retry after backoff");
            idle.Force(700); Check(idle.Active, "manual start while automation paused");
            idle.Reset(800); Check(!idle.Active && !idle.TryActivate(801, 10, true, false), "resume and display topology reset action");
            idle.Force(900);
            for (int second = 901; second <= 960; second++)
            {
                Check(!idle.DisplayLayoutChanged(second, true) && idle.Active, "monitor sleep topology does not rearm or request wake " + second);
                Check(!idle.TryActivate(second, 10, true, false), "no repeated off request while already active " + second);
            }
            idle.Input(961);
            Check(!idle.TryActivate(970, 10, true, false) && idle.TryActivate(971, 10, true, false), "real activity rearms a complete interval after display sleep");
            Check(idle.DisplayLayoutChanged(972, false) && !idle.Active, "slideshow layout changes still reset windows");
        }
        private static void TestPowerGuard()
        {
            var guard = new MonitorPowerGuard();
            Check(!guard.TryRetry(100, true), "inactive monitor guard never sends power commands");
            guard.Begin(10);
            Check(!guard.TryRetry(14.99, true) && guard.TryRetry(15, true), "missing power acknowledgement retries after five seconds");
            Check(!guard.TryRetry(29.99, true) && guard.TryRetry(30, true), "second retry waits fifteen seconds");
            Check(!guard.TryRetry(59.99, true) && guard.TryRetry(60, true), "third retry waits thirty seconds");
            Check(!guard.TryRetry(119.99, true) && guard.TryRetry(120, true), "persistent conflicts back off to sixty seconds");
            guard.Observe(false, 121);
            Check(!guard.TryRetry(10000, true), "confirmed display-off stops all retries");
            guard.Observe(true, 10001); guard.Observe(true, 10002);
            Check(!guard.TryRetry(10003.99, true) && guard.TryRetry(10004, true), "unexpected wake retries after input grace period without duplicate-notification postponement");
            guard.Reset();
            Check(!guard.TryRetry(20000, true), "user input cancels queued retry");
            guard.Begin(0); guard.Observe(true, 1);
            Check(!guard.TryRetry(100, false), "settings, suspended, disconnected and secure sessions cannot retry");
            guard.Reset(); guard.Observe(true, 101);
            Check(!guard.TryRetry(200, true), "late display notification cannot rearm canceled guard");
            guard.Begin(300); guard.Observe(false, 301); guard.Observe(true, 302); guard.Reset();
            Check(!guard.TryRetry(400, true), "input queued after wake prevents immediate reblanking");
            var manual = new DelayedAction();
            manual.Schedule(10, true);
            Check(manual.Pending && !manual.TryTake(11.999), "F11 waits the full two seconds");
            Check(manual.TryTake(12) && manual.MonitorsOnly && !manual.Pending, "F11 explicitly selects monitors-off regardless of slideshow setting");
            Check(!manual.TryTake(14), "manual action fires once");
            manual.Schedule(20, true); manual.Schedule(21, true);
            Check(!manual.TryTake(22) && manual.TryTake(23), "second shortcut invocation restarts countdown");
            manual.Schedule(30, false); manual.Cancel();
            Check(!manual.TryTake(32), "pausing or session change cancels manual countdown");
        }
        private static void TestGamepads()
        {
            Check(DualSenseInput.IsDevice(0x054C, 0x0CE6) && DualSenseInput.IsDevice(0x054C, 0x0DF2)
                && !DualSenseInput.IsDevice(0x1234, 0x0CE6), "raw input restricted to DualSense and Edge identifiers");
            foreach (int format in new[] { 10, 64, 78, 79 })
            {
                bool simple = format == 10 || format == 79;
                byte[] report = new byte[format == 79 ? 78 : format];
                report[0] = (byte)(format == 78 ? 0x31 : 1);
                int start = format == 78 ? 2 : 1;
                for (int i = 0; i < 4; i++) report[start + i] = 128;
                int buttons = start + (simple ? 4 : 7), triggers = start + (simple ? 7 : 4);
                report[buttons] = 8;
                bool active;
                Check(DualSenseInput.TryActivity(report, out active) && !active, "DualSense neutral protocol " + format);
                if (simple) report[buttons + 2] = 0xFC;
                else { report[start + 6] = 255; for (int i = start + 15; i < report.Length; i++) report[i] = 255; }
                Check(DualSenseInput.TryActivity(report, out active) && !active, "counter, sensors and battery never count " + format);
                report[buttons] = 0x28;
                Check(DualSenseInput.TryActivity(report, out active) && active, "DualSense raw held button " + format);
                report[buttons] = 8; report[triggers] = 150;
                Check(DualSenseInput.TryActivity(report, out active) && active, "DualSense raw trigger " + format);
                report[triggers] = 0; report[start + 2] = 255;
                Check(DualSenseInput.TryActivity(report, out active) && active, "DualSense raw right stick " + format);
                report[start + 2] = 135;
                Check(DualSenseInput.TryActivity(report, out active) && !active, "DualSense raw minor drift ignored " + format);
                report[buttons] = 0;
                Check(DualSenseInput.TryActivity(report, out active) && active, "DualSense raw held dpad " + format);
            }
            bool ignored;
            Check(!DualSenseInput.TryActivity(new byte[8], out ignored) && !DualSenseInput.TryActivity(new byte[64], out ignored), "short and unknown HID packets rejected");
            Check(Marshal.SizeOf(typeof(GamepadMonitor.XboxState)) == 16 && Marshal.SizeOf(typeof(GamepadMonitor.JoyState)) == 52
                && Marshal.SizeOf(typeof(GamepadMonitor.JoyCaps)) == 728, "controller native structures match Windows ABI");
            Check(!GamepadMonitor.XboxActive(new GamepadMonitor.XboxState { Packet = 123, LX = 1000, RY = -2000, LeftTrigger = 20 }), "Xbox packet updates and analog drift do not prevent idle");
            Check(GamepadMonitor.XboxActive(new GamepadMonitor.XboxState { Buttons = 0x1000 }), "Xbox held button counts even without packet change");
            Check(GamepadMonitor.XboxActive(new GamepadMonitor.XboxState { LX = Int16.MinValue }), "negative stick extreme handled without overflow");
            Check(GamepadMonitor.XboxActive(new GamepadMonitor.XboxState { RightTrigger = 100 }), "Xbox held trigger counts");
            var caps = new GamepadMonitor.JoyCaps { Name = "Wireless Controller", Caps = 31, Axes = 6,
                XMax = 65535, YMax = 65535, ZMax = 65535, RMax = 65535, UMax = 65535, VMax = 65535 };
            var neutral = GamepadMonitor.NewJoyState(); neutral.X = neutral.Y = neutral.Z = neutral.R = 32767;
            var tracker = new GamepadMonitor.LegacyActivity(caps, neutral);
            Check(!tracker.Observe(neutral), "DualSense connected at rest does not count as activity");
            var drift = neutral; drift.X += 1500; drift.Z -= 800;
            Check(!tracker.Observe(drift), "DualSense stick drift is filtered");
            var idle = new IdleController(0);
            for (int t = 1; t <= 60; t++)
            {
                var held = neutral; held.Buttons = 1;
                if (tracker.Observe(held)) idle.Input(t);
                Check(!idle.TryActivate(t, 10, true, false), "held DualSense button prevents activation " + t);
            }
            Check(!tracker.Observe(neutral) && !idle.TryActivate(69, 10, true, false) && idle.TryActivate(70, 10, true, false), "releasing controller starts full idle countdown");
            for (int axis = 0; axis < 6; axis++)
            {
                var held = neutral;
                if (axis == 0) held.X = 65535; if (axis == 1) held.Y = 0;
                if (axis == 2) held.Z = 65535; if (axis == 3) held.R = 0;
                if (axis == 4) held.U = 65535; if (axis == 5) held.V = 65535;
                Check(tracker.Observe(held) && tracker.Observe(held), "DualSense held stick/trigger axis " + axis);
            }
            var dpad = neutral; dpad.Pov = 0;
            Check(tracker.Observe(dpad), "DualSense held directional pad counts");
            caps.Caps = 0; var basic = new GamepadMonitor.LegacyActivity(caps, neutral);
            Check(!basic.Observe(dpad), "unavailable POV and optional axes are ignored");
            TestSwitch();
            TestGamepadProcess();
        }
        private static void TestGamepadProcess()
        {
            var clock = Stopwatch.StartNew();
            using (var worker = new GamepadProcess(delegate { return new ProcessStartInfo(Application.ExecutablePath, "--worker-fixture exit"); }))
            {
                bool sawActive = false;
                while (clock.ElapsedMilliseconds < 1500) { sawActive |= worker.Poll(clock.Elapsed.TotalSeconds); Thread.Sleep(10); }
                Check(sawActive, "isolated controller state reaches parent");
                Check(!worker.Poll(clock.Elapsed.TotalSeconds) && worker.ChildId == 0, "controller worker exit clears stale input and preserves parent");
                worker.Poll(20);
                Check(worker.ChildId != 0, "controller worker restarts after backoff");
            }
            using (var worker = new GamepadProcess(delegate { return new ProcessStartInfo(Application.ExecutablePath, "--worker-fixture hang"); }))
            {
                clock.Restart(); bool sawActive = false;
                while (clock.ElapsedMilliseconds < 2500) { sawActive |= worker.Poll(clock.Elapsed.TotalSeconds); Thread.Sleep(10); }
                Check(sawActive && !worker.Poll(3), "stopped controller heartbeat cannot hold display awake");
                int id = worker.ChildId;
                worker.Dispose();
                bool ended;
                try { using (var p = Process.GetProcessById(id)) ended = p.HasExited; } catch (ArgumentException) { ended = true; }
                Check(ended && !worker.Poll(30), "shutdown terminates owned controller worker without restart");
            }
        }
        private static void TestSwitch()
        {
            Check(SwitchInput.IsDevice(0x057E, 0x2009) && !SwitchInput.IsDevice(0x054C, 0x2009)
                && !SwitchInput.IsDevice(0x057E, 0x2006), "Switch Pro selection is limited to supported IDs");
            bool active;
            Check(!SwitchInput.TryActivity(null, out active) && !SwitchInput.TryActivity(new byte[11], out active), "Switch truncated reports rejected");
            foreach (byte id in new byte[] { 0x30, 0x31, 0x21 })
            {
                var report = new byte[64]; report[0] = id;
                report[7] = report[10] = 8;
                report[8] = report[11] = 128;
                for (int i = 0; i < 256; i++)
                {
                    report[1] = (byte)i; report[2] = (byte)i;
                    for (int j = 12; j < report.Length; j++) report[j] = (byte)(i + j);
                    Check(SwitchInput.TryActivity(report, out active) && !active, "Switch counter/battery/IMU ignored " + id + "/" + i);
                }
                for (int field = 3; field <= 5; field++)
                {
                    report[field] = 1;
                    Check(SwitchInput.TryActivity(report, out active) && active, "Switch button/dpad held " + id + "/" + field);
                    report[field] = 0;
                }
                report[6] = 255; report[7] = 15;
                Check(SwitchInput.TryActivity(report, out active) && active, "Switch held analog " + id);
                Check(!SwitchInput.TryActivity(new byte[] { id }, out active), "Switch short header rejected " + id);
            }
            var simple = new byte[12]; simple[0] = 0x3F; simple[3] = 8;
            for (int i = 5; i < 12; i += 2) simple[i] = 128;
            Check(SwitchInput.TryActivity(simple, out active) && !active, "Switch simple neutral");
            simple[1] = 128; Check(SwitchInput.TryActivity(simple, out active) && active, "Switch simple trigger");
            simple[1] = 0; simple[3] = 0; Check(SwitchInput.TryActivity(simple, out active) && active, "Switch simple directional pad");
            simple[3] = 8; simple[5] = 255; Check(SwitchInput.TryActivity(simple, out active) && active, "Switch simple analog");
            simple[0] = 0x81; Check(!SwitchInput.TryActivity(simple, out active), "Switch USB status is not input");
            var caps = new GamepadMonitor.JoyCaps { Manufacturer = 0x057E, Product = 0x2009 };
            var bogus = GamepadMonitor.NewJoyState(); bogus.Buttons = 119231; bogus.Y = 12544; bogus.R = 59347;
            var tracker = new GamepadMonitor.LegacyActivity(caps, bogus);
            var idle = new IdleController(0);
            for (int t = 1; t <= 30; t++) if (tracker.Observe(bogus)) idle.Input(t);
            Check(idle.TryActivate(30, 30, true, false), "Switch bogus WinMM data cannot prevent screen timeout");
            var state = new SwitchActivity();
            state.Observe(0x3F, true, 10);
            Check(state.IsActive(60000), "Switch simple held command survives event-only reporting");
            state.Observe(0x3F, false, 60001);
            Check(!state.IsActive(60001), "Switch simple release clears held command");
            state.Observe(0x30, true, UInt32.MaxValue - 10);
            Check(state.IsActive(20) && !state.IsActive(1000), "Switch streaming state expires across tick wrap");
        }
        private static void Pump(int milliseconds)
        {
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(10); }
        }
        private static void TestToasts(string root)
        {
            IntPtr foreground = GetForegroundWindow();
            using (var host = new StatusToastHost())
            {
                host.ShowStatus(true, Appearance.Dark); Pump(100);
                var first = host.Current; var area = Screen.PrimaryScreen.WorkingArea;
                Check(Math.Abs(first.Left + first.Width / 2 - (area.Left + area.Width / 2)) <= 1
                    && first.Bottom < area.Bottom && first.Top > area.Top, "toast centered at bottom of primary monitor");
                Check(GetForegroundWindow() == foreground, "toast does not steal foreground focus");
                using (var bitmap = new Bitmap(first.Width, first.Height))
                { first.DrawToBitmap(bitmap, first.ClientRectangle); bitmap.Save(Path.Combine(root, "toast-enabled.png")); }
                using (var bitmap = new Bitmap(first.Width, first.Height))
                using (var g = Graphics.FromImage(bitmap))
                { g.CopyFromScreen(first.Location, Point.Empty, first.Size); bitmap.Save(Path.Combine(root, "toast-enabled-desktop.png")); }
                Pump(1500); host.ShowStatus(false, Appearance.Dark); Pump(100);
                Check(first.IsDisposed && host.Current.MonitoringEnabled == false, "new alert disposes and replaces previous state immediately");
                using (var bitmap = new Bitmap(host.Current.Width, host.Current.Height))
                { host.Current.DrawToBitmap(bitmap, host.Current.ClientRectangle); bitmap.Save(Path.Combine(root, "toast-disabled.png")); }
                Pump(1600);
                Check(host.Current != null && host.Current.Visible, "old alert deadline cannot close replacement alert");
                Pump(1500); Check(host.Current == null, "replacement closes after its own full duration");
                foreground = GetForegroundWindow(); // The user may switch apps during the expiration waits above.
                for (int i = 0; i < 12; i++) { var old = host.Current; host.ShowStatus(i % 2 == 0, Appearance.Light); if (old != null) Check(old.IsDisposed, "rapid toggle removes old toast " + i); }
                Check(host.Current.MonitoringEnabled == false && GetForegroundWindow() == foreground, "rapid toggles display only final state without focus change");
                using (var bitmap = new Bitmap(host.Current.Width, host.Current.Height))
                { host.Current.DrawToBitmap(bitmap, host.Current.ClientRectangle); bitmap.Save(Path.Combine(root, "toast-light.png")); }
                host.Hide(); Check(host.Current == null, "power-off dismisses pending notification");
            }
            Check(StatusToast.Position(new Rectangle(-1920, -200, 1920, 1040), new Size(420, 132), 20) == new Point(-1170, 688), "toast positioning supports negative monitor coordinates");
        }
        private static void TestFullscreenToast(string root)
        {
            string ready = Path.Combine(root, "fullscreen-" + Guid.NewGuid().ToString("N"));
            using (var child = Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--fullscreen-target \"" + ready + "\"") { UseShellExecute = false, CreateNoWindow = true }))
            {
                IntPtr target = IntPtr.Zero;
                try
                {
                    var wait = Stopwatch.StartNew();
                    while (!File.Exists(ready) && !child.HasExited && wait.ElapsedMilliseconds < 5000) Pump(50);
                    Check(File.Exists(ready), "separate fullscreen process started");
                    target = new IntPtr(Int64.Parse(File.ReadAllText(ready))); Pump(200);
                    // Test setup only: emulate switching to the game before showing any toast.
                    uint currentThread = GetCurrentThreadId();
                    uint foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
                    bool attached = currentThread != foregroundThread && AttachThreadInput(currentThread, foregroundThread, true);
                    uint targetThread = GetWindowThreadProcessId(target, IntPtr.Zero);
                    bool targetAttached = targetThread != currentThread && AttachThreadInput(currentThread, targetThread, true);
                    bool activated;
                    try { activated = SetForegroundWindow(target); }
                    finally {
                        if (targetAttached) AttachThreadInput(currentThread, targetThread, false);
                        if (attached) AttachThreadInput(currentThread, foregroundThread, false);
                    }
                    Pump(100);
                    Check(GetForegroundWindow() == target, "fullscreen process owns foreground before overlay test (activated=" + activated + ", attached=" + attached + "/" + targetAttached + ", foreground=" + GetForegroundWindow() + ", target=" + target + ")");
                    using (var host = new StatusToastHost())
                    {
                        host.ShowStatus(true, Appearance.Dark); Pump(200);
                        var toast = host.Current;
                        long styles = GetWindowLongPtr(toast.Handle, -20).ToInt64();
                        Check((styles & 0x080800A0) == 0x080800A0, "toast uses no-activate toolwindow and layered mouse pass-through styles");
                        Point inside = new Point(toast.Left + toast.Width / 2, toast.Top + toast.Height / 2);
                        Check(WindowFromPoint(inside) == target, "mouse hit-test passes through visible toast to another process");
                        Check(GetForegroundWindow() == target, "showing toast preserves fullscreen foreground");
                        Native.PostMessage(target, FullscreenTarget.Promote, IntPtr.Zero, IntPtr.Zero);
                        Pump(350);
                        bool above = false;
                        for (IntPtr window = GetWindow(target, 3); window != IntPtr.Zero; window = GetWindow(window, 3))
                            if (window == toast.Handle) { above = true; break; }
                        Check(above, "toast regains z-order over competing topmost fullscreen window");
                        Check(GetForegroundWindow() == target, "z-order refresh preserves fullscreen foreground");
                        using (var bitmap = new Bitmap(toast.Width, toast.Height))
                        using (var graphics = Graphics.FromImage(bitmap))
                        {
                            graphics.CopyFromScreen(toast.Location, Point.Empty, toast.Size);
                            bitmap.Save(Path.Combine(root, "toast-over-fullscreen.png"));
                            Check(bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2).ToArgb() != Color.MidnightBlue.ToArgb(), "desktop capture contains overlay over fullscreen surface");
                        }
                        for (int i = 0; i < 8; i++)
                        {
                            host.ShowStatus(i % 2 == 0, Appearance.Dark); Pump(40);
                            Check(GetForegroundWindow() == target, "fullscreen focus survives rapid status replacement " + i);
                        }
                        Pump(StatusToast.Duration + 200);
                        Check(host.Current == null && GetForegroundWindow() == target, "expiration leaves fullscreen process focused without another click");
                    }
                }
                finally
                {
                    if (target != IntPtr.Zero) Native.PostMessage(target, 0x0010, IntPtr.Zero, IntPtr.Zero);
                    if (!child.WaitForExit(5000)) { child.Kill(); child.WaitForExit(); }
                }
                Check(child.ExitCode == 0 && Int32.Parse(File.ReadAllText(ready + ".frames")) > 30, "fullscreen animation continues while alerts are displayed");
            }
        }
        private static void TestStartupLayout(string root)
        {
            foreach (Language language in new[] { Language.English, Language.Portuguese })
            foreach (Appearance appearance in new[] { Appearance.Dark, Appearance.Light })
            using (var form = new SettingsForm(new Settings { Enabled = false, Language = language, Appearance = appearance }))
            {
                string name = language + "-" + appearance;
                Render(form, Path.Combine(root, "startup-" + name + ".png"));
                var toggle = Find<Toggle>(form, L10n.Get("Iniciar com o Windows", language));
                var row = toggle.Parent;
                var labels = (TableLayoutPanel)row.Controls[0];
                var title = (Label)labels.Controls[0];
                var caption = (Label)labels.Controls[1];
                Check(caption.Text == L10n.Get("Retomar as últimas configurações", language), "startup caption preserved in " + name);
                AssertCaptionFits(caption, name);
                using (var titleFont = new Font(title.Font.FontFamily, 16, title.Font.Style))
                using (var captionFont = new Font(caption.Font.FontFamily, 14, caption.Font.Style))
                {
                    title.Font = titleFont; caption.Font = captionFont;
                    form.Size = form.MinimumSize;
                    Render(form, Path.Combine(root, "startup-large-text-" + name + ".png"));
                    AssertCaptionFits(caption, "large text / minimum window " + name);
                    using (var bitmap = new Bitmap(row.Width, row.Height))
                    { row.DrawToBitmap(bitmap, row.ClientRectangle); bitmap.Save(Path.Combine(root, "startup-caption-" + name + ".png")); }
                }
            }
            L10n.Current = Language.Portuguese;
        }
        private static void AssertCaptionFits(Label caption, string context)
        {
            Size required = caption.GetPreferredSize(new Size(caption.Width, 0));
            Check(caption.Height >= required.Height, "startup caption text height fits: " + context);
            Rectangle visible = caption.ClientRectangle;
            Control child = caption;
            while (child.Parent != null && !(child.Parent is Form))
            {
                visible.Offset(child.Left, child.Top);
                Control parent = child.Parent;
                if (parent is ScrollableControl && ((ScrollableControl)parent).AutoScroll) break;
                Check(parent.ClientRectangle.Contains(visible), "startup caption fits ancestor " + parent.GetType().Name + ": " + context);
                child = parent;
            }
        }
        private static void TestMouse()
        {
            Check(InputWindow.KeyboardIsActivity(0x57, 0x7A), "valid F11 keyboard input counts");
            Check(InputWindow.KeyboardIsActivity(0, 0xB3), "media key without a scan code remains supported");
            Check(!InputWindow.KeyboardIsActivity(0xFF, 0x41), "keyboard overrun does not cancel monitor-off");
            Check(!InputWindow.KeyboardIsActivity(0, 0xFF) && !InputWindow.KeyboardIsActivity(0, 0), "unmapped and empty keyboard reports are ignored");
            bool have = false; int x = 0, y = 0;
            Check(!InputWindow.MouseIsActivity(0, 0, 0, 0, ref have, ref x, ref y), "empty mouse report is ignored");
            Check(InputWindow.MouseIsActivity(0, 0, 1, 0, ref have, ref x, ref y), "relative movement counts");
            Check(InputWindow.MouseIsActivity(0, 1, 0, 0, ref have, ref x, ref y), "click counts");
            Check(InputWindow.MouseIsActivity(0, 0x400, 0, 0, ref have, ref x, ref y), "wheel counts");
            Check(InputWindow.MouseIsActivity(1, 0, 150, 200, ref have, ref x, ref y), "initial absolute movement counts");
            Check(!InputWindow.MouseIsActivity(1, 0, 150, 200, ref have, ref x, ref y), "stationary absolute reports are ignored");
            Check(InputWindow.MouseIsActivity(1, 0, 151, 200, ref have, ref x, ref y), "absolute position change counts");
        }
        private static void TestSession()
        {
            var session = new SessionState(); var idle = new IdleController(0);
            Check(session.Change(7), "lock changes session once"); idle.Reset(100);
            Check(!session.Change(7), "duplicate lock notifications preserve countdown");
            Check(session.Allows(IdleAction.Slideshow, false, true) && session.EffectiveAction(IdleAction.Slideshow) == IdleAction.MonitorsOff, "locked slideshow falls back to monitors-off without overlaying secure desktop");
            for (int i = 101; i < 110; i++)
                Check(!idle.TryActivate(i, 10, true, !session.Allows(IdleAction.MonitorsOff, false, true)), "locked wait " + i);
            Check(idle.TryActivate(110, 10, true, !session.Allows(IdleAction.MonitorsOff, false, true)), "locked monitors turn off even with settings open");
            Check(!session.ObserveInput(42) && !session.ObserveInput(42) && session.ObserveInput(43), "secure session input changes detected once; media does not count");
            idle.Input(111);
            Check(!idle.TryActivate(120, 10, true, false) && idle.TryActivate(121, 10, true, false), "login input rearms full interval");
            session.Change(4); Check(!session.Allows(IdleAction.MonitorsOff, false, false), "disconnected locked session does not power off displays");
            session.Change(3); Check(session.Locked && session.Allows(IdleAction.MonitorsOff, false, false), "reconnect preserves lock state");
            session.Suspended = true; Check(!session.Allows(IdleAction.MonitorsOff, false, false), "suspend blocks monitor commands");
            session.Suspended = false; session.Change(8);
            Check(session.EffectiveAction(IdleAction.Slideshow) == IdleAction.Slideshow, "unlock restores configured slideshow mode");
            Check(!session.Allows(IdleAction.MonitorsOff, false, false), "unlocked UAC secure desktop pauses actions");
            Check(!session.Allows(IdleAction.MonitorsOff, true, true) && session.Allows(IdleAction.Slideshow, true, false), "unlocked settings pause and desktop slideshow retained");
            uint inputTime; Check(Native.TryLastInput(out inputTime), "session last-input API available");
        }
        private static void TestPaint()
        {
            // Simulate WM_PAINT into an uninitialized black buffer, not DrawToBitmap's parent fill.
            foreach (Appearance appearance in new[] { Appearance.Dark, Appearance.Light })
            using (var parent = new Panel { Size = new Size(300, 100), BackColor = new Palette(appearance).Background })
            foreach (Control child in new Control[] { new SoftButton(), new Toggle(), new ThemeToggle(), new ModeTile() })
            using (child)
            {
                parent.Controls.Add(child); ((IThemed)child).SetPalette(new Palette(appearance));
                using (var bitmap = new Bitmap(child.Width, child.Height))
                using (var graphics = Graphics.FromImage(bitmap))
                using (var args = new PaintEventArgs(graphics, child.ClientRectangle))
                {
                    graphics.Clear(Color.Black);
                    child.GetType().GetMethod("OnPaintBackground", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(child, new object[] { args });
                    child.GetType().GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(child, new object[] { args });
                    Check(!(bool)typeof(Control).GetMethod("GetStyle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(child, new object[] { ControlStyles.Opaque }), "native paint dispatch includes " + child.GetType().Name + " background");
                    Check(bitmap.GetPixel(0, 0).ToArgb() == parent.BackColor.ToArgb(), appearance + " " + child.GetType().Name + " corners inherit parent without black blocks: " + bitmap.GetPixel(0, 0) + " expected " + parent.BackColor);
                }
            }
        }
        private static void TestSettings(string root)
        {
            string folder = Path.Combine(root, "config-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "settings.xml"), warning;
            var settings = SettingsStore.Load(path, out warning);
            Check(settings.IdleSeconds == 300 && warning == null, "first launch defaults");
            settings.ImageFolder = @"C:\Fotos & imagens\São Paulo"; settings.Enabled = false; settings.StartWithWindows = true;
            settings.Action = IdleAction.Slideshow; settings.IncludeSubfolders = true; settings.SlideSeconds = 25;
            SettingsStore.Save(settings, path); Settings restored = SettingsStore.Load(path, out warning);
            Check(warning == null && restored.ImageFolder == settings.ImageFolder && restored.Action == IdleAction.Slideshow, "Unicode and XML special characters round-trip");
            Check(!restored.Enabled && restored.StartWithWindows && restored.IncludeSubfolders && restored.SlideSeconds == 25, "preferences persist together");
            settings.Appearance = Appearance.Light; SettingsStore.Save(settings, path);
            Check(SettingsStore.Load(path, out warning).Appearance == Appearance.Light, "light theme persists across launches");
            File.WriteAllText(path, File.ReadAllText(path).Replace("<Appearance>Light</Appearance>", ""));
            Settings legacy = SettingsStore.Load(path, out warning);
            Check(warning == null && legacy.Appearance == Appearance.Dark && legacy.ImageFolder == settings.ImageFolder && !legacy.Enabled && legacy.StartWithWindows, "version 1.0 settings migrate without losing preferences");
            settings.IdleSeconds = 123; SettingsStore.Save(settings, path);
            Check(SettingsStore.Load(path, out warning).IdleSeconds == 123, "atomic update existing settings");
            Check(Directory.GetFiles(folder, "*.tmp").Length == 0, "no temporary config left after save");
            settings.IdleSeconds = Int32.MaxValue; settings.SlideSeconds = -50; settings.Action = (IdleAction)999; settings.Normalize();
            Check(settings.IdleSeconds == 86400 && settings.SlideSeconds == 2 && settings.Action == IdleAction.MonitorsOff, "invalid settings clamped safely");
            File.WriteAllText(path, "not xml");
            Check(!SettingsStore.Load(path, out warning).Enabled && warning != null, "corrupt config disables automation with warning");
            File.WriteAllText(path, "<!DOCTYPE Settings [<!ENTITY external SYSTEM 'file:///C:/Windows/win.ini'>]><Settings><ImageFolder>&external;</ImageFolder></Settings>");
            Check(!SettingsStore.Load(path, out warning).Enabled && warning != null, "external XML entities rejected");
            File.WriteAllText(path, "<Settings xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:nil='true' />");
            Check(!SettingsStore.Load(path, out warning).Enabled && warning != null, "null XML settings disable automation without crashing");
            File.WriteAllText(path, "<Settings><ImageFolder>" + new String('x', 70000) + "</ImageFolder></Settings>");
            Check(!SettingsStore.Load(path, out warning).Enabled && warning != null, "oversized XML settings are rejected");
        }
        private static void TestSetupFiles(string root)
        {
            string install = Path.Combine(root, "upgrade-" + Guid.NewGuid().ToString("N"));
            string stage = Path.Combine(install, "stage"); Directory.CreateDirectory(stage);
            foreach (string name in SetupProgram.OwnedFiles) File.WriteAllText(Path.Combine(stage, name), "new synthetic payload");
            foreach (string name in SetupProgram.LegacyFiles) File.WriteAllText(Path.Combine(install, name), "old synthetic payload");
            File.WriteAllText(Path.Combine(install, "LEIA-ME.txt"), "old manual");
            File.WriteAllText(Path.Combine(install, "unrelated.txt"), "keep me");
            var previous = new Dictionary<string, byte[]>();
            SetupProgram.InstallFiles(install, stage, previous);
            Check(File.ReadAllText(Path.Combine(install, "ScreenCare.exe")) == "new synthetic payload"
                && !File.Exists(Path.Combine(install, "MonitorPausa.exe")), "upgrade replaces old executable with Screen Care");
            Check(File.ReadAllText(Path.Combine(install, "unrelated.txt")) == "keep me", "upgrade preserves unrelated files");
            SetupProgram.RestoreFiles(previous);
            Check(File.ReadAllText(Path.Combine(install, "MonitorPausa.exe")) == "old synthetic payload"
                && !File.Exists(Path.Combine(install, "ScreenCare.exe")), "rollback restores old executable and removes new files");
            Check(File.ReadAllText(Path.Combine(install, "LEIA-ME.txt")) == "old manual", "rollback restores overwritten content");
            File.Delete(Path.Combine(stage, "Uninstall.exe")); previous.Clear(); bool failed = false;
            try { SetupProgram.InstallFiles(install, stage, previous); }
            catch (IOException) { failed = true; SetupProgram.RestoreFiles(previous); }
            Check(failed && !File.Exists(Path.Combine(install, "ScreenCare.exe"))
                && File.ReadAllText(Path.Combine(install, "MonitorPausa.exe")) == "old synthetic payload"
                && File.ReadAllText(Path.Combine(install, "LEIA-ME.txt")) == "old manual", "failed payload install can roll back partial writes");
        }
        private static void TestImages(string root)
        {
            string folder = Path.Combine(root, "images-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            string child = Path.Combine(folder, "sub"); Directory.CreateDirectory(child);
            string photo = Path.Combine(folder, "A.PNG");
            using (var bitmap = new Bitmap(1600, 900))
            {
                using (var g = Graphics.FromImage(bitmap)) { g.Clear(Color.FromArgb(19, 36, 62)); g.FillEllipse(Brushes.DarkCyan, 200, 80, 1100, 800); }
                bitmap.Save(photo, ImageFormat.Png); bitmap.Save(Path.Combine(child, "B.jpg"), ImageFormat.Jpeg);
            }
            File.WriteAllText(Path.Combine(folder, "ignored.txt"), "x");
            Check(Images.Find(folder, false, CancellationToken.None).Count == 1, "folder scan filters extensions case-insensitively");
            Check(Images.Find(folder, true, CancellationToken.None).Count == 2, "optional subfolder scan");
            var token = new CancellationTokenSource(); token.Cancel(); bool canceled = false;
            try { Images.Find(folder, true, token.Token); } catch (OperationCanceledException) { canceled = true; } token.Dispose();
            Check(canceled, "catalog scan cancels when user returns");
            using (Bitmap loaded = Images.Load(photo, new Size(800, 600)))
            {
                Check(loaded.Width == 800 && loaded.Height == 450, "decoded bitmap is reduced to screen size");
                using (var stream = new FileStream(photo, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Check(stream.Length > 0, "displayed image does not keep file locked");
                using (var view = new SlideWindow(new Rectangle(0, 0, 800, 600)))
                {
                    view.Slide = loaded; Render(view, Path.Combine(root, "slideshow-preview.png"));
                    using (var image = new Bitmap(Path.Combine(root, "slideshow-preview.png")))
                        Check(image.GetPixel(2, 2).R == 0 && image.GetPixel(400, 300).G > 0, "slideshow paints image with black letterbox");
                }
            }
            Check(Images.Fit(new Size(1600, 900), new Size(800, 600)) == new Rectangle(0, 75, 800, 450), "landscape image fits without distortion");
            Check(Images.Fit(new Size(600, 1200), new Size(800, 600)) == new Rectangle(250, 0, 300, 600), "portrait image fits without distortion");
            Check(Images.Fit(Size.Empty, new Size(800, 600)).IsEmpty, "zero-size image handled");
            var paths = new List<string> { "1", "2", "3", "4" }; Images.Shuffle(paths, new Random(123)); paths.Sort();
            Check(String.Join(",", paths) == "1,2,3,4", "shuffle preserves image catalog");
            string bad = Path.Combine(folder, "bad.png"); File.WriteAllText(bad, "invalid"); bool rejected = false;
            try { using (Images.Load(bad, new Size(800, 600))) { } } catch (ArgumentException) { rejected = true; }
            Check(rejected, "corrupt image detected");
        }
        private static void TestWindows(string root)
        {
            L10n.Current = Language.Portuguese;
            Check(TrayApplication.StatusNotification(true) == "Monitoramento automático ativado.", "global shortcut reports enabled state");
            Check(TrayApplication.StatusNotification(false) == "Monitoramento automático desativado.", "global shortcut reports disabled state");
            using (var form = new SettingsForm(new Settings { Language = Language.Portuguese, Action = IdleAction.Slideshow, ImageFolder = @"C:\Imagens\Natureza" }))
            {
                Render(form, Path.Combine(root, "settings-preview.png"));
                Render(form, Path.Combine(root, "settings-dark.png"));
                results.Add("UI: current scale " + form.CurrentAutoScaleDimensions + ", font height " + form.Font.Height + ", client " + form.ClientSize);
                Check(form.Controls.Count > 0 && form.Width >= 652, "settings form renders at current DPI");
                var wait = Find<NumberField>(form, "Tempo de espera");
                Check(wait.Value == 5 && form.ReadDraft().IdleSeconds == 300, "new time input reads saved minutes");
                wait.ValueText = "17";
                string preferencePath = Path.Combine(root, "theme-only.xml");
                Settings persisted = new Settings { IdleSeconds = 300, ImageFolder = "original" }; int requests = 0;
                form.ThemeRequested += delegate(Appearance appearance) { ++requests; var next = persisted.Copy(); next.Appearance = appearance; SettingsStore.Save(next, preferencePath); persisted = next; };
                form.ChangeTheme(Appearance.Light);
                Check(form.CurrentAppearance == Appearance.Light && form.ReadDraft().IdleSeconds == 1020, "theme changes instantly without discarding draft edits");
                string warning;
                var themeSaved = SettingsStore.Load(preferencePath, out warning);
                Check(themeSaved.Appearance == Appearance.Light && themeSaved.IdleSeconds == 300 && themeSaved.ImageFolder == "original", "theme-only save preserves other stored preferences");
                var theme = Find<ThemeToggle>(form, "Tema escuro");
                MouseClick(theme, theme.Width / 4);
                Check(form.CurrentAppearance == Appearance.Light && requests == 1, "clicking selected light segment does not toggle to dark");
                MouseClick(theme, theme.Width * 3 / 4);
                Check(form.CurrentAppearance == Appearance.Dark && requests == 2, "dark segment selects dark exactly once");
                Click(theme);
                Check(form.CurrentAppearance == Appearance.Light && requests == 3, "keyboard-style theme activation toggles once");
                wait.Value = 5;
                Render(form, Path.Combine(root, "settings-light.png"));
                wait.ValueText = "abc"; bool rejected = false;
                try { form.ReadDraft(); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "invalid pasted time is rejected instead of silently saved");
                wait.Value = 24; Click(Find<SoftButton>(form, "Horas"));
                Check(form.ReadDraft().IdleSeconds == 86400, "24-hour upper limit accepted");
                wait.Value = 25; rejected = false;
                try { form.ReadDraft(); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "time above 24 hours rejected");
                wait.Value = 5; Click(Find<SoftButton>(form, "Minutos"));
                Find<ModeTile>(form, "Modo desligar monitores").Checked = true;
                Check(form.ReadDraft().Action == IdleAction.MonitorsOff && !Find<ModeTile>(form, "Modo slideshow").Checked, "mode tiles are mutually exclusive and update saved action");
                form.ChangeTheme(Appearance.Dark);
                Render(form, Path.Combine(root, "settings-dark-monitors.png"));
                form.Size = new Size(900, 610); Render(form, Path.Combine(root, "settings-small.png"));
                var field = Find<NumberField>(form, "Intervalo entre imagens"); field.Value = 2;
                Click(Find<SoftButton>(field, "Diminuir: Intervalo entre imagens"));
                Check(field.Value == 2, "decrement respects image interval minimum");
                form.SetEnabled(false);
                Check(!form.ReadDraft().Enabled, "external automation toggle stays synchronized with open settings");
            }
            using (var setup = new SetupForm()) { Render(setup, Path.Combine(root, "setup-preview.png")); Check(setup.Controls.Count == 7, "installer UI and language picker render"); }
            bool firstHotKeyRegistered;
            using (var input = new InputWindow())
            {
                bool toggled = false; input.ToggleRequested += delegate { toggled = true; };
                Check(Native.PostMessage(input.Handle, Native.WM_HOTKEY, new IntPtr(InputWindow.ToggleHotKeyId + 2), IntPtr.Zero), "unrelated hotkey message can be queued");
                Application.DoEvents();
                Check(!toggled, "unrelated hotkey id is ignored");
                Check(Native.PostMessage(input.Handle, Native.WM_HOTKEY, new IntPtr(InputWindow.ToggleHotKeyId), IntPtr.Zero), "global shortcut message can be queued");
                Application.DoEvents();
                Check(toggled, "global shortcut message requests automation toggle");
                bool offRequested = false; input.MonitorOffRequested += delegate { offRequested = true; };
                Native.PostMessage(input.Handle, Native.WM_HOTKEY, new IntPtr(InputWindow.MonitorOffHotKeyId), IntPtr.Zero); Application.DoEvents();
                Check(offRequested, "F11 native hotkey message requests delayed display-off");
                if (input.MonitorOffHotKeyRegistered)
                {
                    using (var collision = new InputWindow())
                        Check(!collision.MonitorOffHotKeyRegistered && collision.MonitorOffHotKeyRegistrationError == Native.ErrorHotKeyAlreadyRegistered, "F11 collision is nonfatal and reported independently");
                }
                else Check(input.MonitorOffHotKeyRegistrationError == Native.ErrorHotKeyAlreadyRegistered, "F11 registration only unavailable because another app owns it");
                firstHotKeyRegistered = input.HotKeyRegistered;
                if (firstHotKeyRegistered)
                {
                    using (var collision = new InputWindow())
                    {
                        Check(!collision.HotKeyRegistered, "second global shortcut registration is rejected while first is active");
                        Check(collision.HotKeyRegistrationError == Native.ErrorHotKeyAlreadyRegistered, "global shortcut collision reports ERROR_HOTKEY_ALREADY_REGISTERED");
                    }
                }
                else
                {
                    Check(input.HotKeyRegistrationError == Native.ErrorHotKeyAlreadyRegistered, "unavailable global shortcut is already owned, not rejected for invalid parameters");
                    results.Add("SKIP: native hotkey lifecycle is owned by another application in this environment.");
                }
                Check(input.Handle != IntPtr.Zero, "native raw input, hotkey, display and session notifications register");
                bool checkedRequest = false;
                input.AllowMonitorOff = delegate { checkedRequest = true; return false; };
                Native.PostMessage(input.Handle, InputWindow.MonitorOffMessage, IntPtr.Zero, IntPtr.Zero); Application.DoEvents();
                Check(checkedRequest, "queued monitor command rechecks cancellation before touching power");
            }
            if (firstHotKeyRegistered)
                using (var replacement = new InputWindow())
                    Check(replacement.HotKeyRegistered, "global shortcut registers again after owner disposal");
            using (var icon = Theme.MakeIcon()) Check(icon.Width == 32, "tray icon resource lifecycle");
        }
        private static void TestLocalization(string root)
        {
            Check(L10n.Detect(new System.Globalization.CultureInfo("pt-BR")) == Language.Portuguese && L10n.Detect(new System.Globalization.CultureInfo("pt-PT")) == Language.Portuguese, "Portuguese Windows locales select Portuguese");
            Check(L10n.Detect(new System.Globalization.CultureInfo("en-US")) == Language.English && L10n.Detect(new System.Globalization.CultureInfo("de-DE")) == Language.English, "other Windows locales default to English");
            Check(L10n.Duration(1, 0, Language.English) == "1 second" && L10n.Duration(2, 0, Language.Portuguese) == "2 segundos", "duration grammar follows selected language");
            string path = Path.Combine(root, "language-only.xml"), warning;
            var persisted = new Settings { Language = Language.Portuguese, Enabled = false, IdleSeconds = 300, ImageFolder = "Cancelar", Appearance = Appearance.Dark };
            using (var form = new SettingsForm(persisted))
            {
                var wait = Find<NumberField>(form, "Tempo de espera"); wait.ValueText = "abc";
                int requests = 0;
                form.LanguageRequested += delegate(Language language) { requests++; var next = persisted.Copy(); next.Language = language; SettingsStore.Save(next, path); persisted = next; };
                var picker = Find<ComboBox>(form, "Idioma"); picker.SelectedIndex = 0;
                Check(requests == 1 && form.CurrentLanguage == Language.English && wait.ValueText == "abc", "language changes immediately without parsing or discarding invalid draft input");
                Check(picker.AccessibleName == "Language" && Find<NumberField>(form, "Idle timeout") == wait, "language picker and accessibility labels translate");
                Check(Find<SoftButton>(wait, "Decrease: Idle timeout") != null, "numeric control accessibility translates fully");
                Check(Find<TextBox>(form, "Image folder").Text == "Cancelar", "user-entered folder names are never translated");
                var saved = SettingsStore.Load(path, out warning);
                Check(warning == null && saved.Language == Language.English && saved.IdleSeconds == 300 && saved.ImageFolder == "Cancelar" && !saved.Enabled, "language-only save preserves other settings");
                wait.Value = 17; Check(form.ReadDraft().IdleSeconds == 1020 && form.ReadDraft().Language == Language.English, "saving draft retains language and edits");
                Render(form, Path.Combine(root, "settings-en-dark.png"));
                Find<ModeTile>(form, "Slideshow mode").Checked = true;
                form.ChangeTheme(Appearance.Light); Render(form, Path.Combine(root, "settings-en-light.png"));
                picker.SelectedIndex = 1;
                Check(requests == 2 && Find<NumberField>(form, "Tempo de espera") == wait && form.ReadDraft().IdleSeconds == 1020, "switching back restores Portuguese without losing state");
                Render(form, Path.Combine(root, "settings-pt-light.png"));
                form.ChangeTheme(Appearance.Dark); Render(form, Path.Combine(root, "settings-pt-dark.png"));
            }
            File.WriteAllText(path, File.ReadAllText(path).Replace("<Language>Portuguese</Language>", ""));
            var legacy = SettingsStore.Load(path, out warning);
            Check(warning == null && legacy.Language == L10n.Detect(System.Globalization.CultureInfo.CurrentUICulture) && legacy.ImageFolder == "Cancelar" && !legacy.Enabled, "pre-language settings migrate without changing preferences");
            legacy.Language = (Language)999; legacy.Normalize();
            Check(legacy.Language == L10n.Detect(System.Globalization.CultureInfo.CurrentUICulture), "invalid language normalizes safely");
            L10n.Current = Language.English;
            Check(TrayApplication.StatusNotification(true) == "Automatic monitoring enabled.", "English toggle notification");
            using (var setup = new SetupForm())
            {
                Check(setup.Text == "Install Screen Care", "installer opens in selected language");
                Render(setup, Path.Combine(root, "setup-en.png"));
                Find<ComboBox>(setup, "Language").SelectedIndex = 1;
                Check(setup.Text == "Instalar Screen Care", "installer changes language before install");
            }
        }
        private static void TestLifetime(string root)
        {
            using (var app = new TrayApplication(new Settings { Enabled = false, Language = Language.English }, false))
            {
                var type = typeof(TrayApplication);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var open = type.GetMethod("OpenSettings", flags);
                for (int i = 0; i < 3; i++)
                {
                    open.Invoke(app, null);
                    var form = (Form)type.GetField("form", flags).GetValue(app);
                    form.Close(); Application.DoEvents();
                    Check(type.GetField("form", flags).GetValue(app) == null && !(bool)type.GetField("exiting", flags).GetValue(app), "closing settings keeps tray application alive " + i);
                }
                var tray = (NotifyIcon)type.GetField("tray", flags).GetValue(app);
                Check(tray.Visible && tray.ContextMenuStrip.Items[3].Text == "Settings…", "English tray remains visible after settings closure");
                Check(app.TryRecover(new IOException("synthetic failure")), "recoverable UI error pauses automation instead of exiting");
                Check(tray.Visible && !((System.Windows.Forms.Timer)type.GetField("timer", flags).GetValue(app)).Enabled, "recovery keeps tray visible and stops failing timer");
                open.Invoke(app, null);
                var failedForm = (Form)type.GetField("form", flags).GetValue(app);
                Check(app.TryRecover(new IOException("repeated synthetic failure")), "repeated operational error keeps process alive");
                Check(failedForm.IsDisposed && type.GetField("form", flags).GetValue(app) == null, "recovery disposes failing settings UI");
                var notifications = (StatusToastHost)type.GetField("notifications", flags).GetValue(app);
                Check(notifications.Current == null, "repeated recovery removes old notification without creating an error loop");
                Check(app.TryRecover(new UnauthorizedAccessException()) && app.TryRecover(new ArgumentException()) && app.TryRecover(new System.Security.SecurityException()), "permission and UI argument errors remain recoverable");
                Check(tray.Visible && !(bool)type.GetField("exiting", flags).GetValue(app), "successive recoverable failures preserve tray and application lifetime");
                open.Invoke(app, null);
                Check(type.GetField("form", flags).GetValue(app) != null, "settings can reopen after repeated recovery");
                Check(!app.TryRecover(new SEHException()), "native process corruption is not swallowed");
                Check(!app.TryRecover(new OutOfMemoryException()), "fatal resource errors are not treated as recoverable");
                app.Shutdown(); app.Shutdown();
                Check(!tray.Visible, "repeated shutdown safely disposes the tray");
            }
            using (var app = new TrayApplication(new Settings { Enabled = false, Action = IdleAction.Slideshow }, false))
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic; var type = typeof(TrayApplication);
                var input = (InputWindow)type.GetField("input", flags).GetValue(app);
                // Exercise the real timer path but block its queued native power command.
                input.AllowMonitorOff = delegate { return false; };
                var manual = (DelayedAction)type.GetField("manual", flags).GetValue(app);
                // Assert synchronous activation before live desktop input can cancel it.
                manual.Schedule(-3, true); type.GetMethod("Tick", flags).Invoke(app, null);
                Check((IdleAction)type.GetProperty("EffectiveAction", flags).GetValue(app, null) == IdleAction.MonitorsOff, "manual F11 action overrides slideshow even with automation paused");
                var settings = (Settings)type.GetField("settings", flags).GetValue(app);
                Check(!settings.Enabled && settings.Action == IdleAction.Slideshow, "manual monitor-off preserves configured mode and automation preference");
                Check((uint)type.GetField("powerFlags", flags).GetValue(app) == 0x80000001u, "F11 keeps background playback running without requesting display-on");
                type.GetMethod("OnInput", flags).Invoke(app, null);
                Check((uint)type.GetField("powerFlags", flags).GetValue(app) == 0x80000000u, "ending manual monitor-off releases its system-only power request");
                Check((IdleAction)type.GetProperty("EffectiveAction", flags).GetValue(app, null) == IdleAction.Slideshow, "user activity restores configured mode after manual power-off");
                Check(!((MonitorPowerGuard)type.GetField("powerGuard", flags).GetValue(app)).Armed, "user activity cancels monitor guard in running app");
                Application.DoEvents();
            }
            Diagnostics.Error(new InvalidOperationException("synthetic diagnostic"));
            string error = File.ReadAllText(Path.Combine(Diagnostics.DirectoryPath, "last-error.txt"));
            Check(error.Contains("synthetic diagnostic"), "failure is recorded independently of shutdown");
            string log = Path.Combine(Diagnostics.DirectoryPath, "diagnostics.log");
            File.WriteAllText(log, new String('x', 66000)); Diagnostics.Event("rotation-test");
            Check(new FileInfo(log).Length < 200 && File.ReadAllText(log).Contains("rotation-test"), "local diagnostic log is bounded");
            L10n.Current = Language.Portuguese;
        }
        private static void Render(Form form, string path)
        {
            form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.ShowInTaskbar = false;
            form.Opacity = 0;
            IntPtr hwnd = form.Handle;
            SetWindowLongPtr(hwnd, -20, new IntPtr(GetWindowLongPtr(hwnd, -20).ToInt64() | 0x08000000)); // WS_EX_NOACTIVATE
            form.Show(); form.PerformLayout(); Application.DoEvents();
            if (form is SettingsForm) CheckModernLayout(form);
            using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(path, ImageFormat.Png); }
            if (form is SettingsForm)
            {
                // DrawToBitmap does not capture the native DWM title-bar composition.
                using (var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                { form.Controls[0].DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.ChangeExtension(path, ".client.png"), ImageFormat.Png); }
            }
            form.Hide();
        }
        private static T Find<T>(Control root, string name) where T : Control
        {
            if (root is T && root.AccessibleName == name) return (T)root;
            foreach (Control child in root.Controls) { T found = Find<T>(child, name); if (found != null) return found; }
            return null;
        }
        private static void Click(Control control) { control.GetType().GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, new object[] { EventArgs.Empty }); }
        private static void MouseClick(ThemeToggle control, int x)
        { typeof(ThemeToggle).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x, control.Height / 2, 0) }); Click(control); }
        private static void CheckModernLayout(Control root)
        {
            if (root.Visible && (root is Toggle || root is SoftButton || root is NumberField || root is ModeTile))
                if (root.Width < 20 || root.Height < 20 || !root.Parent.ClientRectangle.Contains(root.Bounds)) throw new Exception("Clipped control: " + root.GetType().Name + " " + root.AccessibleName + " " + root.Bounds + " parent=" + root.Parent.ClientRectangle);
            foreach (Control child in root.Controls) CheckModernLayout(child);
        }
        private static int Smoke(string report)
        {
            try
            {
                using (var app = new TrayApplication(new Settings { Enabled = false }, false))
                using (var timer = new System.Windows.Forms.Timer { Interval = 5000 })
                using (var process = Process.GetCurrentProcess())
                {
                    TimeSpan start = TimeSpan.Zero; int ticks = 0;
                    timer.Tick += delegate {
                        ++ticks; process.Refresh();
                        if (ticks == 1) start = process.TotalProcessorTime;
                        if (ticks == 3)
                        {
                            File.WriteAllText(report, "Native tray smoke: PASS\r\nDuration: 15 s; CPU sample: final 10 s\r\nWorking set MiB: " + (process.WorkingSet64 / 1048576.0).ToString("F2") +
                                "\r\nPrivate memory MiB: " + (process.PrivateMemorySize64 / 1048576.0).ToString("F2") + "\r\nCPU seconds: " + (process.TotalProcessorTime - start).TotalSeconds.ToString("F4") +
                                "\r\nAutomation disabled; no settings, startup or monitor-power changes.\r\n");
                            timer.Stop(); app.Shutdown();
                        }
                    };
                    timer.Start(); Application.Run(app);
                }
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(report, ex.ToString()); return 1; }
        }
    }
}
