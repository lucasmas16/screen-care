using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ScreenCare
{
    internal static class SetupProgram
    {
        internal const string Marker = "MonitorPausa-install-92004874-248e-4054-a65c-b40d10b298ba";
        internal static readonly string InstallDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "MonitorPausa");
        internal static readonly string ShortcutDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "MonitorPausa");
        internal const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\MonitorPausa";
        internal static readonly string[] OwnedFiles = { "ScreenCare.exe", "ScreenCare.exe.config", "LEIA-ME.txt", "LICENSE", "Uninstall.exe", ".monitorpausa-install" };
        internal static readonly string[] LegacyFiles = { "MonitorPausa.exe", "MonitorPausa.exe.config" };
        internal static readonly string[] ShortcutFiles = { "Screen Care.lnk", "Desinstalar Screen Care.lnk", "MonitorPausa.lnk", "Desinstalar MonitorPausa.lnk" };

        [STAThread]
        private static void Main(string[] args)
        {
            string settingsWarning; L10n.Current = SettingsStore.Load(SettingsStore.DefaultPath, out settingsWarning).Language;
            Native.EnableDpi(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                if (args.Length == 2 && args[0] == "--verify-payload") { VerifyPayload(args[1]); return; }
                if (args.Length == 1 && args[0] == "--uninstall") { LaunchUninstall(); return; }
                if (args.Length == 2 && args[0] == "--remove")
                {
                    int pid;
                    if (!Int32.TryParse(args[1], out pid)) return;
                    try { using (var parent = Process.GetProcessById(pid)) { if (!parent.WaitForExit(15000)) throw new IOException(L10n.T("O desinstalador anterior ainda está aberto.")); } } catch (ArgumentException) { }
                    Remove();
                    MessageBox.Show(L10n.T("Screen Care desinstalado. Suas configurações foram preservadas."), "Screen Care", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                bool owned;
                using (var mutex = new Mutex(true, "Local\\MonitorPausa.Setup", out owned))
                {
                    if (!owned) { MessageBox.Show(L10n.T("O instalador já está aberto."), "Screen Care"); return; }
                    try { Application.Run(new SetupForm()); } finally { mutex.ReleaseMutex(); }
                }
            }
            catch (Exception ex)
            {
                if (args.Length == 2 && args[0] == "--verify-payload") { Environment.ExitCode = 1; return; }
                MessageBox.Show(L10n.T("Não foi possível concluir.\n\n") + ex.Message, "Screen Care", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1;
            }
        }
        internal static void ValidateInstallDirectory()
        {
            ValidatePaths(InstallDirectory, ShortcutDirectory);
            if (Directory.Exists(InstallDirectory) && Directory.GetFileSystemEntries(InstallDirectory).Length > 0 &&
                (!File.Exists(Path.Combine(InstallDirectory, ".monitorpausa-install")) || File.ReadAllText(Path.Combine(InstallDirectory, ".monitorpausa-install")) != Marker))
                throw new IOException(L10n.T("A pasta de destino já existe e não pertence a esta instalação: ") + InstallDirectory);
        }
        internal static void ValidatePaths(string install, string shortcuts)
        {
            SafePaths.RequireNoLinks(install);
            SafePaths.RequireNoLinks(shortcuts);
            foreach (string name in OwnedFiles) SafePaths.RequireNoLinks(Path.Combine(install, name));
            foreach (string name in LegacyFiles) SafePaths.RequireNoLinks(Path.Combine(install, name));
            foreach (string name in ShortcutFiles) SafePaths.RequireNoLinks(Path.Combine(shortcuts, name));
        }
        internal static void StopInstalledApp()
        {
            // Also stop a portable copy in this session before opening the installed version.
            Native.PostMessage(new IntPtr(0xFFFF), InputWindow.ExitMessage, IntPtr.Zero, IntPtr.Zero);
            var matching = new List<Process>();
            try
            {
                foreach (string processName in new[] { "ScreenCare", "MonitorPausa" })
                foreach (Process process in Process.GetProcessesByName(processName))
                {
                    bool match = false;
                    try { match = String.Equals(process.MainModule.FileName, Path.Combine(InstallDirectory, processName + ".exe"), StringComparison.OrdinalIgnoreCase); }
                    catch (Exception) { }
                    if (match) matching.Add(process); else process.Dispose();
                }
                if (matching.Count > 0)
                {
                    foreach (Process process in matching)
                        if (!process.WaitForExit(10000)) throw new IOException(L10n.T("Feche o Screen Care pelo ícone perto do relógio e tente novamente."));
                }
            }
            finally { foreach (Process process in matching) process.Dispose(); }
        }
        internal static void Extract(string directory)
        {
            SafePaths.RequireNoLinks(directory);
            Directory.CreateDirectory(directory);
            foreach (string name in new[] { "ScreenCare.exe", "ScreenCare.exe.config", "LEIA-ME.txt", "LICENSE" })
            {
                using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload." + name))
                {
                    if (source == null) throw new IOException(L10n.T("Arquivo ausente no instalador: ") + name);
                    using (var destination = new FileStream(Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write)) source.CopyTo(destination);
                }
            }
        }
        internal static void VerifyPayload(string directory)
        {
            string target = Path.GetFullPath(directory);
            if (Directory.Exists(target) && Directory.GetFileSystemEntries(target).Length > 0) throw new IOException(L10n.T("Use uma pasta de teste vazia."));
            Extract(target);
            string executable = Path.Combine(target, "ScreenCare.exe");
            AssemblyName identity = AssemblyName.GetAssemblyName(executable);
            if (identity.Name != "ScreenCare") throw new IOException(L10n.T("Identidade inválida."));
            File.WriteAllText(Path.Combine(target, "payload-ok.txt"), identity.FullName);
        }
        internal static void Install()
        {
            ValidateInstallDirectory(); StopInstalledApp();
            Directory.CreateDirectory(InstallDirectory);
            string stage = Path.Combine(InstallDirectory, ".stage-" + Guid.NewGuid().ToString("N"));
            SafePaths.RequireNoLinks(stage);
            Directory.CreateDirectory(stage);
            var previous = new Dictionary<string, byte[]>();
            var registryPrevious = SnapshotRegistry(UninstallKey);
            var shortcutPrevious = new Dictionary<string, byte[]>();
            string runPrevious;
            using (var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) runPrevious = run == null ? null : run.GetValue("MonitorPausa") as string;
            try
            {
                Extract(stage);
                File.Copy(Application.ExecutablePath, Path.Combine(stage, "Uninstall.exe"));
                File.WriteAllText(Path.Combine(stage, ".monitorpausa-install"), Marker);
                InstallFiles(InstallDirectory, stage, previous);
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKey))
                {
                    key.SetValue("DisplayName", "Screen Care"); key.SetValue("DisplayVersion", "1.5.0");
                    key.SetValue("Publisher", "Screen Care"); key.SetValue("InstallLocation", InstallDirectory);
                    key.SetValue("DisplayIcon", Path.Combine(InstallDirectory, "ScreenCare.exe"));
                    key.SetValue("UninstallString", "\"" + Path.Combine(InstallDirectory, "Uninstall.exe") + "\" --uninstall");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord); key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                    long size = 0; foreach (string name in OwnedFiles) size += new FileInfo(Path.Combine(InstallDirectory, name)).Length;
                    key.SetValue("EstimatedSize", (int)((size + 1023) / 1024), RegistryValueKind.DWord);
                }
                Directory.CreateDirectory(ShortcutDirectory);
                foreach (string name in ShortcutFiles)
                {
                    string path = Path.Combine(ShortcutDirectory, name);
                    SafePaths.RequireNoLinks(path);
                    shortcutPrevious[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
                }
                Shortcut(Path.Combine(ShortcutDirectory, "Screen Care.lnk"), Path.Combine(InstallDirectory, "ScreenCare.exe"), "");
                Shortcut(Path.Combine(ShortcutDirectory, "Desinstalar Screen Care.lnk"), Path.Combine(InstallDirectory, "Uninstall.exe"), "--uninstall");
                File.Delete(Path.Combine(ShortcutDirectory, "MonitorPausa.lnk"));
                File.Delete(Path.Combine(ShortcutDirectory, "Desinstalar MonitorPausa.lnk"));
                string warning;
                Settings settings = SettingsStore.Load(SettingsStore.DefaultPath, out warning);
                if (warning == null && settings.StartWithWindows) Startup.Set(true, Path.Combine(InstallDirectory, "ScreenCare.exe"));
            }
            catch
            {
                RestoreFiles(previous);
                RestoreFiles(shortcutPrevious);
                RestoreRegistry(UninstallKey, registryPrevious);
                using (var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                { if (runPrevious == null) run.DeleteValue("MonitorPausa", false); else run.SetValue("MonitorPausa", runPrevious); }
                throw;
            }
            finally
            {
                // Only remove the known staging files; never recursively delete user content.
                SafePaths.RequireNoLinks(stage);
                foreach (string name in OwnedFiles) { string file = Path.Combine(stage, name); SafePaths.RequireNoLinks(file); if (File.Exists(file)) File.Delete(file); }
                if (Directory.GetFileSystemEntries(stage).Length == 0) Directory.Delete(stage);
            }
        }
        internal static void InstallFiles(string directory, string stage, Dictionary<string, byte[]> previous)
        {
            foreach (string name in OwnedFiles)
            {
                string target = Path.Combine(directory, name);
                SafePaths.RequireNoLinks(target);
                previous[target] = File.Exists(target) ? File.ReadAllBytes(target) : null;
                File.Copy(Path.Combine(stage, name), target, true);
            }
            // Keep previous executables in the transaction until the upgrade succeeds.
            foreach (string name in LegacyFiles)
            {
                string target = Path.Combine(directory, name);
                SafePaths.RequireNoLinks(target);
                previous[target] = File.Exists(target) ? File.ReadAllBytes(target) : null;
                File.Delete(target);
            }
        }
        internal static void RestoreFiles(Dictionary<string, byte[]> previous)
        {
            foreach (var pair in previous)
            {
                SafePaths.RequireNoLinks(pair.Key);
                if (pair.Value == null) File.Delete(pair.Key); else File.WriteAllBytes(pair.Key, pair.Value);
            }
        }
        private sealed class RegistryValue { internal object Value; internal RegistryValueKind Kind; }
        private static Dictionary<string, RegistryValue> SnapshotRegistry(string path)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(path))
            {
                if (key == null) return null;
                var snapshot = new Dictionary<string, RegistryValue>();
                foreach (string name in key.GetValueNames()) snapshot[name] = new RegistryValue { Value = key.GetValue(name), Kind = key.GetValueKind(name) };
                return snapshot;
            }
        }
        private static void RestoreRegistry(string path, Dictionary<string, RegistryValue> snapshot)
        {
            Registry.CurrentUser.DeleteSubKeyTree(path, false);
            if (snapshot != null) using (var key = Registry.CurrentUser.CreateSubKey(path)) foreach (var pair in snapshot) key.SetValue(pair.Key, pair.Value.Value, pair.Value.Kind);
        }
        private static void Shortcut(string path, string target, string arguments)
        {
            SafePaths.RequireNoLinks(path);
            Type type = Type.GetTypeFromProgID("WScript.Shell", true);
            object shell = Activator.CreateInstance(type), shortcut = null;
            try
            {
                shortcut = type.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
                shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { InstallDirectory });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally { if (shortcut != null) Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
        }
        private static void LaunchUninstall()
        {
            ValidateInstallDirectory();
            if (MessageBox.Show(L10n.T("Desinstalar o Screen Care deste usuário?\n\nAs configurações e suas imagens serão preservadas."), L10n.T("Desinstalar Screen Care"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string helper = Path.Combine(Path.GetTempPath(), "ScreenCare-remove-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(Application.ExecutablePath, helper);
            Process.Start(new ProcessStartInfo(helper, "--remove " + Process.GetCurrentProcess().Id) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
        }
        private static void Remove()
        {
            ValidateInstallDirectory();
            if (!File.Exists(Path.Combine(InstallDirectory, ".monitorpausa-install"))) throw new IOException(L10n.T("Esta instalação não foi localizada."));
            StopInstalledApp();
            Startup.Set(false, "");
            // Keep the marker and uninstall entry until the executable has been successfully removed.
            ValidatePaths(InstallDirectory, ShortcutDirectory);
            foreach (string name in OwnedFiles) if (name != ".monitorpausa-install") File.Delete(Path.Combine(InstallDirectory, name));
            foreach (string name in LegacyFiles) File.Delete(Path.Combine(InstallDirectory, name));
            foreach (string name in ShortcutFiles) File.Delete(Path.Combine(ShortcutDirectory, name));
            if (Directory.Exists(ShortcutDirectory) && Directory.GetFileSystemEntries(ShortcutDirectory).Length == 0) Directory.Delete(ShortcutDirectory);
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
            File.Delete(Path.Combine(InstallDirectory, ".monitorpausa-install"));
            if (Directory.GetFileSystemEntries(InstallDirectory).Length == 0) Directory.Delete(InstallDirectory);
        }
    }

    internal sealed class SetupForm : Form
    {
        private bool installing;
        private readonly Palette palette;
        internal SetupForm()
        {
            SuspendLayout();
            string warning;
            palette = new Palette(SettingsStore.Load(SettingsStore.DefaultPath, out warning).Appearance);
            Text = "Instalar Screen Care"; Icon = Theme.MakeIcon();
            Font = new Font("Segoe UI", 10); ForeColor = palette.Text; BackColor = palette.Background;
            ClientSize = new Size(580, 420); AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            var title = new Label { Text = "Uma pausa para suas telas.", Font = new Font("Segoe UI Semibold", 21), AutoSize = true, Location = new Point(24, 23) };
            const string bodyText = "Desligue os monitores ou exiba suas imagens quando\nvocê parar de usar o computador.\n\nInstalação só para você, sem senha de administrador.";
            var body = new Label { AutoSize = true, Location = new Point(27, 78), Tag = "muted" };
            var location = new Label { Location = new Point(27, 225), Size = new Size(526, 44), Tag = "muted", AutoEllipsis = true };
            var languageLabel = new Label { Text = "Idioma", Location = new Point(27, 288), AutoSize = true };
            var languagePicker = L10n.LanguagePicker(L10n.Current); languagePicker.Location = new Point(128, 284);
            var install = Theme.Button("Instalar e abrir", true); install.Location = new Point(380, 359); install.Size = new Size(173, 39);
            var cancel = Theme.Button("Cancelar", false); cancel.Location = new Point(253, 359); cancel.Click += delegate { Close(); };
            Controls.AddRange(new Control[] { title, body, location, languageLabel, languagePicker, install, cancel });
            var localized = new LocalizedControls(this);
            Action translate = delegate {
                localized.Apply(L10n.Current);
                body.Text = "Screen Care 1.5.0\n\n" + L10n.T(bodyText);
                location.Text = L10n.T("Destino: ") + SetupProgram.InstallDirectory;
                Invalidate(true);
            };
            languagePicker.SelectedIndexChanged += delegate { L10n.Current = (Language)languagePicker.SelectedIndex; translate(); };
            translate();
            install.Click += async delegate {
                installing = true; install.Enabled = cancel.Enabled = languagePicker.Enabled = false; install.Text = L10n.T("Instalando…");
                try
                {
                    // Keep COM shortcut creation on this STA thread; the small local payload copies quickly.
                    await Task.Yield(); SetupProgram.Install();
                    Process.Start(new ProcessStartInfo(Path.Combine(SetupProgram.InstallDirectory, "ScreenCare.exe"), L10n.Current == Language.Portuguese ? "--language=pt-BR" : "--language=en") { UseShellExecute = true });
                    installing = false; Close();
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, L10n.T("Não foi possível instalar"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
                finally { installing = false; install.Enabled = cancel.Enabled = languagePicker.Enabled = true; install.Text = L10n.T("Instalar e abrir"); }
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (installing) e.Cancel = true; };
            PaintUi.Apply(this, palette);
            ResumeLayout(true);
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); if (palette != null) PaintUi.TitleBar(this, palette); }
    }
}
