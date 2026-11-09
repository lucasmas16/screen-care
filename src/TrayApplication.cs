using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenCare
{
    internal sealed class TrayApplication : ApplicationContext
    {
        private Settings settings;
        private readonly InputWindow input;
        private readonly NotifyIcon tray;
        private readonly ToolStripMenuItem toggle;
        private readonly Timer timer;
        private readonly GamepadProcess gamepads = new GamepadProcess();
        private readonly StatusToastHost notifications = new StatusToastHost();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly IdleController idle;
        private SettingsForm form;
        private Slideshow slides;
        private Blackout blackout;
        private readonly SessionState session = new SessionState();
        private bool exiting;
        private readonly DelayedAction manual = new DelayedAction();
        private readonly MonitorPowerGuard powerGuard = new MonitorPowerGuard();
        private bool forceMonitorsOff;
        // The slideshow could not run: this idle period uses monitor-off instead.
        private bool fallbackOff;
        private string pendingNotice;
        private double coverCheckAt = Double.PositiveInfinity, lastRetryLog = -1000, quietUntil;
        private Timer resume;
        private bool pausedByError, resumeEnabled;
        private int recoveries;
        private double lastRecovery = -100;
        private uint powerFlags;
        internal double Now { get { return clock.Elapsed.TotalSeconds; } }
        private IdleAction EffectiveAction { get { return session.EffectiveAction(forceMonitorsOff || fallbackOff ? IdleAction.MonitorsOff : settings.Action); } }
        private bool CanTurnOff { get { return !exiting && idle.Active && EffectiveAction == IdleAction.MonitorsOff && session.Allows(EffectiveAction, Native.InteractiveDesktop(), form != null); } }

        internal TrayApplication(Settings value, bool showSettings)
        {
            settings = value; L10n.Current = settings.Language;
            idle = new IdleController(Now);
            input = new InputWindow();
            input.IgnoreSimulatedInput = settings.IgnoreSimulatedInput;
            if (Native.SessionLocked())
            {
                // No lock notification arrives for a lock that predates this process.
                session.Change(7); uint lockedAt; if (Native.TryLastInput(out lockedAt)) session.ObserveInput(lockedAt);
                Diagnostics.Event("started-locked");
            }
            input.DirectInput += OnInput;
            input.AllowMonitorOff = delegate { return CanTurnOff; };
            input.DisplayChanged += delegate(bool on) {
                if (powerGuard.DisplayOn != on && Now >= quietUntil) Diagnostics.Event(on ? "display-on" : "display-off");
                powerGuard.Observe(on, Now);
                // Real input ends the action (and disarms the guard) before Windows
                // reports the display on. Still armed means nothing we detect woke it.
                if (on && powerGuard.Armed) CoverScreens();
            };
            input.SessionChanged += delegate(int reason) {
                if (session.Change(reason)) { Reset(); uint time; if (Native.TryLastInput(out time)) session.ObserveInput(time); UpdatePower(); }
            };
            input.Suspended += delegate { session.Suspended = true; Reset(); UpdatePower(); };
            input.Resumed += delegate { session.Suspended = false; Reset(); UpdatePower(); };
            input.DisplayLayoutChanged += delegate {
                if (idle.DisplayLayoutChanged(Now, EffectiveAction == IdleAction.MonitorsOff))
                { manual.Cancel(); powerGuard.Reset(); forceMonitorsOff = fallbackOff = false; StopSlides(); UpdatePower(); }
                // Monitors waking from DisplayPort sleep reconnect one by one.
                else if (blackout != null) coverCheckAt = Now + 0.5;
            };
            input.ShowRequested += OpenSettings;
            input.ExitRequested += delegate { Diagnostics.Event("exit-request"); Shutdown(); };
            input.ToggleRequested += delegate { Toggle(true); };
            input.MonitorOffRequested += ScheduleMonitorOff;
            var menu = new ContextMenuStrip();
            var heading = new ToolStripMenuItem("Screen Care") { Enabled = false };
            menu.Items.Add(heading); menu.Items.Add(new ToolStripSeparator());
            toggle = new ToolStripMenuItem { ShortcutKeyDisplayString = "Ctrl+Alt+F12" };
            toggle.Click += delegate { Toggle(true); }; menu.Items.Add(toggle);
            var configure = new ToolStripMenuItem("Configurações…") { Tag = "Configurações…" }; configure.Click += delegate { OpenSettings(); }; menu.Items.Add(configure);
            var off = new ToolStripMenuItem { Tag = "Desligar telas em 2 segundos", ShortcutKeyDisplayString = "Ctrl+Alt+F11" };
            off.Click += delegate { ScheduleMonitorOff(); }; menu.Items.Add(off); menu.Items.Add(new ToolStripSeparator());
            var quit = new ToolStripMenuItem("Sair") { Tag = "Sair" }; quit.Click += delegate { Diagnostics.Event("exit-menu"); Shutdown(); }; menu.Items.Add(quit);
            tray = new NotifyIcon { Icon = Theme.MakeIcon(), ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += delegate { OpenSettings(); };
            timer = new Timer { Interval = 100 };
            timer.Tick += delegate { Tick(); };
            UpdateTray(); UpdatePower(); timer.Start();
            if (!input.HotKeyRegistered) Notify(L10n.T("O atalho global Ctrl+Alt+F12 não pôde ser registrado. Ele pode estar em uso por outro aplicativo."));
            if (!input.MonitorOffHotKeyRegistered) Notify(L10n.T("O atalho Ctrl+Alt+F11 está em uso. Use o menu para desligar as telas."));
            if (showSettings)
            {
                try { OpenSettings(); }
                catch (Exception error) { Diagnostics.Error(error); if (!TryRecover(error)) throw; }
            }
        }

        private void OnInput()
        { OnInputFrom(input.LastInputSource ?? "direct"); }
        private void OnInputFrom(string source)
        {
            if (exiting) return;
            bool ended = idle.Input(Now);
            if (ended) { Diagnostics.Event("idle-ended-by-" + source); StopSlides(); }
            powerGuard.Reset(); forceMonitorsOff = fallbackOff = false;
            UpdatePower();
            // Explain the fallback once the person is back to read it.
            if (ended && pendingNotice != null) { string notice = pendingNotice; pendingNotice = null; Notify(notice); }
        }
        private void Reset()
        { manual.Cancel(); powerGuard.Reset(); forceMonitorsOff = fallbackOff = false; StopSlides(); idle.Reset(Now); }
        private void ScheduleMonitorOff()
        {
            if (form != null) form.Close();
            Reset(); manual.Schedule(Now, true);
            Notify(L10n.T("As telas serão desligadas em 2 segundos."));
        }
        private void Tick()
        {
            if (exiting) return;
            if (!session.Locked && !session.Suspended && !session.Disconnected)
            {
                if (gamepads.Poll(Now)) OnInputFrom("gamepad");
                if (input.SwitchHeld) OnInputFrom("switch-pro");
            }
            if (session.Locked)
            {
                uint time;
                if (Native.TryLastInput(out time) && session.ObserveInput(time)) OnInputFrom("locked-session-input");
            }
            if (!session.Allows(settings.Action, Native.InteractiveDesktop(), false))
            {
                // Never show images on the secure desktop; monitors-off is allowed when locked.
                Reset(); SetPower(0x80000000); return;
            }
            UpdatePower();
            if (manual.TryTake(Now))
            { forceMonitorsOff = manual.MonitorsOnly; idle.Force(Now); Activate(); }
            else if (!manual.Pending && idle.TryActivate(Now, settings.IdleSeconds, settings.Enabled, !session.Allows(settings.Action, true, form != null))) Activate();
            if (powerGuard.TryRetry(Now, CanTurnOff)) RequestMonitorOff(true);
            if (blackout != null && Now >= coverCheckAt) { coverCheckAt = Double.PositiveInfinity; RefreshCover(); }
            if (slides != null) slides.Tick();
        }
        private void Activate()
        {
            if (EffectiveAction == IdleAction.MonitorsOff) { powerGuard.Begin(Now); UpdatePower(); RequestMonitorOff(false); return; }
            try
            {
                slides = new Slideshow(settings, delegate { return Now; }, SlideshowFailed, delegate { Reset(); UpdatePower(); });
                UpdatePower(); slides.Start();
            }
            catch (Exception ex) { SlideshowFailed(ex.Message); }
        }
        // A missing folder or unreadable images must not leave a static screen on:
        // retrying every minute did exactly that. Turn the monitors off instead.
        private void SlideshowFailed(string message)
        {
            StopSlides();
            if (exiting || !idle.Active) return;
            Diagnostics.Event("slideshow-failed-monitors-off");
            pendingNotice = message; fallbackOff = true;
            powerGuard.Begin(Now); UpdatePower(); RequestMonitorOff(false);
        }
        private void ActionFailed(string message)
        { powerGuard.Reset(); forceMonitorsOff = fallbackOff = false; StopSlides(); idle.Failed(Now); UpdatePower(); Notify(message); }
        private void RequestMonitorOff(bool retry)
        {
            notifications.Hide();
            // A display that keeps waking is retried every minute for as long as it
            // lasts. Record the first retries and then one cycle every ten minutes,
            // so start/exit history is not pushed out of the bounded log.
            bool record = !retry || powerGuard.Retries < 3 || Now - lastRetryLog >= 600;
            if (!record) quietUntil = Now + 5;
            else
            {
                if (retry) { lastRetryLog = Now; Diagnostics.Event("monitor-off-retry"); }
                Diagnostics.Event("monitor-off-request");
            }
            // Post to our own top-level window; DefWindowProc executes the standard system command.
            // Never broadcast synchronously to arbitrary (possibly hung) third-party windows.
            if (!Native.PostMessage(input.Handle, InputWindow.MonitorOffMessage, IntPtr.Zero, IntPtr.Zero))
                ActionFailed(L10n.T("O Windows não aceitou o pedido para desligar os monitores."));
        }
        private void CoverScreens()
        {
            if (exiting || blackout != null || slides != null || !CanTurnOff || !Native.InteractiveDesktop()) return;
            Diagnostics.Event("display-woke-without-input");
            try { blackout = new Blackout(delegate { OnInputFrom("cover-pointer"); }); coverCheckAt = Now + 2; }
            catch (Exception error)
            {
                if (!Recovery.CanHandle(error)) throw;
                Diagnostics.Event("cover-failed");
            }
        }
        // Rebuild the cover when the monitor arrangement no longer matches it.
        private void RefreshCover()
        {
            if (blackout == null || blackout.Covers(Screen.AllScreens)) return;
            Diagnostics.Event("cover-relayout");
            var old = blackout; blackout = null; old.Dispose();
            try { blackout = new Blackout(delegate { OnInputFrom("cover-pointer"); }); }
            catch (Exception error)
            {
                if (!Recovery.CanHandle(error)) throw;
                Diagnostics.Event("cover-failed");
            }
        }
        // Ends every screen presentation: slideshow and the black cover.
        private void StopSlides()
        {
            var old = slides; slides = null; if (old != null) old.Dispose();
            var cover = blackout; blackout = null; if (cover != null) cover.Dispose();
        }
        private void SetPower(uint flags)
        {
            if (powerFlags != flags && Native.SetThreadExecutionState(flags) != 0)
            {
                powerFlags = flags;
                Diagnostics.Event(flags == 0x80000003 ? "power-keep-display" : flags == 0x80000001 ? "power-keep-system" : "power-released");
            }
        }
        private void UpdatePower()
        {
            // Own the wait interval while enabled, and keep a visible slideshow on screen.
            // Automatic monitor-off restores the normal sleep policy. Explicit F11
            // keeps only the system awake until input/cancel, allowing background audio.
            bool hold = !session.Suspended && !session.Disconnected
                && (settings.Enabled || idle.Active) && !(idle.Active && EffectiveAction == IdleAction.MonitorsOff);
            bool manualOff = !session.Suspended && !session.Disconnected && idle.Active && forceMonitorsOff;
            SetPower(hold ? 0x80000003u : manualOff ? 0x80000001u : 0x80000000u);
        }
        private void Toggle(bool notifyStatus)
        {
            Settings next = settings.Copy(); next.Enabled = !next.Enabled;
            try
            {
                Apply(next);
                if (notifyStatus) notifications.ShowStatus(settings.Enabled, settings.Appearance);
            }
            catch (Exception ex) { Notify(L10n.T("Não foi possível salvar. ") + ex.Message); }
        }
        internal static string StatusNotification(bool enabled)
        { return L10n.T(enabled ? "Monitoramento automático ativado." : "Monitoramento automático desativado."); }
        private void Apply(Settings next)
        {
            bool startupChanged = settings.StartWithWindows != next.StartWithWindows;
            // Refresh an enabled startup entry if this executable moved after installation.
            if (startupChanged || next.StartWithWindows) Startup.Set(next.StartWithWindows, Application.ExecutablePath);
            try { SettingsStore.Save(next, SettingsStore.DefaultPath); }
            catch { if (startupChanged) Startup.Set(settings.StartWithWindows, Application.ExecutablePath); throw; }
            pausedByError = false; if (resume != null) resume.Stop();
            pendingNotice = null;
            Reset(); settings = next; L10n.Current = settings.Language;
            input.IgnoreSimulatedInput = settings.IgnoreSimulatedInput;
            if (form != null) form.SetEnabled(settings.Enabled);
            UpdateTray(); UpdatePower(); timer.Start();
        }
        internal bool TryRecover(Exception error)
        {
            if (exiting || !Recovery.CanHandle(error)) return false;
            bool showNotice = Now - lastRecovery >= 60;
            recoveries = Now - lastRecovery >= 3600 ? 1 : recoveries + 1;
            lastRecovery = Now;
            try
            {
                timer.Stop();
                if (!pausedByError) { pausedByError = true; resumeEnabled = settings.Enabled; }
                settings = settings.Copy(); settings.Enabled = false;
                manual.Cancel(); powerGuard.Reset(); forceMonitorsOff = fallbackOff = false; idle.Reset(Now);
                // Remove failing UI/timers before doing optional presentation work.
                Recovery.Attempt(StopSlides);
                Recovery.Attempt(notifications.Hide);
                var failedForm = form; form = null;
                if (failedForm != null) Recovery.Attempt(failedForm.Dispose);
                Recovery.Attempt(UpdatePower);
                Recovery.Attempt(UpdateTray);
                Diagnostics.Event("automation-paused-after-error");
                // Nobody may be there to re-enable it: try again later, backing off while errors repeat.
                ScheduleResume(ResumeDelay(recoveries));
                // Throttle notifications, not recovery: a second operational error is no reason to exit.
                if (showNotice) Recovery.Attempt(delegate { Notify(L10n.T("A automação foi pausada após um erro e será retomada automaticamente em alguns minutos. O app continua aberto.")); });
                return true;
            }
            catch (Exception recoveryError) { Diagnostics.Error(recoveryError); return false; }
        }
        internal static int ResumeDelay(int consecutive)
        { return consecutive <= 1 ? 60 : consecutive == 2 ? 300 : consecutive == 3 ? 900 : 3600; }
        private void ScheduleResume(int seconds)
        {
            if (resume == null) { resume = new Timer(); resume.Tick += delegate { ResumeAfterError(); }; }
            resume.Stop(); resume.Interval = seconds * 1000; resume.Start();
        }
        private void ResumeAfterError()
        {
            if (resume != null) resume.Stop();
            if (exiting || !pausedByError) return;
            pausedByError = false;
            settings = settings.Copy(); settings.Enabled = resumeEnabled;
            idle.Reset(Now); UpdateTray(); UpdatePower(); timer.Start();
            Diagnostics.Event("automation-resumed-after-error");
        }
        private void UpdateTray()
        {
            foreach (ToolStripItem item in tray.ContextMenuStrip.Items)
                if (item.Tag is string) item.Text = L10n.T((string)item.Tag);
            tray.ContextMenuStrip.Renderer = new ThemeMenuRenderer(new Palette(settings.Appearance));
            toggle.Text = settings.Enabled ? L10n.T("Pausar ação automática") : L10n.T("Ativar ação automática");
            toggle.Checked = settings.Enabled;
            tray.Text = "Screen Care — " + (settings.Enabled ? (settings.Action == IdleAction.MonitorsOff ? L10n.T("Desligar monitores") : "Slideshow") : L10n.T("Pausado"));
        }
        internal void Notify(string text)
        { notifications.ShowMessage(text, settings.Appearance); }
        private void OpenSettings()
        {
            if (exiting) return;
            Reset(); UpdatePower();
            if (form != null) { if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal; form.Show(); form.Activate(); return; }
            form = new SettingsForm(settings);
            form.ThemeRequested += delegate(Appearance appearance) {
                Settings next = settings.Copy(); next.Appearance = appearance;
                SettingsStore.Save(next, SettingsStore.DefaultPath); settings = next; UpdateTray();
            };
            form.LanguageRequested += delegate(Language language) {
                Settings next = settings.Copy(); next.Language = language;
                SettingsStore.Save(next, SettingsStore.DefaultPath); settings = next; L10n.Current = language;
                notifications.Hide(); UpdateTray();
            };
            form.SaveRequested += Apply;
            var opened = form;
            form.FormClosed += delegate { form = null; opened.Dispose(); if (!exiting) { Reset(); UpdatePower(); } };
            form.Show();
        }
        internal void Shutdown()
        {
            if (exiting) return;
            exiting = true;
            Diagnostics.Event("shutdown");
            try
            {
                Recovery.Attempt(timer.Dispose);
                if (resume != null) Recovery.Attempt(resume.Dispose);
                Recovery.Attempt(gamepads.Dispose);
                Recovery.Attempt(StopSlides);
                Recovery.Attempt(notifications.Dispose);
                var oldForm = form; form = null;
                if (oldForm != null) Recovery.Attempt(oldForm.Dispose);
                Recovery.Attempt(delegate { SetPower(0x80000000); });
                Recovery.Attempt(delegate { tray.Visible = false; });
                Recovery.Attempt(tray.Icon.Dispose);
                Recovery.Attempt(tray.ContextMenuStrip.Dispose);
                Recovery.Attempt(tray.Dispose);
                Recovery.Attempt(input.Dispose);
            }
            finally { ExitThread(); }
        }
        protected override void Dispose(bool disposing) { if (disposing) Shutdown(); base.Dispose(disposing); }
    }
}
