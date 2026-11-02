using System;

namespace ScreenCare
{
    // Pure state machine: the clock is monotonic and all calls run on the UI thread.
    internal sealed class IdleController
    {
        internal double LastActivity { get; private set; }
        internal bool Active { get; private set; }
        private double retryAfter;
        internal IdleController(double now) { Reset(now); }
        internal void Reset(double now) { LastActivity = now; Active = false; retryAfter = 0; }
        internal bool Input(double now)
        {
            bool wasActive = Active;
            Reset(now);
            return wasActive;
        }
        internal bool TryActivate(double now, int seconds, bool enabled, bool blocked)
        {
            if (!enabled || blocked || Active || now < retryAfter || now - LastActivity < seconds) return false;
            Active = true;
            return true;
        }
        internal void Force(double now) { LastActivity = now; Active = true; retryAfter = 0; }
        internal bool DisplayLayoutChanged(double now, bool monitorsOff)
        {
            // DisplayPort deep sleep can look like an unplug. Do not reassert
            // ES_DISPLAY_REQUIRED in response to our own power-off operation.
            if (Active && monitorsOff) return false;
            Reset(now); return true;
        }
        internal void Failed(double now) { Active = false; retryAfter = now + 60; }
    }
}
