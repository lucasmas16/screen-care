using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenCare
{
    internal sealed class SlideWindow : Form
    {
        internal Bitmap Slide;
        internal event Action ExitSlide;
        internal SlideWindow(Rectangle bounds)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            BackColor = Color.Black;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            Text = "Screen Care — apresentação";
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.Black);
            if (Slide != null)
            {
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                e.Graphics.DrawImage(Slide, Images.Fit(Slide.Size, ClientSize));
            }
        }
        protected override void OnKeyDown(KeyEventArgs e)
        { if (e.KeyCode == Keys.Escape && ExitSlide != null) { e.Handled = true; ExitSlide(); } base.OnKeyDown(e); }
    }

    internal sealed class Slideshow : IDisposable
    {
        private readonly List<SlideWindow> windows = new List<SlideWindow>();
        private CancellationTokenSource cancellation;
        private List<string> paths;
        private Bitmap current;
        private bool loading, disposed, cursorHidden;
        private int index;
        private double nextSlide;
        private readonly Settings settings;
        private readonly Func<double> now;
        private readonly Action<string> error;
        private readonly Action exit;
        private readonly Size target;
        internal Slideshow(Settings value, Func<double> clock, Action<string> onError, Action onExit)
        {
            settings = value.Copy(); now = clock; error = onError; exit = onExit;
            int width = 1, height = 1;
            foreach (Screen screen in Screen.AllScreens)
            {
                width = Math.Max(width, screen.Bounds.Width); height = Math.Max(height, screen.Bounds.Height);
                var window = new SlideWindow(screen.Bounds);
                window.ExitSlide += exit;
                windows.Add(window);
            }
            target = new Size(width, height);
        }
        internal async void Start()
        {
            loading = true;
            cancellation = new CancellationTokenSource();
            CancellationToken token = cancellation.Token;
            try
            {
                paths = await Task.Run(delegate {
                    var found = Images.Find(settings.ImageFolder, settings.IncludeSubfolders, token);
                    if (settings.Shuffle) Images.Shuffle(found, new Random());
                    return found;
                }, token);
                if (disposed) return;
                if (paths.Count == 0) { error("A pasta não contém imagens compatíveis. Escolha JPG, PNG, BMP, GIF ou TIFF."); return; }
                loading = false;
                LoadNext();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!disposed) error("Não foi possível abrir a pasta. " + ex.Message); }
            finally { if (disposed && cancellation != null) { cancellation.Dispose(); cancellation = null; } }
        }
        internal void Tick() { if (!disposed && !loading && paths != null && now() >= nextSlide) LoadNext(); }
        private async void LoadNext()
        {
            if (disposed || loading) return;
            loading = true;
            Bitmap image = null;
            CancellationToken token = cancellation.Token;
            try
            {
                image = await Task.Run(delegate {
                    while (paths.Count > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        if (index >= paths.Count) index = 0;
                        string path = paths[index];
                        try { Bitmap loaded = Images.Load(path, target); ++index; return loaded; }
                        catch (Exception ex)
                        {
                            if (!(ex is System.IO.IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is OutOfMemoryException || ex is System.Runtime.InteropServices.ExternalException)) throw;
                            paths.RemoveAt(index);
                        }
                    }
                    return (Bitmap)null;
                }, token);
                if (disposed) { if (image != null) image.Dispose(); return; }
                if (image == null) { error("Nenhuma imagem pôde ser aberta. Verifique os arquivos da pasta."); return; }
                Bitmap previous = current; current = image;
                foreach (SlideWindow window in windows)
                {
                    window.Slide = current;
                    if (!window.Visible) window.Show();
                    window.Invalidate();
                }
                if (!cursorHidden) { Cursor.Hide(); cursorHidden = true; }
                if (previous != null) previous.Dispose();
                nextSlide = now() + settings.SlideSeconds;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!disposed) error("Não foi possível exibir as imagens. " + ex.Message); }
            finally
            {
                loading = false;
                if (disposed && cancellation != null) { cancellation.Dispose(); cancellation = null; }
            }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (cancellation != null)
            { cancellation.Cancel(); if (!loading) { cancellation.Dispose(); cancellation = null; } }
            foreach (SlideWindow window in windows) { window.Slide = null; window.Close(); window.Dispose(); }
            windows.Clear();
            if (current != null) { current.Dispose(); current = null; }
            if (cursorHidden) { Cursor.Show(); cursorHidden = false; }
        }
    }
}
