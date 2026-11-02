namespace ScreenCare
{
    internal sealed class SessionState
    {
        internal bool Locked, Disconnected, Suspended;
        private uint lastInput;
        private bool haveInput;
        internal bool Change(int reason)
        {
            bool oldLock = Locked, oldDisconnect = Disconnected;
            if (reason == 7) Locked = true;
            if (reason == 8) Locked = false;
            if (reason == 2 || reason == 4) Disconnected = true;
            if (reason == 1 || reason == 3) Disconnected = false;
            bool changed = oldLock != Locked || oldDisconnect != Disconnected;
            if (changed) haveInput = false;
            return changed;
        }
        internal bool ObserveInput(uint time)
        {
            bool changed = haveInput && time != lastInput;
            haveInput = true; lastInput = time;
            return changed;
        }
        internal bool Allows(IdleAction action, bool interactive, bool settingsOpen)
        {
            if (Suspended || Disconnected) return false;
            if (Locked) return true; // Both modes use monitors-off on the lock screen.
            return interactive && !settingsOpen;
        }
        internal IdleAction EffectiveAction(IdleAction configured)
        { return Locked ? IdleAction.MonitorsOff : configured; }
    }
}
