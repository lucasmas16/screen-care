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
        private readonly GamepadMonitor gamepads = new GamepadMonitor();
        private readonly StatusToastHost notifications = new StatusToastHost();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly IdleController idle;
        private SettingsForm form;
        private Slideshow slides;
        private readonly SessionState session = new SessionState();
        private bool exiting;
        private double manualAt = -1;
        private uint powerFlags;
        internal double Now { get { return clock.Elapsed.TotalSeconds; } }

        internal TrayApplication(Settings value, bool showSettings)
        {
            settings = value;
            idle = new IdleController(Now);
            input = new InputWindow();
            input.DirectInput += OnInput;
            input.AllowMonitorOff = delegate { return !exiting && idle.Active && session.EffectiveAction(settings.Action) == IdleAction.MonitorsOff; };
            input.SessionChanged += delegate(int reason) {
                if (session.Change(reason)) { Reset(); uint time; if (Native.TryLastInput(out time)) session.ObserveInput(time); UpdatePower(); }
            };
            input.Suspended += delegate { session.Suspended = true; Reset(); UpdatePower(); };
            input.Resumed += delegate { session.Suspended = false; Reset(); UpdatePower(); };
            input.DisplayLayoutChanged += delegate {
                if (idle.DisplayLayoutChanged(Now, session.EffectiveAction(settings.Action) == IdleAction.MonitorsOff))
                { manualAt = -1; StopSlides(); UpdatePower(); }
            };
            input.ShowRequested += OpenSettings;
            input.ExitRequested += Shutdown;
            input.ToggleRequested += delegate { Toggle(true); };
            var menu = new ContextMenuStrip();
            var heading = new ToolStripMenuItem("Screen Care") { Enabled = false };
            menu.Items.Add(heading); menu.Items.Add(new ToolStripSeparator());
            toggle = new ToolStripMenuItem { ShortcutKeyDisplayString = "Ctrl+Alt+F12" };
            toggle.Click += delegate { Toggle(true); }; menu.Items.Add(toggle);
            var configure = new ToolStripMenuItem("Configurações…"); configure.Click += delegate { OpenSettings(); }; menu.Items.Add(configure);
            var now = new ToolStripMenuItem("Iniciar agora (em 2 segundos)");
            now.Click += delegate {
                if (form != null) form.Close();
                Reset(); manualAt = Now + 2;
                Notify("A ação começará em 2 segundos. Use teclado ou mouse depois para retornar.");
            };
            menu.Items.Add(now); menu.Items.Add(new ToolStripSeparator());
            var quit = new ToolStripMenuItem("Sair"); quit.Click += delegate { Shutdown(); }; menu.Items.Add(quit);
            tray = new NotifyIcon { Icon = Theme.MakeIcon(), ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += delegate { OpenSettings(); };
            timer = new Timer { Interval = 100 };
            timer.Tick += delegate { Tick(); };
            UpdateTray(); UpdatePower(); timer.Start();
            if (!input.HotKeyRegistered) Notify("O atalho global Ctrl+Alt+F12 não pôde ser registrado. Ele pode estar em uso por outro aplicativo.");
            if (showSettings) OpenSettings();
        }

        private void OnInput()
        {
            if (exiting) return;
            if (idle.Input(Now)) StopSlides();
            UpdatePower();
        }
        private void Reset()
        { manualAt = -1; StopSlides(); idle.Reset(Now); }
        private void Tick()
        {
            if (exiting) return;
            if (!session.Locked && !session.Suspended && !session.Disconnected && gamepads.Poll(Now)) OnInput();
            if (session.Locked)
            {
                uint time;
                if (Native.TryLastInput(out time) && session.ObserveInput(time)) OnInput();
            }
            if (!session.Allows(settings.Action, Native.InteractiveDesktop(), false))
            {
                // Never show images on the secure desktop; monitors-off is allowed when locked.
                StopSlides(); idle.Reset(Now); manualAt = -1; SetPower(0x80000000); return;
            }
            UpdatePower();
            if (manualAt >= 0 && Now >= manualAt)
            { manualAt = -1; idle.Force(Now); Activate(); }
            else if (manualAt < 0 && idle.TryActivate(Now, settings.IdleSeconds, settings.Enabled, !session.Allows(settings.Action, true, form != null))) Activate();
            // One request per idle period. Repeated requests fight driver/application
            // wakeups and cause a visible off/on loop. Only input/session changes rearm.
            if (slides != null) slides.Tick();
        }
        private void Activate()
        {
            if (session.EffectiveAction(settings.Action) == IdleAction.MonitorsOff) { UpdatePower(); RequestMonitorOff(); return; }
            try
            {
                slides = new Slideshow(settings, delegate { return Now; }, ActionFailed, delegate { Reset(); UpdatePower(); });
                UpdatePower(); slides.Start();
            }
            catch (Exception ex) { ActionFailed(ex.Message); }
        }
        private void ActionFailed(string message)
        { StopSlides(); idle.Failed(Now); UpdatePower(); Notify(message); }
        private void RequestMonitorOff()
        {
            notifications.Hide();
            // Post to our own top-level window; DefWindowProc executes the standard system command.
            // Never broadcast synchronously to arbitrary (possibly hung) third-party windows.
            if (!Native.PostMessage(input.Handle, InputWindow.MonitorOffMessage, IntPtr.Zero, IntPtr.Zero))
                ActionFailed("O Windows não aceitou o pedido para desligar os monitores.");
        }
        private void StopSlides() { if (slides != null) { slides.Dispose(); slides = null; } }
        private void SetPower(uint flags)
        {
            if (powerFlags != flags && Native.SetThreadExecutionState(flags) != 0) powerFlags = flags;
        }
        private void UpdatePower()
        {
            // Own the wait interval while enabled, and keep a visible slideshow on screen.
            // After monitors-off, restore the Windows power plan (including automatic sleep).
            bool hold = !session.Suspended && !session.Disconnected
                && (settings.Enabled || idle.Active) && !(idle.Active && session.EffectiveAction(settings.Action) == IdleAction.MonitorsOff);
            SetPower(hold ? 0x80000003 : 0x80000000);
        }
        private void Toggle(bool notifyStatus)
        {
            Settings next = settings.Copy(); next.Enabled = !next.Enabled;
            try
            {
                Apply(next);
                if (notifyStatus) notifications.ShowStatus(settings.Enabled, settings.Appearance);
            }
            catch (Exception ex) { Notify("Não foi possível salvar. " + ex.Message); }
        }
        internal static string StatusNotification(bool enabled)
        { return "Monitoramento automático " + (enabled ? "ativado." : "desativado."); }
        private void Apply(Settings next)
        {
            bool startupChanged = settings.StartWithWindows != next.StartWithWindows;
            // Refresh an enabled startup entry if this executable moved after installation.
            if (startupChanged || next.StartWithWindows) Startup.Set(next.StartWithWindows, Application.ExecutablePath);
            try { SettingsStore.Save(next, SettingsStore.DefaultPath); }
            catch { if (startupChanged) Startup.Set(settings.StartWithWindows, Application.ExecutablePath); throw; }
            Reset(); settings = next;
            if (form != null) form.SetEnabled(settings.Enabled);
            UpdateTray(); UpdatePower();
        }
        private void UpdateTray()
        {
            tray.ContextMenuStrip.Renderer = new ThemeMenuRenderer(new Palette(settings.Appearance));
            toggle.Text = settings.Enabled ? "Pausar ação automática" : "Ativar ação automática";
            toggle.Checked = settings.Enabled;
            tray.Text = "Screen Care — " + (settings.Enabled ? (settings.Action == IdleAction.MonitorsOff ? "Desligar monitores" : "Slideshow") : "Pausado");
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
            form.SaveRequested += Apply;
            form.FormClosed += delegate { form.Dispose(); form = null; idle.Reset(Now); UpdatePower(); };
            form.Show();
        }
        internal void Shutdown()
        {
            if (exiting) return;
            exiting = true;
            timer.Stop(); timer.Dispose();
            StopSlides();
            notifications.Dispose();
            if (form != null) form.Close();
            SetPower(0x80000000);
            tray.Visible = false; tray.Icon.Dispose(); tray.ContextMenuStrip.Dispose(); tray.Dispose();
            input.Dispose(); ExitThread();
        }
        protected override void Dispose(bool disposing) { if (disposing) Shutdown(); base.Dispose(disposing); }
    }
}
