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
                    if (File.Exists(path) && new FileInfo(path).Length > Limit)
                        File.WriteAllText(path, Tail(File.ReadAllText(path, Encoding.UTF8)), new UTF8Encoding(false));
                    File.AppendAllText(path, DateTime.UtcNow.ToString("o") + " " + code + Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (System.Security.SecurityException) { }
        }
        internal const int Limit = 65536;
        // Keep the newest half as whole lines: wiping the file erased the start
        // and exit history exactly when something had gone wrong.
        internal static string Tail(string text)
        {
            if (text.Length <= Limit / 2) return text;
            string tail = text.Substring(text.Length - Limit / 2);
            int line = tail.IndexOf('\n');
            return line < 0 ? "" : tail.Substring(line + 1);
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
            catch (System.Security.SecurityException) { }
            Event("error-recorded");
        }
    }
}
