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
        private readonly SessionState session = new SessionState();
        private bool exiting;
        private readonly DelayedAction manual = new DelayedAction();
        private readonly MonitorPowerGuard powerGuard = new MonitorPowerGuard();
        private bool forceMonitorsOff;
        private double lastRecovery = -100;
        private uint powerFlags;
        internal double Now { get { return clock.Elapsed.TotalSeconds; } }
        private IdleAction EffectiveAction { get { return session.EffectiveAction(forceMonitorsOff ? IdleAction.MonitorsOff : settings.Action); } }
        private bool CanTurnOff { get { return !exiting && idle.Active && EffectiveAction == IdleAction.MonitorsOff && session.Allows(EffectiveAction, Native.InteractiveDesktop(), form != null); } }

        internal TrayApplication(Settings value, bool showSettings)
        {
            settings = value; L10n.Current = settings.Language;
            idle = new IdleController(Now);
            input = new InputWindow();
            input.DirectInput += OnInput;
            input.AllowMonitorOff = delegate { return CanTurnOff; };
            input.DisplayChanged += delegate(bool on) {
                if (powerGuard.DisplayOn != on) Diagnostics.Event(on ? "display-on" : "display-off");
                powerGuard.Observe(on, Now);
            };
            input.SessionChanged += delegate(int reason) {
                if (session.Change(reason)) { Reset(); uint time; if (Native.TryLastInput(out time)) session.ObserveInput(time); UpdatePower(); }
            };
            input.Suspended += delegate { session.Suspended = true; Reset(); UpdatePower(); };
            input.Resumed += delegate { session.Suspended = false; Reset(); UpdatePower(); };
            input.DisplayLayoutChanged += delegate {
                if (idle.DisplayLayoutChanged(Now, EffectiveAction == IdleAction.MonitorsOff))
                { manual.Cancel(); powerGuard.Reset(); forceMonitorsOff = false; StopSlides(); UpdatePower(); }
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
            if (idle.Input(Now)) { Diagnostics.Event("idle-ended-by-" + source); StopSlides(); }
            powerGuard.Reset(); forceMonitorsOff = false;
            UpdatePower();
        }
        private void Reset()
        { manual.Cancel(); powerGuard.Reset(); forceMonitorsOff = false; StopSlides(); idle.Reset(Now); }
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
            if (powerGuard.TryRetry(Now, CanTurnOff)) { Diagnostics.Event("monitor-off-retry"); RequestMonitorOff(); }
            if (slides != null) slides.Tick();
        }
        private void Activate()
        {
            if (EffectiveAction == IdleAction.MonitorsOff) { powerGuard.Begin(Now); UpdatePower(); RequestMonitorOff(); return; }
            try
            {
                slides = new Slideshow(settings, delegate { return Now; }, ActionFailed, delegate { Reset(); UpdatePower(); });
                UpdatePower(); slides.Start();
            }
            catch (Exception ex) { ActionFailed(ex.Message); }
        }
        private void ActionFailed(string message)
        { powerGuard.Reset(); forceMonitorsOff = false; StopSlides(); idle.Failed(Now); UpdatePower(); Notify(message); }
        private void RequestMonitorOff()
        {
            notifications.Hide();
            Diagnostics.Event("monitor-off-request");
            // Post to our own top-level window; DefWindowProc executes the standard system command.
            // Never broadcast synchronously to arbitrary (possibly hung) third-party windows.
            if (!Native.PostMessage(input.Handle, InputWindow.MonitorOffMessage, IntPtr.Zero, IntPtr.Zero))
                ActionFailed(L10n.T("O Windows não aceitou o pedido para desligar os monitores."));
        }
        private void StopSlides() { var old = slides; slides = null; if (old != null) old.Dispose(); }
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
            Reset(); settings = next; L10n.Current = settings.Language;
            if (form != null) form.SetEnabled(settings.Enabled);
            UpdateTray(); UpdatePower(); timer.Start();
        }
        internal bool TryRecover(Exception error)
        {
            if (exiting || !Recovery.CanHandle(error)) return false;
            bool showNotice = Now - lastRecovery >= 60;
            lastRecovery = Now;
            try
            {
                timer.Stop(); settings = settings.Copy(); settings.Enabled = false;
                manual.Cancel(); powerGuard.Reset(); forceMonitorsOff = false; idle.Reset(Now);
                // Remove failing UI/timers before doing optional presentation work.
                Recovery.Attempt(StopSlides);
                Recovery.Attempt(notifications.Hide);
                var failedForm = form; form = null;
                if (failedForm != null) Recovery.Attempt(failedForm.Dispose);
                Recovery.Attempt(UpdatePower);
                Recovery.Attempt(UpdateTray);
                Diagnostics.Event("automation-paused-after-error");
                // Throttle notifications, not recovery: a second operational error is no reason to exit.
                if (showNotice) Recovery.Attempt(delegate { Notify(L10n.T("A automação foi pausada após um erro. O app continua aberto. Revise as configurações e ative novamente.")); });
                return true;
            }
            catch (Exception recoveryError) { Diagnostics.Error(recoveryError); return false; }
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
