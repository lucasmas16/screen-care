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
        // Passive windows never take focus: keystrokes keep reaching the
        // application underneath, exactly as with monitors physically off.
        private readonly bool passive;
        internal SlideWindow(Rectangle bounds) : this(bounds, false) { }
        internal SlideWindow(Rectangle bounds, bool passiveCover)
        {
            passive = passiveCover;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            BackColor = Color.Black;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            Text = L10n.T("Screen Care — apresentação");
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
        protected override bool ShowWithoutActivation { get { return passive; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams value = base.CreateParams;
                if (passive) value.ExStyle |= 0x08000000 | 0x80; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
                return value;
            }
        }
        // Touch/pen are not registered as Raw Input; a tap on the cover is input.
        protected override void OnMouseDown(MouseEventArgs e)
        { base.OnMouseDown(e); if (passive && ExitSlide != null) ExitSlide(); }
    }

    // Shown when Windows turns the displays back on although Screen Care saw no
    // keyboard, mouse or controller input (for example a driver that reports a
    // resting gamepad as user presence). No static image stays on screen while
    // monitor-off keeps being retried. Any detected input removes it.
    internal sealed class Blackout : IDisposable
    {
        private readonly List<SlideWindow> windows = new List<SlideWindow>();
        private bool cursorHidden;
        internal Blackout(Action exit) : this(exit, null) { }
        internal Blackout(Action exit, Rectangle[] areas)
        {
            try
            {
                if (areas == null)
                {
                    Screen[] screens = Screen.AllScreens; areas = new Rectangle[screens.Length];
                    for (int i = 0; i < screens.Length; i++) areas[i] = screens[i].Bounds;
                }
                foreach (Rectangle area in areas)
                {
                    var window = new SlideWindow(area, true);
                    window.ExitSlide += exit;
                    windows.Add(window);
                    window.Show();
                }
                Cursor.Hide(); cursorHidden = true;
            }
            catch { Dispose(); throw; }
        }
        internal int Count { get { return windows.Count; } }
        // True while every current screen has a cover window with the same bounds.
        internal bool Covers(Screen[] screens)
        {
            if (screens.Length != windows.Count) return false;
            foreach (Screen screen in screens)
            {
                bool found = false;
                foreach (SlideWindow window in windows) found |= window.Bounds == screen.Bounds;
                if (!found) return false;
            }
            return true;
        }
        public void Dispose()
        {
            foreach (SlideWindow window in windows) { window.Close(); window.Dispose(); }
            windows.Clear();
            if (cursorHidden) { Cursor.Show(); cursorHidden = false; }
        }
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
                if (paths.Count == 0) { error(L10n.T("A pasta não contém imagens compatíveis. Escolha JPG, PNG, BMP, GIF ou TIFF.")); return; }
                loading = false;
                LoadNext();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!disposed) error(L10n.T("Não foi possível abrir a pasta. ") + ex.Message); }
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
                if (image == null) { error(L10n.T("Nenhuma imagem pôde ser aberta. Verifique os arquivos da pasta.")); return; }
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
            catch (Exception ex) { if (!disposed) error(L10n.T("Não foi possível exibir as imagens. ") + ex.Message); }
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
