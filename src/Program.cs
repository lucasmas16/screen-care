using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Screen Care")]
[assembly: AssemblyDescription("Slideshow e descanso dos monitores por inatividade real")]
[assembly: AssemblyCompany("Screen Care")]
[assembly: AssemblyProduct("Screen Care")]
[assembly: AssemblyVersion("1.4.1.0")]
[assembly: AssemblyFileVersion("1.4.1.0")]
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
            Native.EnableDpi();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
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
                { if (app != null) app.Shutdown(); Report(e.Exception); };
                try
                {
                    string warning;
                    Settings settings = SettingsStore.Load(SettingsStore.DefaultPath, out warning);
                    using (app = new TrayApplication(settings, Array.IndexOf(args, "--background") < 0 || warning != null))
                    { if (warning != null) app.Notify(warning); Application.Run(app); }
                }
                catch (Exception ex) { if (app != null) app.Shutdown(); Report(ex); }
                finally { mutex.ReleaseMutex(); }
            }
        }
        private static void Report(Exception ex)
        {
            try { Directory.CreateDirectory(SettingsStore.DirectoryPath); File.WriteAllText(Path.Combine(SettingsStore.DirectoryPath, "last-error.txt"), DateTime.Now.ToString("s") + Environment.NewLine + ex); } catch { }
            MessageBox.Show("O Screen Care precisou encerrar.\n\n" + ex.Message, "Screen Care", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
