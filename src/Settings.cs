using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace ScreenCare
{
    public enum IdleAction { MonitorsOff, Slideshow }

    public sealed class Settings
    {
        public bool Enabled = true;
        public IdleAction Action = IdleAction.MonitorsOff;
        public int IdleSeconds = 300;
        public string ImageFolder = "";
        public int SlideSeconds = 15;
        public bool IncludeSubfolders = false;
        public bool Shuffle = true;
        public bool StartWithWindows = false;
        public Appearance Appearance = Appearance.Dark;
        public Language Language = L10n.Detect(System.Globalization.CultureInfo.CurrentUICulture);
        // Advanced, file-only option. When true, keyboard/mouse input that no device
        // produced (automation, remote control, some touchpads) is not activity.
        public bool IgnoreSimulatedInput = false;

        public Settings Copy() { return (Settings)MemberwiseClone(); }
        public void Normalize()
        {
            IdleSeconds = Math.Max(10, Math.Min(86400, IdleSeconds));
            SlideSeconds = Math.Max(2, Math.Min(3600, SlideSeconds));
            ImageFolder = ImageFolder ?? "";
            if (!Enum.IsDefined(typeof(IdleAction), Action)) Action = IdleAction.MonitorsOff;
            if (!Enum.IsDefined(typeof(Appearance), Appearance)) Appearance = Appearance.Dark;
            if (!Enum.IsDefined(typeof(Language), Language)) Language = L10n.Detect(System.Globalization.CultureInfo.CurrentUICulture);
        }
    }

    internal static class SettingsStore
    {
        // Keep the legacy location and startup identity so existing profiles survive the rename.
        internal static string DirectoryPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonitorPausa"); } }
        internal static string DefaultPath { get { return Path.Combine(DirectoryPath, "settings.xml"); } }
        internal static Settings Load(string path, out string warning)
        {
            warning = null;
            if (!File.Exists(path)) return new Settings();
            try
            {
                var options = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 };
                using (var reader = XmlReader.Create(path, options))
                {
                    var settings = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(reader);
                    if (settings == null) throw new InvalidOperationException("Configuração vazia.");
                    settings.Normalize();
                    return settings;
                }
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is XmlException)) throw;
                warning = "Não foi possível ler as configurações salvas. Revise as opções e salve novamente.";
                var safe = new Settings();
                safe.Enabled = false;
                return safe;
            }
        }
        internal static void Save(Settings settings, string path)
        {
            settings.Normalize();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var writer = XmlWriter.Create(temp, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true }))
                    new XmlSerializer(typeof(Settings)).Serialize(writer, settings);
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    internal static class Startup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        internal static void Set(bool enabled, string executable)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) key.SetValue("MonitorPausa", "\"" + executable + "\" --background", RegistryValueKind.String);
                else key.DeleteValue("MonitorPausa", false);
            }
        }
    }
}
