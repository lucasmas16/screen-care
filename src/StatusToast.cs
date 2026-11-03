using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenCare
{
    internal sealed class StatusToastHost : IDisposable
    {
        internal StatusToast Current { get; private set; }
        internal void ShowStatus(bool enabled, Appearance appearance)
        { Show(delegate { return new StatusToast(enabled, null, appearance); }); }
        internal void ShowMessage(string message, Appearance appearance)
        { Show(delegate { return new StatusToast(null, message, appearance); }); }
        private void Show(Func<StatusToast> create)
        {
            // A notification failure must not terminate monitoring or undo a saved toggle.
            try
            {
                Hide(); var next = create(); Current = next;
                next.FormClosed += delegate { if (ReferenceEquals(Current, next)) Current = null; };
                next.Show();
            }
            catch (Exception error)
            {
                if (!Recovery.CanHandle(error)) throw;
                Diagnostics.Error(error); Hide();
            }
        }
        internal void Hide()
        {
            var old = Current; Current = null;
            if (old != null) Recovery.Attempt(delegate { old.Dispose(); });
        }
        public void Dispose() { Hide(); }
    }

    internal sealed class StatusToast : Form
    {
        internal const int Duration = 3000;
        internal readonly bool? MonitoringEnabled;
        private readonly string message;
        private readonly Palette palette;
        private readonly Timer timer;
        private readonly Stopwatch lifetime = new Stopwatch();
        private const int NoActivate = 0x08000000, ToolWindow = 0x80, Layered = 0x80000, Transparent = 0x20;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint color, byte alpha, uint flags);
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        { get { var cp = base.CreateParams; cp.ExStyle |= NoActivate | ToolWindow | Layered | Transparent; return cp; } }
        internal StatusToast(bool? enabled, string text, Appearance appearance)
        {
            MonitoringEnabled = enabled; message = text; palette = new Palette(appearance);
            Text = "Screen Care · " + (enabled.HasValue ? enabled.Value ? L10n.T("Ativado") : L10n.T("Desativado") : L10n.T("Aviso"));
            AccessibleName = enabled.HasValue ? TrayApplication.StatusNotification(enabled.Value) : text;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96); ClientSize = new Size(420, 132);
            BackColor = palette.Surface; DoubleBuffered = true;
            timer = new Timer { Interval = 100 };
            timer.Tick += delegate {
                if (lifetime.ElapsedMilliseconds >= Duration) { timer.Stop(); Close(); }
                else KeepAbove();
            };
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Layered + transparent passes mouse input through even to another process.
            // Fully opaque alpha keeps the existing appearance.
            if (!SetLayeredWindowAttributes(Handle, 0, 255, 2)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0021) { m.Result = new IntPtr(3); return; } // MA_NOACTIVATE
            if (m.Msg == 0x0084) { m.Result = new IntPtr(-1); return; } // HTTRANSPARENT
            base.WndProc(ref m);
        }
        private void KeepAbove()
        {
            // Reassert z-order while visible; never activate, focus, or alter the foreground app.
            Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0200);
        }
        internal static Point Position(Rectangle area, Size size, int margin)
        { return new Point(area.Left + (area.Width - size.Width) / 2, area.Bottom - size.Height - margin); }
        private void Shape()
        {
            if (ClientSize.Width < 1 || ClientSize.Height < 1) return;
            using (var path = PaintUi.Round(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), 16 * PaintUi.Scale(this)))
            { Region old = Region; Region = new Region(path); if (old != null) old.Dispose(); }
        }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); Shape(); }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            var area = Screen.PrimaryScreen.WorkingArea;
            // Move to the primary monitor before measuring its DPI.
            Location = area.Location;
            float scale = PaintUi.Scale(this);
            ClientSize = new Size(Math.Min((int)(420 * scale), area.Width - 24), Math.Min((int)(132 * scale), area.Height - 24));
            Location = Position(area, Size, (int)(20 * scale)); Shape();
            KeepAbove();
            Update(); lifetime.Restart(); timer.Start();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); float s = PaintUi.Scale(this); var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            PaintUi.Surface(g, new RectangleF(1, 1, Width - 3, Height - 3), palette, false, false, s);
            Color accent = MonitoringEnabled == false ? palette.Muted : palette.Accent;
            using (var path = PaintUi.Round(new RectangleF(18 * s, 22 * s, 84 * s, 84 * s), 14 * s))
            using (var brush = new SolidBrush(palette.Inset)) g.FillPath(brush, path);
            PaintUi.Glyph(g, "monitor", new RectangleF(30 * s, 35 * s, 60 * s, 60 * s), accent, 1.4f);
            // Check = enabled; pause bars = disabled. Shape communicates state as well as color.
            using (var brush = new SolidBrush(palette.Surface)) g.FillEllipse(brush, 71 * s, 70 * s, 27 * s, 27 * s);
            using (var pen = new Pen(accent, 2.2f * s))
            {
                g.DrawEllipse(pen, 72 * s, 71 * s, 25 * s, 25 * s);
                if (MonitoringEnabled == false)
                { g.DrawLine(pen, 81 * s, 78 * s, 81 * s, 89 * s); g.DrawLine(pen, 88 * s, 78 * s, 88 * s, 89 * s); }
                else if (MonitoringEnabled == true)
                    g.DrawLines(pen, new[] { new PointF(78 * s, 83 * s), new PointF(83 * s, 88 * s), new PointF(91 * s, 79 * s) });
                else { g.DrawLine(pen, 85 * s, 77 * s, 85 * s, 85 * s); g.DrawEllipse(pen, 84 * s, 89 * s, s, s); }
            }
            using (var title = new Font("Segoe UI", 9, FontStyle.Regular))
            using (var body = new Font("Segoe UI", MonitoringEnabled.HasValue ? 18 : 10, FontStyle.Regular))
            using (var hint = new Font("Segoe UI", 9, FontStyle.Regular))
            {
                PaintUi.Text(g, "Screen Care", title, palette.Muted, new Rectangle((int)(120*s), (int)(20*s), Width-(int)(138*s), (int)(22*s)), TextFormatFlags.SingleLine);
                PaintUi.Text(g, MonitoringEnabled.HasValue ? MonitoringEnabled.Value ? L10n.T("Ativado") : L10n.T("Desativado") : message,
                    body, MonitoringEnabled.HasValue ? accent : palette.Text,
                    new Rectangle((int)(120*s), (int)(44*s), Width-(int)(138*s), (int)((MonitoringEnabled.HasValue ? 36 : 70)*s)), TextFormatFlags.WordBreak);
                if (MonitoringEnabled.HasValue) PaintUi.Text(g, L10n.T("Ctrl+Alt+F12 para alternar"), hint, palette.Muted,
                    new Rectangle((int)(120*s), (int)(91*s), Width-(int)(138*s), (int)(22*s)), TextFormatFlags.SingleLine);
            }
        }
        protected override void Dispose(bool disposing) { if (disposing && timer != null) timer.Dispose(); base.Dispose(disposing); }
    }
}
