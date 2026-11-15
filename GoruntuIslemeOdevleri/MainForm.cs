using GoruntuIslemeOdevleri.UI;

namespace GoruntuIslemeOdevleri;

/// <summary>
/// Ana pencere: menü çubuğu, solda modül listesi, ortada etkin modül, altta durum çubuğu.
/// Kısayollar: Ctrl+1…7 modül değiştirir, F1 rehber panelini açar/kapatır.
/// </summary>
internal sealed class MainForm : Form
{
    private readonly (string Icon, string Title, string Subtitle, Control Page)[] _modules;
    private readonly Panel _content;
    private readonly NavList _nav;
    private readonly ToolStripStatusLabel _statusMessage;
    private readonly ToolStripStatusLabel _statusImage;
    private readonly ToolStripStatusLabel _statusModule;

    public MainForm()
    {
        Text = "Görüntü İşleme Laboratuvarı";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new System.Drawing.Size(1180, 720);
        WindowState = FormWindowState.Maximized;
        BackColor = Theme.Window;
        ForeColor = Theme.Ink;
        Font = Theme.UiFont;
        KeyPreview = true;

        _modules =
        [
            ("", "Başlangıç", "Kılavuz ve sözlük", new StartPage(i => Select(i))),
            ("", "Ceviz / Fındık", "Ödev 1 · sayma", new NutsPage()),
            ("", "Arka Plan", "Ödev 2 · GrabCut", new ForegroundPage()),
            ("", "Tuz / Biber", "Ödev 3 · morfoloji", new NoisePage()),
            ("", "Bozuk Para", "Ödev 4 · tutar", new CoinPage()),
            ("", "Uydu Alanı", "Ödev 5 · alan/çevre", new LakePage()),
            ("", "Eşikleme", "Araştırma", new ThresholdResearchPage()),
        ];

        // --- Menü çubuğu ---
        var menu = new MenuStrip { Renderer = Theme.Renderer, BackColor = Theme.Window, ForeColor = Theme.Ink, Padding = new Padding(6, 3, 0, 3) };
        var file = new ToolStripMenuItem("&Dosya");
        file.DropDownItems.Add(MenuItem("Çı&kış", Keys.Alt | Keys.F4, (_, _) => Close()));
        var view = new ToolStripMenuItem("&Görünüm");
        view.DropDownItems.Add(MenuItem("Rehber panelini aç/kapat", Keys.F1, (_, _) => ToggleGuide()));
        view.DropDownItems.Add(new ToolStripSeparator());
        for (int i = 0; i < _modules.Length; i++)
        {
            int index = i;
            view.DropDownItems.Add(MenuItem(_modules[i].Title, Keys.Control | (Keys.D1 + i), (_, _) => Select(index)));
        }
        var help = new ToolStripMenuItem("&Yardım");
        help.DropDownItems.Add(MenuItem("Kullanım kılavuzu", Keys.None, (_, _) => Select(0)));
        help.DropDownItems.Add(MenuItem("Hakkında", Keys.None, (_, _) => MessageBox.Show(this,
            "Görüntü İşleme Laboratuvarı\n8. hafta ödevleri: morfolojik işlemler, bölge bulma ve ölçüm.\n\nC# · .NET 8 · OpenCvSharp5",
            "Hakkında", MessageBoxButtons.OK, MessageBoxIcon.Information)));
        foreach (var m in new[] { file, view, help })
        {
            m.ForeColor = Theme.Ink;
            foreach (ToolStripItem sub in m.DropDownItems) sub.ForeColor = Theme.Ink;
            menu.Items.Add(m);
        }
        MainMenuStrip = menu;

        // --- Sol modül listesi ---
        _nav = new NavList(_modules.Select(m => (m.Icon, m.Title, m.Subtitle)).ToArray()) { Dock = DockStyle.Left, Width = 230 };
        _nav.SelectedChanged += i => Show(i);

        // --- İçerik ---
        _content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Page };
        foreach (var m in _modules)
        {
            m.Page.Dock = DockStyle.Fill;
            m.Page.Visible = false;
            _content.Controls.Add(m.Page);
        }

        // --- Durum çubuğu ---
        var status = new StatusStrip { Renderer = Theme.Renderer, BackColor = Theme.Window, SizingGrip = false };
        _statusMessage = new ToolStripStatusLabel("Hazır") { Spring = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Muted };
        _statusImage = new ToolStripStatusLabel("") { ForeColor = Theme.Muted, BorderSides = ToolStripStatusLabelBorderSides.Left, BorderStyle = Border3DStyle.Etched };
        _statusModule = new ToolStripStatusLabel("") { ForeColor = Theme.Accent, BorderSides = ToolStripStatusLabelBorderSides.Left, BorderStyle = Border3DStyle.Etched };
        status.Items.AddRange([_statusMessage, _statusImage, _statusModule]);
        AppStatus.Message += text => _statusMessage.Text = $"{DateTime.Now:HH:mm:ss}  {text}";
        AppStatus.ImageInfo += text => _statusImage.Text = text;

