using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenCare
{
    public enum Appearance { Dark, Light }

    internal sealed class Palette
    {
        internal readonly bool Dark;
        internal readonly Color Background, Surface, SurfaceEnd, Inset, Text, Muted, Line, Accent, AccentText, Selection, Highlight, Shadow;
        internal Palette(Appearance appearance)
        {
            Dark = appearance == Appearance.Dark;
            Background = C(Dark ? 0x121619 : 0xEDF1F5);
            Surface = C(Dark ? 0x252B30 : 0xFFFFFF); SurfaceEnd = C(Dark ? 0x1D2328 : 0xF5F8FB);
            Inset = C(Dark ? 0x161C20 : 0xE8EEF3); Text = C(Dark ? 0xEFF4F6 : 0x202F38);
            Muted = C(Dark ? 0xA6B2BA : 0x536772); Line = C(Dark ? 0x39434B : 0xCBD5DF);
            Accent = C(Dark ? 0x99DFBE : 0x216F54); AccentText = C(Dark ? 0x13271E : 0xFFFFFF);
            Selection = C(Dark ? 0x283E35 : 0xE1F0E8);
            Highlight = Color.FromArgb(Dark ? 22 : 220, Color.White); Shadow = Color.FromArgb(Dark ? 56 : 17, 0, 0, 0);
            if (SystemInformation.HighContrast)
            {
                Background = Surface = SurfaceEnd = Inset = SystemColors.Window;
                Text = SystemColors.WindowText; Muted = Text; Line = Text;
                Accent = SystemColors.Highlight; AccentText = SystemColors.HighlightText; Selection = SystemColors.Window;
                Highlight = Shadow = Color.Transparent;
            }
        }
        private static Color C(int rgb) { return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }
    }
    internal interface IThemed { void SetPalette(Palette palette); }

    internal static class PaintUi
    {
        // ButtonBase skips background painting for some flat/transparent styles.
        // Paint the actual parent surface, including its gradient, before rounded controls.
        internal static void ParentBackground(Control child, PaintEventArgs e, Action<Control, PaintEventArgs> background, Action<Control, PaintEventArgs> foreground)
        {
            if (child.Parent == null) { e.Graphics.Clear(SystemColors.Control); return; }
            Control opaque = child.Parent;
            while (opaque.Parent != null && opaque.BackColor.A < 255) opaque = opaque.Parent;
            using (var brush = new SolidBrush(opaque.BackColor)) e.Graphics.FillRectangle(brush, e.ClipRectangle);
            var state = e.Graphics.Save();
            try
            {
                e.Graphics.TranslateTransform(-child.Left, -child.Top);
                Rectangle clip = e.ClipRectangle; clip.Offset(child.Left, child.Top);
                using (var parentArgs = new PaintEventArgs(e.Graphics, clip))
                { background(child.Parent, parentArgs); foreground(child.Parent, parentArgs); }
            }
            finally { e.Graphics.Restore(state); }
        }
        internal static float Scale(Control control)
        {
            Form form = control.FindForm();
            return form != null && form.AutoScaleMode == AutoScaleMode.Dpi ? form.CurrentAutoScaleDimensions.Width / 96f : control.DeviceDpi / 96f;
        }
        internal static int S(Control control, float value) { return (int)Math.Round(value * Scale(control)); }
        internal static GraphicsPath Round(RectangleF r, float radius)
        {
            var path = new GraphicsPath(); float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) return path;
            path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90); path.CloseFigure();
            return path;
        }
        internal static void Surface(Graphics g, RectangleF r, Palette p, bool inset, bool selected, float scale)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (r.Width < 2 || r.Height < 2) return;
            var shadow = r; shadow.Offset(2 * scale, 3 * scale);
            using (var path = Round(shadow, 13 * scale)) using (var brush = new SolidBrush(p.Shadow)) g.FillPath(brush, path);
            using (var path = Round(r, 13 * scale))
            using (var brush = new LinearGradientBrush(r, selected ? p.Selection : inset ? p.Inset : p.Surface, selected ? p.Selection : inset ? p.Inset : p.SurfaceEnd, 110f))
            using (var pen = new Pen(selected ? p.Accent : p.Line, scale))
            { g.FillPath(brush, path); g.DrawPath(pen, path); }
            if (!inset)
            {
                var inner = r; inner.Inflate(-1.4f * scale, -1.4f * scale);
                using (var path = Round(inner, 12 * scale)) using (var pen = new Pen(p.Highlight, scale)) g.DrawPath(pen, path);
            }
        }
        internal static void Glyph(Graphics g, string glyph, RectangleF rect, Color color, float width)
        {
            var state = g.Save(); g.TranslateTransform(rect.X, rect.Y); g.ScaleTransform(rect.Width / 24f, rect.Height / 24f);
            using (var pen = new Pen(color, width))
            {
                pen.StartCap = pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                if (glyph == "monitor") { g.DrawRectangle(pen, 3, 4, 18, 13); g.DrawLine(pen, 12, 17, 12, 21); g.DrawLine(pen, 8, 21, 16, 21); }
                else if (glyph == "image") { g.DrawRectangle(pen, 3, 3, 18, 18); g.DrawEllipse(pen, 6, 6, 3, 3); g.DrawLines(pen, new[] { new PointF(4, 18), new PointF(10, 12), new PointF(14, 16), new PointF(17, 11), new PointF(21, 15) }); }
                else if (glyph == "folder") { g.DrawLines(pen, new[] { new PointF(3, 7), new PointF(3, 4), new PointF(10, 4), new PointF(13, 7), new PointF(21, 7), new PointF(21, 20), new PointF(3, 20), new PointF(3, 7), new PointF(21, 7) }); }
                else if (glyph == "sun") { g.DrawEllipse(pen, 8, 8, 8, 8); for (int i = 0; i < 8; ++i) { double a = i * Math.PI / 4; g.DrawLine(pen, 12 + (float)Math.Cos(a) * 8, 12 + (float)Math.Sin(a) * 8, 12 + (float)Math.Cos(a) * 10, 12 + (float)Math.Sin(a) * 10); } }
                else if (glyph == "moon") { using (var path = new GraphicsPath()) { path.AddArc(3, 3, 17, 17, 50, 275); path.AddBezier(18.5f, 5.9f, 8, 6, 8, 18, 16.9f, 18); g.DrawPath(pen, path); } }
                else if (glyph == "clock") { g.DrawEllipse(pen, 3, 3, 18, 18); g.DrawLines(pen, new[] { new PointF(12, 7), new PointF(12, 12), new PointF(16, 14) }); }
                else if (glyph == "check") g.DrawLines(pen, new[] { new PointF(5, 12), new PointF(10, 17), new PointF(19, 7) });
                else if (glyph == "power") { g.DrawArc(pen, 5, 5, 14, 15, -45, 270); g.DrawLine(pen, 12, 3, 12, 12); }
                else if (glyph == "plus") { g.DrawLine(pen, 5, 12, 19, 12); g.DrawLine(pen, 12, 5, 12, 19); }
                else if (glyph == "minus") g.DrawLine(pen, 5, 12, 19, 12);
            }
            g.Restore(state);
        }
        internal static void Text(Graphics g, string text, Font font, Color color, Rectangle rect, TextFormatFlags extra)
        { TextRenderer.DrawText(g, text, font, rect, color, TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | extra); }
        internal static void Apply(Control root, Palette palette)
        {
            IThemed own = root as IThemed;
            if (own != null) own.SetPalette(palette);
            else if (root is TextBox) { root.BackColor = palette.Inset; root.ForeColor = palette.Text; }
            else if (root is Label) { root.ForeColor = (string)root.Tag == "muted" ? palette.Muted : palette.Text; }
            foreach (Control child in root.Controls) Apply(child, palette);
            root.Invalidate();
        }
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        internal static void TitleBar(Form form, Palette p)
        {
            if (!form.IsHandleCreated) return;
            try
            {
                int dark = p.Dark ? 1 : 0; DwmSetWindowAttribute(form.Handle, 20, ref dark, 4);
                int caption = ColorTranslator.ToWin32(p.Background); DwmSetWindowAttribute(form.Handle, 35, ref caption, 4);
                int text = ColorTranslator.ToWin32(p.Text); DwmSetWindowAttribute(form.Handle, 36, ref text, 4);
            }
            catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
        }
    }

    internal class GlassPanel : Panel, IThemed
    {
        protected Palette palette = new Palette(Appearance.Dark);
        internal bool Inset;
        internal GlassPanel()
        { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent; }
        public virtual void SetPalette(Palette p) { palette = p; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        { base.OnPaint(e); float scale = PaintUi.Scale(this); PaintUi.Surface(e.Graphics, new RectangleF(1, 1, Width - 5 * scale, Height - 6 * scale), palette, Inset, false, scale); }
    }

    internal class SoftButton : Button, IThemed
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        { PaintUi.ParentBackground(this, e, InvokePaintBackground, InvokePaint); }
        protected Palette palette = new Palette(Appearance.Dark);
        internal bool Primary, Selected;
        internal string Glyph;
        private bool hover, down;
        internal SoftButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; BackColor = Color.Transparent; Cursor = Cursors.Hand;
            SetStyle(ControlStyles.Opaque, false);
            Size = new Size(110, 40); Margin = Padding.Empty; UseVisualStyleBackColor = false;
        }
        public void SetPalette(Palette p) { palette = p; Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            float s = PaintUi.Scale(this); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = new RectangleF(s, s, Width - 4 * s, Height - 5 * s);
            if (Primary)
            {
                using (var path = PaintUi.Round(r, 11 * s)) using (var brush = new SolidBrush(down ? palette.Selection : palette.Accent)) e.Graphics.FillPath(brush, path);
            }
            else PaintUi.Surface(e.Graphics, r, palette, down, Selected || hover, s);
            Color fg = Enabled ? (Primary && !down ? palette.AccentText : Selected ? palette.Accent : palette.Text) : palette.Muted;
            if (!String.IsNullOrEmpty(Glyph)) PaintUi.Glyph(e.Graphics, Glyph, new RectangleF((Width - 20 * s) / 2, (Height - 22 * s) / 2, 20 * s, 20 * s), fg, 1.7f);
            else PaintUi.Text(e.Graphics, Text, Font, fg, Rectangle.Round(r), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues) { var focus = r; focus.Inflate(-4 * s, -4 * s); using (var path = PaintUi.Round(focus, 7 * s)) using (var pen = new Pen(fg, s) { DashStyle = DashStyle.Dot }) e.Graphics.DrawPath(pen, path); }
        }
    }

    internal class Toggle : CheckBox, IThemed
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        { PaintUi.ParentBackground(this, e, InvokePaintBackground, InvokePaint); }
        protected Palette palette = new Palette(ScreenCare.Appearance.Dark);
        internal Toggle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent; AutoSize = false; Size = new Size(50, 30); Cursor = Cursors.Hand; Margin = Padding.Empty;
            SetStyle(ControlStyles.Opaque, false);
            AccessibleRole = AccessibleRole.CheckButton;
        }
        public void SetPalette(Palette p) { palette = p; Invalidate(); }
        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); AccessibilityNotifyClients(AccessibleEvents.StateChange, -1); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            float s = PaintUi.Scale(this); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(2 * s, (Height - 24 * s) / 2, 44 * s, 24 * s);
            using (var path = PaintUi.Round(r, 12 * s)) using (var brush = new SolidBrush(Checked ? palette.Accent : palette.Inset)) using (var pen = new Pen(Checked ? palette.Accent : palette.Line, s)) { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path); }
            float x = Checked ? r.Right - 21 * s : r.X + 3 * s;
            using (var brush = new SolidBrush(Checked ? palette.AccentText : palette.Muted)) e.Graphics.FillEllipse(brush, x, r.Y + 3 * s, 18 * s, 18 * s);
            if (Focused && ShowFocusCues) using (var pen = new Pen(palette.Accent, s) { DashStyle = DashStyle.Dot }) e.Graphics.DrawRectangle(pen, s, s, Width - 3 * s, Height - 3 * s);
        }
    }

    internal sealed class ThemeToggle : Toggle
    {
        private bool? clickedSide;
        internal ThemeToggle() { AutoCheck = false; Size = new Size(156, 38); AccessibleName = "Tema escuro"; AccessibleDescription = "Ativado: dark. Desativado: light. Espaço alterna o tema."; }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) clickedSide = e.X >= Width / 2; base.OnMouseDown(e); }
        protected override void OnMouseLeave(EventArgs e) { clickedSide = null; base.OnMouseLeave(e); }
        protected override void OnKeyDown(KeyEventArgs e) { clickedSide = null; base.OnKeyDown(e); }
        protected override void OnClick(EventArgs e) { Checked = clickedSide ?? !Checked; clickedSide = null; base.OnClick(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            float s = PaintUi.Scale(this); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            PaintUi.Surface(e.Graphics, new RectangleF(s, s, Width - 4 * s, Height - 5 * s), palette, true, false, s);
            float half = (Width - 10 * s) / 2;
            var active = new RectangleF(Checked ? half + 5 * s : 4 * s, 4 * s, half - s, Height - 11 * s);
            using (var path = PaintUi.Round(active, 9 * s)) using (var brush = new SolidBrush(palette.Surface)) using (var pen = new Pen(palette.Line, s)) { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path); }
            for (int i = 0; i < 2; ++i)
            {
                Color fg = (Checked == (i == 1)) ? palette.Text : palette.Muted;
                float x = i * half + 12 * s;
                PaintUi.Glyph(e.Graphics, i == 0 ? "sun" : "moon", new RectangleF(x, (Height - 19 * s) / 2, 16 * s, 16 * s), fg, 1.6f);
                PaintUi.Text(e.Graphics, i == 0 ? "Light" : "Dark", Font, fg, new Rectangle((int)(x + 21 * s), 0, (int)(half - 29 * s), Height - (int)(3 * s)), TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5), palette.Accent, palette.Inset);
        }
    }

    internal sealed class ModeTile : RadioButton, IThemed
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        { PaintUi.ParentBackground(this, e, InvokePaintBackground, InvokePaint); }
        private Palette palette = new Palette(ScreenCare.Appearance.Dark);
        internal string Glyph, Caption;
        internal ModeTile()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent; AutoSize = false; Cursor = Cursors.Hand; Margin = Padding.Empty; Size = new Size(240, 74);
            SetStyle(ControlStyles.Opaque, false);
        }
        public void SetPalette(Palette p) { palette = p; Invalidate(); }
        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            float s = PaintUi.Scale(this); PaintUi.Surface(e.Graphics, new RectangleF(s, s, Width - 5 * s, Height - 6 * s), palette, false, Checked, s);
            PaintUi.Glyph(e.Graphics, Glyph, new RectangleF(15 * s, 21 * s, 24 * s, 24 * s), Checked ? palette.Accent : palette.Muted, 1.5f);
            PaintUi.Text(e.Graphics, Text, Font, palette.Text, new Rectangle((int)(51 * s), (int)(13 * s), Width - (int)(73 * s), (int)(25 * s)), TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            using (var small = new Font(Font.FontFamily, 8.5f)) PaintUi.Text(e.Graphics, Caption, small, palette.Muted, new Rectangle((int)(51 * s), (int)(39 * s), Width - (int)(62 * s), (int)(21 * s)), TextFormatFlags.SingleLine);
            if (Checked) PaintUi.Glyph(e.Graphics, "check", new RectangleF(Width - 27 * s, 11 * s, 15 * s, 15 * s), palette.Accent, 2);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -7, -7), palette.Accent, palette.Surface);
        }
    }

    internal sealed class NumberField : GlassPanel
    {
        private readonly TextBox entry;
        private readonly SoftButton minus, plus;
        internal int Minimum = 1, Maximum = 86400;
        internal event EventHandler ValueChanged;
        internal NumberField()
        {
            Inset = true; Size = new Size(138, 42); Margin = Padding.Empty;
            entry = new TextBox { BorderStyle = BorderStyle.None, TextAlign = HorizontalAlignment.Center, Text = "5", AccessibleName = "Valor", TabIndex = 0, Margin = Padding.Empty };
            minus = new SoftButton { Glyph = "minus", AccessibleName = "Diminuir", TabIndex = 1 };
            plus = new SoftButton { Glyph = "plus", AccessibleName = "Aumentar", TabIndex = 2 };
            Controls.AddRange(new Control[] { entry, minus, plus });
            minus.Click += delegate { Step(-1); }; plus.Click += delegate { Step(1); };
            entry.TextChanged += delegate { if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); };
            entry.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) { Step(e.KeyCode == Keys.Up ? 1 : -1); e.SuppressKeyPress = true; } };
        }
        internal string ValueText { get { return entry.Text; } set { entry.Text = value; } }
        internal int Value { get { int v; return Int32.TryParse(entry.Text, out v) ? v : Minimum; } set { entry.Text = Math.Max(Minimum, Math.Min(Maximum, value)).ToString(); } }
        internal bool Valid { get { int v; return Int32.TryParse(entry.Text, out v) && v >= Minimum && v <= Maximum; } }
        internal void SetName(string name) { AccessibleName = name; entry.AccessibleName = name; minus.AccessibleName = "Diminuir: " + name; plus.AccessibleName = "Aumentar: " + name; }
        private void Step(int delta) { Value = (int)Math.Max(Minimum, Math.Min((long)Maximum, (long)Value + delta)); }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e); if (entry == null) return;
            int side = PaintUi.S(this, 30), pad = PaintUi.S(this, 5);
            minus.SetBounds(pad, pad, side, Height - pad * 2); plus.SetBounds(Width - side - pad - 2, pad, side, Height - pad * 2);
            entry.SetBounds(side + pad, (Height - entry.PreferredHeight) / 2 - 1, Math.Max(20, Width - side * 2 - pad * 3), entry.PreferredHeight);
        }
        public override void SetPalette(Palette p) { base.SetPalette(p); entry.BackColor = p.Inset; entry.ForeColor = p.Text; }
    }

    internal sealed class ThemeMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly Palette palette;
        internal ThemeMenuRenderer(Palette p) { palette = p; RoundedEdges = false; }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { e.Graphics.Clear(palette.SurfaceEnd); }
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        { if (e.Item.Selected && e.Item.Enabled) using (var brush = new SolidBrush(palette.Selection)) e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size)); }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = e.Item.Enabled ? palette.Text : palette.Muted; base.OnRenderItemText(e); }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) { using (var pen = new Pen(palette.Line)) e.Graphics.DrawLine(pen, 8, 3, e.Item.Width - 8, 3); }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { using (var pen = new Pen(palette.Line)) e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1); }
    }
}
