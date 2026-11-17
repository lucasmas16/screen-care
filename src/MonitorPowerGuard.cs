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
        internal int Wakes { get; private set; }
        internal const int MaxRetries = 3;
        internal const double WakeLimitSeconds = 120;
        private double firstWake = Double.PositiveInfinity;
        private double nextAttempt = Double.PositiveInfinity, lastAttempt;
        private double Delay { get { return Retries == 0 ? 5 : Retries == 1 ? 15 : Retries == 2 ? 30 : 60; } }
        internal void Begin(double now)
        { Armed = true; DisplayOn = null; Retries = Wakes = 0; firstWake = Double.PositiveInfinity; lastAttempt = now; nextAttempt = now + Delay; }
        internal void Reset()
        { Armed = false; DisplayOn = null; Retries = Wakes = 0; firstWake = nextAttempt = Double.PositiveInfinity; }
        internal void Observe(bool on, double now)
        {
            bool changed = DisplayOn != on; DisplayOn = on;
            if (!Armed) return;
            if (on && changed) { Wakes++; if (Double.IsPositiveInfinity(firstWake)) firstWake = now; }
            if (!on) nextAttempt = Double.PositiveInfinity;
            else if (changed) nextAttempt = Math.Max(now + 3, lastAttempt + Delay);
        }
        internal bool TryRetry(double now, bool allowed)
        {
            if (!Armed || !allowed || Retries >= MaxRetries || DisplayOn == false || now < nextAttempt) return false;
            Retries++; lastAttempt = now; nextAttempt = now + Delay;
            return true;
        }
        internal bool ShouldGiveUp(double now)
        {
            // The deadline also applies if a final display-off notification is
            // stale: a cover must never survive indefinitely waiting for input.
            return Armed && (now - firstWake >= WakeLimitSeconds
                || (Retries >= MaxRetries && DisplayOn != false && now >= nextAttempt));
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
