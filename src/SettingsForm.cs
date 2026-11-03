using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace ScreenCare
{
    internal static class Theme
    {
        internal static readonly Color Ink = Color.FromArgb(32, 47, 56), Muted = Color.FromArgb(83, 103, 114), Accent = Color.FromArgb(33, 111, 84);
        internal static Icon MakeIcon()
        {
            using (var bitmap = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
                    using (var path = PaintUi.Round(new RectangleF(1, 1, 30, 30), 9)) using (var brush = new SolidBrush(Accent)) g.FillPath(brush, path);
                    PaintUi.Glyph(g, "monitor", new RectangleF(5, 5, 22, 22), Color.White, 1.8f);
                    using (var pen = new Pen(Color.White, 2)) { g.DrawLine(pen, 13, 11, 13, 16); g.DrawLine(pen, 18, 11, 18, 16); }
                }
                IntPtr handle = bitmap.GetHicon();
                try { using (Icon icon = Icon.FromHandle(handle)) return (Icon)icon.Clone(); } finally { Native.DestroyIcon(handle); }
            }
        }
        internal static Button Button(string text, bool primary) { var button = new SoftButton { Text = text, Primary = primary }; button.SetPalette(new Palette(Appearance.Light)); return button; }
    }

    internal sealed class OverviewPanel : GlassPanel
    {
        internal bool Slideshow, Active = true;
        internal string Wait = "";
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Graphics g = e.Graphics; float s = PaintUi.Scale(this);
            int pad = (int)(23 * s), textWidth = Width - pad * 2;
            using (var small = new Font("Segoe UI", 8.2f, FontStyle.Bold)) PaintUi.Text(g, L10n.T("SUAS TELAS"), small, palette.Muted, new Rectangle(pad, (int)(25 * s), textWidth, (int)(22 * s)), TextFormatFlags.SingleLine);
            float cx = Width / 2f - 3 * s, y = 110 * s, w = Math.Min(198 * s, Width - 49 * s), h = w * .66f;
            // Static vector layers create glass depth without blur loops or an image cache.
            var rear = new RectangleF(cx - w / 2 + 15 * s, y - 18 * s, w - 6 * s, h);
            using (var path = PaintUi.Round(rear, 10 * s)) using (var brush = new SolidBrush(palette.Inset)) using (var pen = new Pen(palette.Line, s)) { g.FillPath(brush, path); g.DrawPath(pen, path); }
            var screen = new RectangleF(cx - w / 2 - 8 * s, y, w, h);
            PaintUi.Surface(g, screen, palette, false, false, s);
            var display = screen; display.Inflate(-8 * s, -8 * s);
            using (var path = PaintUi.Round(display, 5 * s)) using (var brush = new LinearGradientBrush(display, palette.Selection, palette.Inset, 60f)) g.FillPath(brush, path);
            if (Slideshow)
            {
                var state = g.Save(); using (var clip = PaintUi.Round(display, 5 * s)) g.SetClip(clip);
                using (var brush = new SolidBrush(Color.FromArgb(100, palette.Accent))) g.FillPolygon(brush, new[] { new PointF(display.Left - 8 * s, display.Bottom), new PointF(display.Left + display.Width * .36f, display.Top + display.Height * .35f), new PointF(display.Right + 8 * s, display.Bottom) });
                using (var brush = new SolidBrush(Color.FromArgb(65, palette.Accent))) { g.FillPolygon(brush, new[] { new PointF(display.Left + 40 * s, display.Bottom), new PointF(display.Right - 35 * s, display.Top + 30 * s), new PointF(display.Right + 10 * s, display.Bottom) }); g.FillEllipse(brush, display.Right - 44 * s, display.Top + 15 * s, 16 * s, 16 * s); }
                g.Restore(state);
            }
            else PaintUi.Glyph(g, "power", new RectangleF(display.X + (display.Width - 38 * s) / 2, display.Y + (display.Height - 38 * s) / 2, 38 * s, 38 * s), palette.Accent, 1.4f);
            using (var pen = new Pen(palette.Line, 3 * s)) { g.DrawLine(pen, cx - 8 * s, screen.Bottom + 2 * s, cx - 8 * s, screen.Bottom + 25 * s); g.DrawLine(pen, cx - 39 * s, screen.Bottom + 26 * s, cx + 23 * s, screen.Bottom + 26 * s); }
            using (var title = new Font("Segoe UI Semibold", 17)) PaintUi.Text(g, Active ? Slideshow ? L10n.T("Seu álbum, em pausa.") : L10n.T("Hora de descansar.") : L10n.T("No seu controle."), title, palette.Text, new Rectangle(pad, (int)(315 * s), textWidth, (int)(64 * s)), TextFormatFlags.WordBreak);
            string description = !Active ? L10n.T("A ação automática está pausada.") : L10n.Format(Slideshow ? "Suas imagens em todas as telas, após {0} sem atividade." : "Todos os monitores desligam após {0} sem atividade.", Wait);
            PaintUi.Text(g, description, Font, palette.Muted, new Rectangle(pad, (int)(393 * s), textWidth, (int)(89 * s)), TextFormatFlags.WordBreak);
            using (var pen = new Pen(palette.Line, s)) g.DrawLine(pen, pad, Height - 76 * s, Width - pad - 5 * s, Height - 76 * s);
            using (var small = new Font("Segoe UI", 8.6f)) PaintUi.Text(g, L10n.T("Volte com um toque no teclado\nou um movimento do mouse."), small, palette.Muted, new Rectangle(pad, Height - (int)(62 * s), textWidth, (int)(48 * s)), TextFormatFlags.WordBreak);
        }
    }

    internal sealed class FolderField : GlassPanel
    {
        internal readonly TextBox Entry;
        internal FolderField()
        {
            Inset = true; Size = new Size(300, 42); Margin = Padding.Empty;
            Entry = new TextBox { BorderStyle = BorderStyle.None, AccessibleName = "Pasta de imagens", TabIndex = 0, Margin = Padding.Empty }; Controls.Add(Entry);
        }
        protected override void OnLayout(LayoutEventArgs e)
        { base.OnLayout(e); if (Entry != null) Entry.SetBounds(PaintUi.S(this, 12), (Height - Entry.PreferredHeight) / 2 - 2, Math.Max(20, Width - PaintUi.S(this, 28)), Entry.PreferredHeight); }
        public override void SetPalette(Palette p) { base.SetPalette(p); Entry.BackColor = p.Inset; Entry.ForeColor = p.Text; }
    }

    internal sealed class SettingsForm : Form
    {
        private readonly Settings original;
        private readonly Toggle enabled, startup, recursive, shuffle;
        private readonly ThemeToggle themeToggle;
        private readonly ModeTile monitors, slideshow;
        private readonly NumberField idle, interval;
        private readonly SoftButton[] units;
        private readonly FolderField folder;
        private readonly Panel gallery, idleHelp;
        private readonly OverviewPanel overview;
        private Palette palette;
        private Appearance appearance;
        private int unitIndex;
        private bool themeChanging, languageChanging;
        private Language language;
        private readonly ComboBox languagePicker;
        private readonly LocalizedControls localized;
        internal event Action<Language> LanguageRequested;
        internal Language CurrentLanguage { get { return language; } }
        internal event Action<Settings> SaveRequested;
        internal event Action<Appearance> ThemeRequested;
        internal Appearance CurrentAppearance { get { return appearance; } }

        internal SettingsForm(Settings settings)
        {
            SuspendLayout(); language = settings.Language; L10n.Current = language; original = settings.Copy(); appearance = settings.Appearance; palette = new Palette(appearance);
            Text = "Screen Care"; Icon = Theme.MakeIcon(); Font = new Font("Segoe UI", 9.5f);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(944, 740); MinimumSize = new Size(900, 610); StartPosition = FormStartPosition.CenterScreen; MaximizeBox = false;
            var root = Table(1); root.Dock = DockStyle.Fill; root.RowCount = 3; root.AutoSize = false;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70)); Controls.Add(root);
            var header = Table(3); header.Dock = DockStyle.Fill; header.Padding = new Padding(26, 18, 28, 12);
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 184)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 196));
            var brand = Table(1); brand.Dock = DockStyle.Fill;
            brand.Controls.Add(Label("Screen Care", 18, true, false, 32)); brand.Controls.Add(Label("Um descanso para suas telas.", 9, false, true, 21));
            themeToggle = new ThemeToggle { Checked = appearance == Appearance.Dark, Anchor = AnchorStyles.Right | AnchorStyles.Top, Margin = new Padding(0, 5, 0, 0), TabIndex = 0 };
            languagePicker = L10n.LanguagePicker(language);
            languagePicker.Anchor = AnchorStyles.Right | AnchorStyles.Top; languagePicker.Margin = new Padding(0, 10, 18, 0);
            header.Controls.Add(brand, 0, 0); header.Controls.Add(languagePicker, 1, 0); header.Controls.Add(themeToggle, 2, 0); root.Controls.Add(header, 0, 0);
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent, Padding = new Padding(24, 0, 24, 0), Margin = Padding.Empty };
            root.Controls.Add(scroll, 0, 1);
            var columns = Table(2); columns.AutoSizeMode = AutoSizeMode.GrowAndShrink; columns.MinimumSize = new Size(0, 570); columns.Dock = DockStyle.Top;
            FillRow(columns);
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31)); columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 69)); scroll.Controls.Add(columns);
            overview = new OverviewPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 16, 0), TabStop = false };
            columns.Controls.Add(overview, 0, 0);
            var card = new GlassPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, Padding = new Padding(22, 20, 24, 16), Margin = Padding.Empty }; columns.Controls.Add(card, 1, 0);
            var body = Table(1); body.Dock = DockStyle.Top; card.Controls.Add(body);
            enabled = new Toggle { Checked = settings.Enabled, AccessibleName = "Ativar automação", TabIndex = 0 };
            body.Controls.Add(SettingRow("Automação", "Ativa após um período sem interação", enabled, 48)); body.Controls.Add(Gap(12));
            var modes = Table(2); modes.Dock = DockStyle.Top; modes.Height = 76; modes.AutoSize = false;
            FillRow(modes);
            modes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); modes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            monitors = new ModeTile { Text = "Desligar telas", Caption = "Economizar energia", Glyph = "monitor", Checked = settings.Action == IdleAction.MonitorsOff, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 5, 0), AccessibleName = "Modo desligar monitores", TabIndex = 0 };
            slideshow = new ModeTile { Text = "Slideshow", Caption = "Exibir suas imagens", Glyph = "image", Checked = settings.Action == IdleAction.Slideshow, Dock = DockStyle.Fill, Margin = new Padding(5, 0, 0, 0), AccessibleName = "Modo slideshow", TabIndex = 1 };
            modes.Controls.Add(monitors, 0, 0); modes.Controls.Add(slideshow, 1, 0); body.Controls.Add(modes); body.Controls.Add(Gap(18));
            var delay = Table(3); delay.Dock = DockStyle.Top; delay.Height = 44; delay.AutoSize = false;
            FillRow(delay);
            delay.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); delay.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 134)); delay.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            delay.Controls.Add(Label("Tempo de espera", 9.5f, true, false, 38), 0, 0);
            idle = new NumberField { Dock = DockStyle.Fill, TabIndex = 0 }; idle.SetName("Tempo de espera");
            unitIndex = settings.IdleSeconds % 60 == 0 ? 1 : 0; idle.Value = unitIndex == 1 ? settings.IdleSeconds / 60 : settings.IdleSeconds; delay.Controls.Add(idle, 1, 0);
            var unitRow = Table(3); unitRow.Dock = DockStyle.Fill; unitRow.Margin = new Padding(8, 0, 0, 0); unitRow.AutoSize = false;
            FillRow(unitRow);
            units = new SoftButton[3]; string[] unitLabels = { "seg", "min", "h" }, unitNames = { "Segundos", "Minutos", "Horas" };
            for (int i = 0; i < units.Length; i++)
            {
                int index = i; unitRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
                units[i] = new SoftButton { Text = unitLabels[i], AccessibleName = unitNames[i], Dock = DockStyle.Fill, Selected = i == unitIndex, TabIndex = i };
                units[i].Click += delegate { SelectUnit(index); }; unitRow.Controls.Add(units[i], i, 0);
            }
            delay.Controls.Add(unitRow, 2, 0); body.Controls.Add(delay); body.Controls.Add(Gap(18));
            gallery = new Panel { Height = 224, Dock = DockStyle.Top, Margin = Padding.Empty, BackColor = Color.Transparent };
            var album = Table(1); album.Dock = DockStyle.Fill; album.AutoSize = false; gallery.Controls.Add(album);
            album.Controls.Add(Label("Pasta de imagens", 9.5f, true, false, 25));
            var pathRow = Table(2); pathRow.Dock = DockStyle.Top; pathRow.Height = 44; pathRow.AutoSize = false;
            FillRow(pathRow);
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 103));
            folder = new FolderField { Dock = DockStyle.Fill }; folder.Entry.Text = settings.ImageFolder;
            var browse = new SoftButton { Text = "Escolher…", Dock = DockStyle.Fill, Margin = new Padding(8, 0, 0, 0), AccessibleName = "Escolher pasta de imagens" };
            browse.Click += delegate { using (var dialog = new FolderBrowserDialog { Description = L10n.T("Escolha a pasta com as imagens do slideshow"), ShowNewFolderButton = false, SelectedPath = Directory.Exists(folder.Entry.Text) ? folder.Entry.Text : "" }) if (dialog.ShowDialog(this) == DialogResult.OK) folder.Entry.Text = dialog.SelectedPath; };
            pathRow.Controls.Add(folder, 0, 0); pathRow.Controls.Add(browse, 1, 0); album.Controls.Add(pathRow);
            album.Controls.Add(Label("Com o Windows bloqueado, os monitores serão desligados.", 8.2f, false, true, 25)); album.Controls.Add(Gap(8));
            var cadence = Table(3); cadence.Height = 43; cadence.Dock = DockStyle.Top; cadence.AutoSize = false;
            FillRow(cadence);
            cadence.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); cadence.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 134)); cadence.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
            cadence.Controls.Add(Label("Trocar a imagem a cada", 9, false, false, 38), 0, 0);
            interval = new NumberField { Minimum = 2, Maximum = 3600, Value = settings.SlideSeconds, Dock = DockStyle.Fill }; interval.SetName("Intervalo entre imagens");
            cadence.Controls.Add(interval, 1, 0); var seconds = Label("segundos", 8.5f, false, true, 38); seconds.Margin = new Padding(9, 0, 0, 0); cadence.Controls.Add(seconds, 2, 0); album.Controls.Add(cadence); album.Controls.Add(Gap(10));
            var options = Table(2); options.Dock = DockStyle.Top; options.AutoSize = false; options.Height = 40;
            FillRow(options);
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            recursive = new Toggle { Checked = settings.IncludeSubfolders, AccessibleName = "Incluir subpastas" }; shuffle = new Toggle { Checked = settings.Shuffle, AccessibleName = "Ordem aleatória" };
            var subfolders = SettingRow("Subpastas", null, recursive, 36); subfolders.Margin = new Padding(0, 0, 14, 0);
            options.Controls.Add(subfolders, 0, 0); options.Controls.Add(SettingRow("Ordem aleatória", null, shuffle, 36), 1, 0); album.Controls.Add(options); body.Controls.Add(gallery);
            idleHelp = new GlassPanel { Inset = true, Height = 224, Dock = DockStyle.Top, Margin = Padding.Empty, Padding = new Padding(20, 26, 20, 18) };
            var help = Table(1); help.Dock = DockStyle.Top; idleHelp.Controls.Add(help);
            help.Controls.Add(Label("Só a sua atividade conta.", 13, true, false, 38));
            help.Controls.Add(Label("Teclado, mouse e touchpad reiniciam a espera.\n\nVídeos e músicas continuam tocando enquanto as telas descansam.", 9.5f, false, true, 122));
            body.Controls.Add(idleHelp); body.Controls.Add(Gap(18));
            startup = new Toggle { Checked = settings.StartWithWindows, AccessibleName = "Iniciar com o Windows" };
            body.Controls.Add(SettingRow("Iniciar com o Windows", "Retomar as últimas configurações", startup, 49));
            var footer = Table(3); footer.AutoSize = false; footer.Dock = DockStyle.Fill; footer.Padding = new Padding(28, 10, 27, 13);
            FillRow(footer);
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            footer.Controls.Add(Label("Fechar mantém o app na bandeja.\nCtrl+Alt+F11: desligar telas em 2 segundos.", 8.5f, false, true, 43), 0, 0);
            var cancel = new SoftButton { Text = "Cancelar", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0) }; var save = new SoftButton { Text = "Salvar configurações", Primary = true, Dock = DockStyle.Fill };
            cancel.Click += delegate { Close(); }; save.Click += delegate { Save(); };
            footer.Controls.Add(cancel, 1, 0); footer.Controls.Add(save, 2, 0); root.Controls.Add(footer, 0, 2); AcceptButton = save; CancelButton = cancel;
            enabled.CheckedChanged += delegate { UpdateOverview(); }; monitors.CheckedChanged += delegate { UpdateMode(); }; slideshow.CheckedChanged += delegate { UpdateMode(); };
            idle.ValueChanged += delegate { UpdateOverview(); }; themeToggle.CheckedChanged += delegate { ChangeTheme(themeToggle.Checked ? Appearance.Dark : Appearance.Light); };
            localized = new LocalizedControls(this); localized.Apply(language);
            languagePicker.SelectedIndexChanged += delegate { ChangeLanguage((Language)languagePicker.SelectedIndex); };
            UpdateMode(); ApplyAppearance(); ResumeLayout(true);
        }
        private static TableLayoutPanel Table(int columns)
        { return new TableLayoutPanel { ColumnCount = columns, AutoSize = true, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty }; }
        private static void FillRow(TableLayoutPanel row) { row.RowCount = 1; row.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); }
        private static Label Label(string text, float size, bool bold, bool muted, int height)
        { return new Label { Text = text, Height = height, Dock = DockStyle.Top, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Font = new Font(bold ? "Segoe UI Semibold" : "Segoe UI", size), BackColor = Color.Transparent, Tag = muted ? "muted" : "text", Margin = Padding.Empty }; }
        private static Control Gap(int height) { return new Panel { Height = height, Dock = DockStyle.Top, Margin = Padding.Empty, BackColor = Color.Transparent, TabStop = false }; }
        private static TableLayoutPanel SettingRow(string title, string description, Toggle toggle, int height)
        {
            var row = Table(2); row.AutoSizeMode = AutoSizeMode.GrowAndShrink; row.Dock = DockStyle.Top; row.MinimumSize = new Size(0, height);
            FillRow(row);
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
            var labels = Table(1); labels.Dock = DockStyle.Fill; labels.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            labels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var name = Label(title, 9.5f, true, false, description == null ? height : 24); name.Cursor = Cursors.Hand; name.Click += delegate { toggle.Checked = !toggle.Checked; };
            // Let text determine its height after DPI, font or language changes.
            // Fixed-height nested tables could clip the startup caption to a thin line.
            name.AutoSize = true; name.MinimumSize = new Size(0, description == null ? height : 24);
            labels.Controls.Add(name);
            if (description != null)
            {
                var caption = Label(description, 8.5f, false, true, 21);
                caption.AutoSize = true; caption.MinimumSize = new Size(0, 21); labels.Controls.Add(caption);
            }
            toggle.Anchor = AnchorStyles.Right; row.Controls.Add(labels, 0, 0); row.Controls.Add(toggle, 1, 0); return row;
        }
        private void SelectUnit(int index) { unitIndex = index; for (int i = 0; i < units.Length; i++) { units[i].Selected = i == index; units[i].Invalidate(); } UpdateOverview(); }
        private void UpdateMode() { if (gallery == null || overview == null) return; gallery.Visible = slideshow.Checked; idleHelp.Visible = !slideshow.Checked; UpdateOverview(); }
        private void UpdateOverview()
        {
            if (overview == null || idle == null) return;
            overview.Slideshow = slideshow.Checked; overview.Active = enabled.Checked;
            overview.Wait = idle.Valid ? L10n.Duration(idle.Value, unitIndex, language) : L10n.T("o tempo escolhido"); overview.Invalidate();
        }
        internal void ChangeLanguage(Language next)
        {
            if (languageChanging || language == next) return;
            languageChanging = true;
            try
            {
                if (LanguageRequested != null) LanguageRequested(next);
                language = next; original.Language = next; L10n.Current = next;
                languagePicker.SelectedIndex = (int)next;
                SuspendLayout(); localized.Apply(next); ResumeLayout(true);
                UpdateOverview(); Invalidate(true);
            }
            catch (Exception ex)
            {
                languagePicker.SelectedIndex = (int)language;
                MessageBox.Show(this, L10n.T("Não foi possível salvar o idioma. ") + ex.Message, "Screen Care", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { languageChanging = false; }
        }
        internal void ChangeTheme(Appearance next)
        {
            if (themeChanging || appearance == next) return; themeChanging = true;
            try { if (ThemeRequested != null) ThemeRequested(next); appearance = next; original.Appearance = next; themeToggle.Checked = next == Appearance.Dark; ApplyAppearance(); }
            catch (Exception ex) { themeToggle.Checked = appearance == Appearance.Dark; MessageBox.Show(this, L10n.T("Não foi possível salvar o tema. ") + ex.Message, "Screen Care", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { themeChanging = false; }
        }
        private void ApplyAppearance() { palette = new Palette(appearance); BackColor = palette.Background; ForeColor = palette.Text; PaintUi.Apply(this, palette); PaintUi.TitleBar(this, palette); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); if (palette != null) PaintUi.TitleBar(this, palette); }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e); var work = Screen.FromControl(this).WorkingArea;
            if (Height > work.Height - 24) Height = Math.Max(MinimumSize.Height, work.Height - 24);
            if (Width > work.Width - 24) Width = Math.Max(MinimumSize.Width, work.Width - 24);
        }
        internal Settings ReadDraft()
        {
            if (!idle.Valid) throw new InvalidOperationException(L10n.T("Informe um tempo de espera válido."));
            long seconds = (long)idle.Value * (unitIndex == 2 ? 3600 : unitIndex == 1 ? 60 : 1);
            if (seconds < 10 || seconds > 86400) throw new InvalidOperationException(L10n.T("Escolha um tempo entre 10 segundos e 24 horas."));
            if (slideshow.Checked && !interval.Valid) throw new InvalidOperationException(L10n.T("Escolha um intervalo entre 2 e 3600 segundos para as imagens."));
            var next = original.Copy(); next.Enabled = enabled.Checked; next.Action = slideshow.Checked ? IdleAction.Slideshow : IdleAction.MonitorsOff;
            next.IdleSeconds = (int)seconds; next.ImageFolder = folder.Entry.Text.Trim(); next.SlideSeconds = interval.Valid ? interval.Value : original.SlideSeconds;
            next.IncludeSubfolders = recursive.Checked; next.Shuffle = shuffle.Checked; next.StartWithWindows = startup.Checked; next.Appearance = appearance; return next;
        }
        internal void SetEnabled(bool value)
        { enabled.Checked = value; }
        private void Save()
        {
            try { Settings next = ReadDraft(); if (next.Action == IdleAction.Slideshow && !Directory.Exists(next.ImageFolder)) throw new InvalidOperationException(L10n.T("Escolha uma pasta de imagens disponível.")); if (SaveRequested != null) SaveRequested(next); Close(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Screen Care", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        }
    }
}
