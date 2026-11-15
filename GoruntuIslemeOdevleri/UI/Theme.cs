namespace GoruntuIslemeOdevleri.UI;

/// <summary>
/// "Koyu stüdyo" teması: görüntü işleme araçlarındaki gibi koyu zemin, turkuaz vurgu.
/// Tüm renkler buradan gelir; başka dosyada sabit renk yazılmaz.
/// </summary>
internal static class Theme
{
    // Yüzeyler (koyudan açığa)
    public static readonly Color Window = Color.FromArgb(24, 25, 28);       // en dış zemin, görüntü alanı
    public static readonly Color Page = Color.FromArgb(30, 31, 35);         // sayfa zemini
    public static readonly Color Panel = Color.FromArgb(37, 39, 44);        // paneller, araç çubuğu
    public static readonly Color Surface = Color.FromArgb(45, 47, 53);      // kart, giriş kutusu
    public static readonly Color Hover = Color.FromArgb(55, 58, 65);
    public static readonly Color Line = Color.FromArgb(58, 61, 68);

    // Metin
    public static readonly Color Ink = Color.FromArgb(230, 232, 235);
    public static readonly Color Muted = Color.FromArgb(152, 158, 168);

    // Vurgu
    public static readonly Color Accent = Color.FromArgb(20, 184, 198);
    public static readonly Color AccentDark = Color.FromArgb(14, 140, 152);
    public static readonly Color AccentSoft = Color.FromArgb(22, 62, 68);
    public static readonly Color Amber = Color.FromArgb(242, 169, 0);
    public static readonly Color TipBack = Color.FromArgb(58, 50, 28);
    public static readonly Color Danger = Color.FromArgb(232, 92, 88);

    // Eski adlar (Rehber ve Başlangıç bunları kullanıyor)
    public static readonly Color GuideBack = Panel;
    public static readonly Color ToolbarBack = Panel;
    public static readonly Color Navy = Window;

    public static readonly Font UiFont = new("Segoe UI", 9f);
    public static readonly Font UiBold = new("Segoe UI", 9f, FontStyle.Bold);

    /// <summary>Windows 11'de "Segoe Fluent Icons", Windows 10'da "Segoe MDL2 Assets" ikon fontu.</summary>
    public static readonly string IconFontName =
        new System.Drawing.Text.InstalledFontCollection().Families.Any(f => f.Name == "Segoe Fluent Icons")
            ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";

    public static Font IconFont(float size) => new(IconFontName, size);

    /// <summary>Menü ve araç çubuklarını koyu temaya boyayan çizici.</summary>
    public static readonly ToolStripProfessionalRenderer Renderer = new DarkRenderer();

    private sealed class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? (e.Item.Tag as string == "primary" ? Color.White : Ink) : Muted;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Tag as string == "primary")
            {
                var r = new Rectangle(Point0, e.Item.Size);
                using var b = new SolidBrush(e.Item.Pressed ? AccentDark : e.Item.Selected ? AccentDark : Accent);
                e.Graphics.FillRectangle(b, r);
                return;
            }
            base.OnRenderButtonBackground(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Ink;
            base.OnRenderArrow(e);
        }

        private static readonly System.Drawing.Point Point0 = new(0, 0);
    }

    private sealed class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripGradientBegin => Panel;
        public override Color ToolStripGradientMiddle => Panel;
        public override Color ToolStripGradientEnd => Panel;
        public override Color ToolStripBorder => Line;
        public override Color MenuStripGradientBegin => Window;
        public override Color MenuStripGradientEnd => Window;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemPressedGradientBegin => Surface;
        public override Color MenuItemPressedGradientEnd => Surface;
        public override Color MenuItemBorder => Line;
        public override Color MenuBorder => Line;
        public override Color ToolStripDropDownBackground => Surface;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color SeparatorDark => Line;
        public override Color SeparatorLight => Line;
        public override Color ButtonSelectedHighlight => Hover;
        public override Color ButtonSelectedGradientBegin => Hover;
        public override Color ButtonSelectedGradientMiddle => Hover;
        public override Color ButtonSelectedGradientEnd => Hover;
        public override Color ButtonSelectedBorder => Line;
        public override Color ButtonPressedGradientBegin => Surface;
        public override Color ButtonPressedGradientMiddle => Surface;
        public override Color ButtonPressedGradientEnd => Surface;
        public override Color ButtonCheckedGradientBegin => AccentSoft;
        public override Color ButtonCheckedGradientMiddle => AccentSoft;
        public override Color ButtonCheckedGradientEnd => AccentSoft;
        public override Color StatusStripGradientBegin => Window;
        public override Color StatusStripGradientEnd => Window;
        public override Color GripDark => Line;
        public override Color GripLight => Panel;
    }
}

/// <summary>Durum çubuğuna mesaj göndermek için uygulama geneli olay.</summary>
internal static class AppStatus
{
    public static event Action<string>? Message;
    public static event Action<string>? ImageInfo;
    public static void Report(string text) => Message?.Invoke(text);
    public static void Image(string text) => ImageInfo?.Invoke(text);
}
