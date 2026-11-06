using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ScreenCare
{
    // WinMM/DirectInput can terminate a process with native heap corruption.
    // Keep those drivers outside the tray process; exchange only active/idle.
    internal sealed class GamepadProcess : IDisposable
    {
        private readonly Func<ProcessStartInfo> startInfo;
        private readonly object gate = new object();
        private Process child;
        private bool active, disposed;
        private long lastReport;
        private double retryAt, startedAt;
        internal const double RestartDelay = 5, StaleSeconds = 2, HangSeconds = 10;
        internal int ChildId { get { return child == null ? 0 : child.Id; } }

        internal GamepadProcess() : this(delegate {
            return new ProcessStartInfo(Application.ExecutablePath, "--gamepad-worker " + Process.GetCurrentProcess().Id);
        }) { }
        internal GamepadProcess(Func<ProcessStartInfo> factory) { startInfo = factory; }

        internal bool Poll(double now)
        {
            if (disposed) return false;
            try
            {
                if (child != null)
                {
                    double silence;
                    lock (gate) silence = lastReport == 0 ? now - startedAt : (Stopwatch.GetTimestamp() - lastReport) / (double)Stopwatch.Frequency;
                    if (child.HasExited || silence >= HangSeconds)
                    {
                        Diagnostics.Event(child.HasExited ? "gamepad-worker-exited" : "gamepad-worker-unresponsive");
                        StopChild(); retryAt = now + RestartDelay;
                    }
                }
                if (child == null && now >= retryAt)
                {
                    var info = startInfo(); info.UseShellExecute = false; info.CreateNoWindow = true;
                    info.RedirectStandardOutput = true;
                    var next = new Process { StartInfo = info };
                    next.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) {
                        if (e.Data != "0" && e.Data != "1") return;
                        lock (gate)
                        {
                            if (!ReferenceEquals(child, next)) return;
                            active = e.Data == "1"; lastReport = Stopwatch.GetTimestamp();
                        }
                    };
                    child = next; startedAt = now;
                    next.Start(); next.BeginOutputReadLine();
                }
                lock (gate) return active && lastReport != 0
                    && (Stopwatch.GetTimestamp() - lastReport) / (double)Stopwatch.Frequency < StaleSeconds;
            }
            catch (Exception error)
            {
                if (!Recovery.CanHandle(error)) throw;
                Diagnostics.Event("gamepad-worker-start-or-read-failed");
                StopChild(); retryAt = now + RestartDelay; return false;
            }
        }
        private void StopChild()
        {
            Process old;
            lock (gate) { old = child; child = null; active = false; lastReport = 0; }
            if (old == null) return;
            try
            {
                if (!old.HasExited) { old.Kill(); old.WaitForExit(1000); }
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            finally { old.Dispose(); }
        }
        public void Dispose() { disposed = true; StopChild(); }

        internal static int RunWorker(string parentId)
        {
            int id;
            if (!Int32.TryParse(parentId, out id) || id <= 0) return 2;
            try
            {
                using (var parent = Process.GetProcessById(id))
                {
                    var monitor = new GamepadMonitor(); var clock = Stopwatch.StartNew();
                    while (!parent.HasExited)
                    {
                        Console.Out.WriteLine(monitor.Poll(clock.Elapsed.TotalSeconds) ? "1" : "0");
                        Console.Out.Flush(); Thread.Sleep(100);
                    }
                }
                return 0;
            }
            catch (ArgumentException) { return 2; }
            catch (IOException) { return 3; }
        }
    }
}
