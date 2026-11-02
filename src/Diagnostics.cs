using System;
using System.IO;
using System.Text;

namespace ScreenCare
{
    internal static class Diagnostics
    {
        private static readonly object Gate = new object();
        internal static string DirectoryPath = SettingsStore.DirectoryPath;
        internal static void Event(string code)
        {
            // Callers supply fixed event codes, never paths, keys, device names,
            // image names, window titles or individual input events.
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(DirectoryPath);
                    string path = Path.Combine(DirectoryPath, "diagnostics.log");
                    if (File.Exists(path) && new FileInfo(path).Length > 65536)
                        File.WriteAllText(path, "", new UTF8Encoding(false));
                    File.AppendAllText(path, DateTime.UtcNow.ToString("o") + " " + code + Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        internal static void Error(Exception exception)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(DirectoryPath);
                    File.WriteAllText(Path.Combine(DirectoryPath, "last-error.txt"), DateTime.UtcNow.ToString("o") + Environment.NewLine + exception, new UTF8Encoding(false));
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            Event("error-recorded");
        }
    }
}
