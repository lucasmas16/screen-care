using System;

namespace ScreenCare
{
    // Windows/app wake notifications are not user input. Retry only while the
    // current idle action is active, leaving time for queued user input first.
    internal sealed class MonitorPowerGuard
    {
        internal bool Armed { get; private set; }
        internal bool? DisplayOn { get; private set; }
        internal int Retries { get; private set; }
        private double nextAttempt = Double.PositiveInfinity, lastAttempt;
        private double Delay { get { return Retries == 0 ? 5 : Retries == 1 ? 15 : Retries == 2 ? 30 : 60; } }
        internal void Begin(double now)
        { Armed = true; DisplayOn = null; Retries = 0; lastAttempt = now; nextAttempt = now + Delay; }
        internal void Reset()
        { Armed = false; DisplayOn = null; Retries = 0; nextAttempt = Double.PositiveInfinity; }
        internal void Observe(bool on, double now)
        {
            bool changed = DisplayOn != on; DisplayOn = on;
            if (!Armed) return;
            if (!on) nextAttempt = Double.PositiveInfinity;
            else if (changed) nextAttempt = Math.Max(now + 3, lastAttempt + Delay);
        }
        internal bool TryRetry(double now, bool allowed)
        {
            if (!Armed || !allowed || DisplayOn == false || now < nextAttempt) return false;
            Retries = Math.Min(Retries + 1, 3); lastAttempt = now; nextAttempt = now + Delay;
            return true;
        }
    }

    internal sealed class DelayedAction
    {
        private double due = Double.PositiveInfinity;
        internal bool MonitorsOnly { get; private set; }
        internal bool Pending { get { return !Double.IsPositiveInfinity(due); } }
        internal void Schedule(double now, bool monitorsOnly) { due = now + 2; MonitorsOnly = monitorsOnly; }
        internal void Cancel() { due = Double.PositiveInfinity; }
        internal bool TryTake(double now)
        { if (now < due) return false; due = Double.PositiveInfinity; return true; }
    }
}
