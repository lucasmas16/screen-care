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
        internal bool LegacyDisabled { get; private set; }
        internal bool WorkerDisabled { get; private set; }
        internal const int HeapCorruption = unchecked((int)0xC0000374);
        private long lastReport;
        private double retryAt, startedAt, lastPoll, graceUntil;
        private int failures;
        internal const double RestartDelay = 5, StaleSeconds = 2, HangSeconds = 10, HealthySeconds = 60;
        // The worker leaves on its own when Windows has leaked too many handles
        // into it (see SlotProbe); that is housekeeping, not a failure.
        internal const int RecycleExitCode = 10;
        internal const uint RecycleHandles = 20000;
        internal double MinRecycleSeconds = 30;
        // A worker that keeps dying is restarted less and less often: a crash loop
        // would otherwise produce a Windows error report every five seconds.
        internal static double Backoff(int consecutive)
        { return consecutive <= 1 ? RestartDelay : consecutive == 2 ? 30 : consecutive == 3 ? 120 : 600; }
        internal int ChildId { get { return child == null ? 0 : child.Id; } }

        internal GamepadProcess()
        {
            startInfo = delegate { return new ProcessStartInfo(Application.ExecutablePath,
                "--gamepad-worker " + Process.GetCurrentProcess().Id + (LegacyDisabled ? " --xinput-only" : "")); };
        }
        internal GamepadProcess(Func<ProcessStartInfo> factory) { startInfo = factory; }

        internal bool Poll(double now)
        {
            if (disposed || WorkerDisabled) return false;
            try
            {
                // After a pause in polling (lock, sleep) old timestamps say nothing
                // about the worker: give it time to report before calling it hung.
                if (now - lastPoll > 3) graceUntil = now + HangSeconds;
                lastPoll = now;
                if (child != null)
                {
                    double silence;
                    lock (gate) silence = lastReport == 0 ? now - startedAt : (Stopwatch.GetTimestamp() - lastReport) / (double)Stopwatch.Frequency;
                    bool exited = child.HasExited;
                    if (exited && child.ExitCode == RecycleExitCode && now - startedAt >= MinRecycleSeconds)
                    { Diagnostics.Event("gamepad-worker-recycled"); StopChild(); failures = 0; retryAt = now; }
                    else if (exited || (silence >= HangSeconds && now >= graceUntil))
                    {
                        failures = now - startedAt >= HealthySeconds ? 1 : failures + 1;
                        if (exited)
                        {
                            Diagnostics.Event("gamepad-worker-exited code=" + unchecked((uint)child.ExitCode).ToString("X8"));
                            ObserveExit(child.ExitCode);
                        }
                        else Diagnostics.Event("gamepad-worker-unresponsive");
                        StopChild(); retryAt = now + Backoff(failures);
                    }
                }
                if (!WorkerDisabled && child == null && now >= retryAt)
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
                StopChild(); retryAt = now + Backoff(++failures); return false;
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
        internal void ObserveExit(int exitCode)
        {
            if (exitCode != HeapCorruption) return;
            // A driver heap failure cannot be recovered inside that process.
            // Try XInput alone once; Raw Input in the tray remains independent.
            if (!LegacyDisabled)
            { LegacyDisabled = true; Diagnostics.Event("gamepad-legacy-disabled-heap-corruption"); }
            else
            { WorkerDisabled = true; Diagnostics.Event("gamepad-worker-disabled-heap-corruption"); }
        }

        internal static int RunWorker(string parentId, bool includeLegacy = true)
        {
            int id;
            if (!Int32.TryParse(parentId, out id) || id <= 0) return 2;
            try
            {
                using (var parent = Process.GetProcessById(id))
                {
                    var monitor = new GamepadMonitor(includeLegacy); var clock = Stopwatch.StartNew();
                    double handleCheck = 60;
                    while (!parent.HasExited)
                    {
                        double now = clock.Elapsed.TotalSeconds;
                        Console.Out.WriteLine(monitor.Poll(now) ? "1" : "0");
                        Console.Out.Flush();
                        if (now >= handleCheck)
                        { handleCheck = now + 60; if (GamepadMonitor.HandleCount() > RecycleHandles) return RecycleExitCode; }
                        Thread.Sleep(100);
                    }
                }
                return 0;
            }
            catch (ArgumentException) { return 2; }
            catch (IOException) { return 3; }
        }
    }
}
