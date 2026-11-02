using System;
using System.IO;

namespace ScreenCare
{
    internal static class SafePaths
    {
        // Inspect the leaf AND every ancestor. Checking only the installation
        // directory misses junctions in Programs or the Start Menu directory.
        internal static void RequireNoLinks(string path)
        {
            string current = Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("O destino contém um link de arquivo ou pasta. Escolha uma pasta local sem links.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                current = Path.GetDirectoryName(current);
            }
        }
    }
}