        var divider = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = Theme.Line };
        Controls.Add(_content);
        Controls.Add(divider);
        Controls.Add(_nav);
        Controls.Add(status);
        Controls.Add(menu);

        Select(0);
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Windows 10 (20H1+) ve 11'de başlık çubuğunu koyu temaya alır; desteklenmezse sessizce geçer.</summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int on = 1;
        if (DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int)) != 0)
            DwmSetWindowAttribute(Handle, 19, ref on, sizeof(int));
    }

    private void Select(int index) => _nav.SelectedIndex = index;

    private void Show(int index)
    {
        for (int i = 0; i < _modules.Length; i++)
            _modules[i].Page.Visible = i == index;
        _statusModule.Text = $"{_modules[index].Title}  ·  Ctrl+{index + 1}";
        AppStatus.Report($"{_modules[index].Title} açıldı" + (index == 0 ? "" : " · F1 rehberi açar/kapatır"));
    }

    private void ToggleGuide()
    {
        if (_modules[_nav.SelectedIndex].Page is PageBase page)
            page.ToggleGuide();
    }

    private static ToolStripMenuItem MenuItem(string text, Keys keys, EventHandler click)
    {
        var item = new ToolStripMenuItem(text, null, click) { ShortcutKeys = keys == Keys.None ? Keys.None : keys };
        if (keys == (Keys.Alt | Keys.F4)) { item.ShortcutKeys = Keys.None; item.ShortcutKeyDisplayString = "Alt+F4"; }
        return item;
    }
}

/// <summary>Soldaki dikey modül listesi: ikon, başlık, alt başlık; seçili satır vurgu çubuğuyla.</summary>
internal sealed class NavList : Control
{
    private readonly (string Icon, string Title, string Subtitle)[] _items;
    private const int RowHeight = 56;
    private const int Top0 = 12;
    private int _selected = -1;
    private int _hover = -1;
    private readonly Font _icon = Theme.IconFont(15);
    private readonly Font _title = new("Segoe UI", 10f, FontStyle.Bold);
    private readonly Font _sub = new("Segoe UI", 8.25f);

    public event Action<int>? SelectedChanged;

    public NavList((string, string, string)[] items)
    {
        _items = items;
        DoubleBuffered = true;
        BackColor = Theme.Panel;
        Cursor = Cursors.Hand;
    }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (value == _selected || value < 0 || value >= _items.Length) return;
            _selected = value;
            Invalidate();
            SelectedChanged?.Invoke(value);
        }
    }

    private int HitTest(int y) => y < Top0 ? -1 : (y - Top0) / RowHeight is var i && i < _items.Length ? i : -1;

    protected override void OnMouseMove(MouseEventArgs e) { int h = HitTest(e.Y); if (h != _hover) { _hover = h; Invalidate(); } }
    protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); }
    protected override void OnMouseClick(MouseEventArgs e) { int h = HitTest(e.Y); if (h >= 0) SelectedIndex = h; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        for (int i = 0; i < _items.Length; i++)
        {
            var row = new Rectangle(8, Top0 + i * RowHeight, Width - 16, RowHeight - 4);
            if (i == _selected || i == _hover)
            {
                using var b = new SolidBrush(i == _selected ? Theme.AccentSoft : Theme.Hover);
                g.FillRectangle(b, row);
            }
            if (i == _selected)
            {
                using var a = new SolidBrush(Theme.Accent);
                g.FillRectangle(a, row.Left, row.Top + 10, 3, row.Height - 20);
            }
            Color iconColor = i == _selected ? Theme.Accent : Theme.Muted;
            TextRenderer.DrawText(g, _items[i].Icon, _icon, new Rectangle(row.Left + 12, row.Top, 32, row.Height), iconColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, _items[i].Title, _title, new System.Drawing.Point(row.Left + 52, row.Top + 9), Theme.Ink);
            TextRenderer.DrawText(g, _items[i].Subtitle, _sub, new System.Drawing.Point(row.Left + 53, row.Top + 29), Theme.Muted);
            if (i == 0)
            {
                using var pen = new Pen(Theme.Line);
                g.DrawLine(pen, row.Left, row.Bottom + 2, row.Right, row.Bottom + 2);
            }
        }
        using var foot = new Font("Segoe UI", 8f);
        TextRenderer.DrawText(g, "Ctrl+1…7 modül · F1 rehber", foot, new System.Drawing.Point(16, Height - 26), Theme.Muted);
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
}
