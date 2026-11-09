using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Screen Care")]
[assembly: AssemblyDescription("Reduce static-image exposure with activity-based monitor standby and slideshows")]
[assembly: AssemblyCompany("Screen Care")]
[assembly: AssemblyProduct("Screen Care")]
[assembly: AssemblyVersion("1.5.8.0")]
[assembly: AssemblyFileVersion("1.5.8.0")]
// Every native dependency is a Windows component. Never search the working
// directory or the portable application's directory for these DLLs.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace ScreenCare
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--gamepad-worker")
            { Environment.ExitCode = GamepadProcess.RunWorker(args[1]); return; }
            Native.EnableDpi();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) {
                var error = e.ExceptionObject as Exception;
                if (error != null) Diagnostics.Error(error);
                Diagnostics.Event("unhandled-process-error");
            };
            if (Array.IndexOf(args, "--quit") >= 0)
            { Native.PostMessage(new IntPtr(0xFFFF), InputWindow.ExitMessage, IntPtr.Zero, IntPtr.Zero); return; }
            bool owned;
            using (var mutex = new Mutex(true, "Local\\MonitorPausa." + WindowsIdentity.GetCurrent().User.Value, out owned))
            {
                if (!owned)
                { if (Array.IndexOf(args, "--background") < 0) Native.PostMessage(new IntPtr(0xFFFF), InputWindow.ShowMessage, IntPtr.Zero, IntPtr.Zero); return; }
                TrayApplication app = null;
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    Diagnostics.Error(e.Exception);
                    if (app != null && app.TryRecover(e.Exception)) return;
                    Diagnostics.Event("fatal-ui-error");
                    try { if (app != null) app.Shutdown(); } finally { Report(e.Exception); }
                };
                try
                {
                    string warning;
                    Settings settings = SettingsStore.Load(SettingsStore.DefaultPath, out warning);
                    Language selected = Array.IndexOf(args, "--language=en") >= 0 ? Language.English : Array.IndexOf(args, "--language=pt-BR") >= 0 ? Language.Portuguese : settings.Language;
                    L10n.Current = selected;
                    if (settings.Language != selected) { settings.Language = selected; SettingsStore.Save(settings, SettingsStore.DefaultPath); }
                    Diagnostics.Event("start-1.5.8");
                    using (app = new TrayApplication(settings, Array.IndexOf(args, "--background") < 0 || warning != null))
                    { if (warning != null) app.Notify(L10n.T(warning)); Application.Run(app); }
                }
                catch (Exception ex) { Diagnostics.Error(ex); Diagnostics.Event("startup-or-loop-error"); try { if (app != null) app.Shutdown(); } finally { Report(ex); } }
                finally { mutex.ReleaseMutex(); }
            }
        }
        private static void Report(Exception ex)
        {
            Diagnostics.Error(ex);
            MessageBox.Show(L10n.T("O Screen Care precisou encerrar.\n\n") + ex.Message, "Screen Care", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
