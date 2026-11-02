using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

namespace ScreenCare
{
    internal static class Images
    {
        private static readonly HashSet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };
        internal static List<string> Find(string folder, bool recursive, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) throw new IOException(L10n.T("A pasta de imagens não está disponível."));
            var files = new List<string>();
            var pending = new Stack<string>();
            pending.Push(folder);
            while (pending.Count > 0 && files.Count < 20000)
            {
                token.ThrowIfCancellationRequested();
                string current = pending.Pop();
                try
                {
                    foreach (string path in Directory.EnumerateFiles(current))
                    {
                        token.ThrowIfCancellationRequested();
                        if (Extensions.Contains(Path.GetExtension(path))) files.Add(path);
                        if (files.Count == 20000) break;
                    }
                    if (recursive)
                        foreach (string dir in Directory.EnumerateDirectories(current))
                        {
                            token.ThrowIfCancellationRequested();
                            if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) == 0) pending.Push(dir);
                        }
                }
                catch (UnauthorizedAccessException) { if (current == folder) throw; }
                catch (IOException) { if (current == folder) throw; }
            }
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }
        internal static void Shuffle(List<string> paths, Random random)
        {
            for (int i = paths.Count - 1; i > 0; --i)
            { int j = random.Next(i + 1); string item = paths[i]; paths[i] = paths[j]; paths[j] = item; }
        }
        internal static Rectangle Fit(Size source, Size target)
        {
            if (source.Width <= 0 || source.Height <= 0 || target.Width <= 0 || target.Height <= 0) return Rectangle.Empty;
            double scale = Math.Min((double)target.Width / source.Width, (double)target.Height / source.Height);
            int width = Math.Max(1, (int)Math.Round(source.Width * scale));
            int height = Math.Max(1, (int)Math.Round(source.Height * scale));
            return new Rectangle((target.Width - width) / 2, (target.Height - height) / 2, width, height);
        }
        internal static Bitmap Load(string path, Size target)
        {
            var info = new FileInfo(path);
            if (info.Length > 64L * 1024 * 1024) throw new IOException(L10n.T("Imagem maior que 64 MB."));
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var original = Image.FromStream(stream, false, false))
            {
                if ((long)original.Width * original.Height > 40000000) throw new IOException(L10n.T("Imagem acima de 40 megapixels."));
                try
                {
                    if (Array.IndexOf(original.PropertyIdList, 0x0112) >= 0)
                    {
                        byte[] orientationBytes = original.GetPropertyItem(0x0112).Value;
                        int orientation = orientationBytes != null && orientationBytes.Length > 0 ? orientationBytes[0] : 1;
                        RotateFlipType[] transforms = { RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipNone,
                            RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate180FlipX,
                            RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate270FlipNone };
                        if (orientation >= 1 && orientation <= 8) original.RotateFlip(transforms[orientation]);
                    }
                }
                catch (ArgumentException) { }
                // Cache just a screen-sized bitmap, capped at 8.3 megapixels.
                double scale = Math.Min(1.0, Math.Min((double)target.Width / original.Width, (double)target.Height / original.Height));
                scale = Math.Min(scale, Math.Sqrt(8294400.0 / ((long)original.Width * original.Height)));
                int width = Math.Max(1, (int)(original.Width * scale)), height = Math.Max(1, (int)(original.Height * scale));
                var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                try
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    { g.Clear(Color.Black); g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(original, 0, 0, width, height); }
                    return bitmap;
                }
                catch { bitmap.Dispose(); throw; }
            }
        }
    }
}
