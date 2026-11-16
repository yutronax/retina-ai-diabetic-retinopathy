namespace GoruntuIslemeOdevleri.UI;

/// <summary>
/// Her modül sayfasının iskeleti: üstte yerel araç çubuğu (ToolStrip), ortada girdi | sonuç görüntüleri,
/// altta sonuç metni, sağda Rehber. Aralarındaki ayırıcılar sürüklenerek boyutlandırılabilir.
/// </summary>
internal abstract class PageBase : UserControl
{
    protected readonly ToolStrip Toolbar;
    protected readonly ImageDisplay InputView;
    protected readonly ImageDisplay OutputView;
    protected readonly TextBox Results;
    private readonly GuidePanel _guide;
    private readonly SplitContainer _guideSplit;
    private ToolStripButton? _guideButton;

    protected PageBase(string inputTitle, string outputTitle)
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Page;

        Toolbar = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            Renderer = Theme.Renderer,
            BackColor = Theme.Panel,
            ForeColor = Theme.Ink,
            Padding = new Padding(8, 6, 8, 6),
            ImageScalingSize = new System.Drawing.Size(16, 16),
            CanOverflow = true
        };

        InputView = new ImageDisplay(inputTitle);
        OutputView = new ImageDisplay(outputTitle);
        var imageSplit = Split(Orientation.Vertical, BorderStyle.None);
        imageSplit.Panel1.Controls.Add(InputView);
        imageSplit.Panel2.Controls.Add(OutputView);
        imageSplit.SizeChanged += (_, _) => { if (imageSplit.Width > 100) imageSplit.SplitterDistance = imageSplit.Width / 2; };

        Results = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Theme.Window,
            ForeColor = Theme.Ink,
            Font = new Font("Cascadia Mono", 10f),
            BorderStyle = BorderStyle.None
        };
        if (Results.Font.Name != "Cascadia Mono") Results.Font = new Font("Consolas", 10f);
        Results.TextChanged += (_, _) =>
        {
            string first = Results.Lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "";
            if (first.Length > 0) AppStatus.Report(first.Trim());
        };
        var resultsHeader = new Label
        {
            Text = "SONUÇLAR",
            Dock = DockStyle.Top,
            Height = 26,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            ForeColor = Theme.Muted,
            BackColor = Theme.Panel,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };
        var resultsBody = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Window, Padding = new Padding(10, 6, 6, 6) };
        resultsBody.Controls.Add(Results);

        var mainSplit = Split(Orientation.Horizontal, BorderStyle.None);
        mainSplit.Panel1.Controls.Add(imageSplit);
        mainSplit.Panel2.Controls.Add(resultsBody);
        mainSplit.Panel2.Controls.Add(resultsHeader);
        mainSplit.SizeChanged += (_, _) => { if (mainSplit.Height > 300) mainSplit.SplitterDistance = mainSplit.Height - 170; };

        _guide = new GuidePanel { Dock = DockStyle.Fill };
        _guideSplit = Split(Orientation.Vertical, BorderStyle.None);
        _guideSplit.FixedPanel = FixedPanel.Panel2;
        _guideSplit.Panel1.Controls.Add(mainSplit);
        _guideSplit.Panel2.Controls.Add(_guide);
        _guideSplit.Panel2Collapsed = true;
        _guideSplit.SizeChanged += (_, _) => { if (_guideSplit.Width > 700) _guideSplit.SplitterDistance = _guideSplit.Width - 360; };

        Controls.Add(_guideSplit);
        Controls.Add(Toolbar);
    }

    private static SplitContainer Split(Orientation orientation, BorderStyle border) => new()
    {
        Dock = DockStyle.Fill,
        Orientation = orientation,
        BorderStyle = border,
        SplitterWidth = 5,
        BackColor = Theme.Line,       // ayırıcı çizgi rengi
        Panel1 = { BackColor = Theme.Page },
        Panel2 = { BackColor = Theme.Page }
    };

    /// <summary>Sağdaki rehberi doldurur ve araç çubuğunun sağ ucuna aç/kapa düğmesi koyar.</summary>
    protected void SetGuide(GuideContent content)
    {
        _guide.SetContent(content);
        _guideSplit.Panel2Collapsed = false;
        _guideButton = new ToolStripButton("Rehber  (F1)")
        {
            Alignment = ToolStripItemAlignment.Right,
            CheckOnClick = true,
            Checked = true,
            ForeColor = Theme.Ink,
            Margin = new Padding(4, 1, 0, 2),
            Padding = new Padding(6, 0, 6, 0)
        };
        _guideButton.Click += (_, _) => _guideSplit.Panel2Collapsed = !_guideButton.Checked;
        Toolbar.Items.Add(_guideButton);
        if (Results.TextLength == 0)
            Results.Text = "Başlamak için sağdaki rehberdeki adımları izleyin.";
    }

    /// <summary>F1 / Görünüm menüsünden çağrılır.</summary>
    public void ToggleGuide()
    {
        _guideSplit.Panel2Collapsed = !_guideSplit.Panel2Collapsed;
        if (_guideButton is not null) _guideButton.Checked = !_guideSplit.Panel2Collapsed;
    }

    protected ToolStripButton AddButton(string text, EventHandler click, bool primary = false)
    {
        if (Toolbar.Items.Count > 0 && !(Toolbar.Items[^1] is ToolStripButton or ToolStripDropDownButton))
            Toolbar.Items.Add(new ToolStripSeparator());
        var button = new ToolStripButton(text)
        {
            Tag = primary ? "primary" : null,
            Font = primary ? Theme.UiBold : Theme.UiFont,
            ForeColor = primary ? Color.White : Theme.Ink,
            Padding = new Padding(primary ? 12 : 6, 3, primary ? 12 : 6, 3),
            Margin = new Padding(2, 1, 2, 2)
        };
        button.Click += click;
        button.Click += (_, _) => AppStatus.Report(text + "…");
        Toolbar.Items.Add(button);
        return button;
    }

    /// <summary>
    /// "Örnek fotoğraflar" açılır menüsü: uygulamanın yanındaki Ornekler/&lt;klasör&gt; içindeki gerçek fotoğraflar
    /// (dosya adı menüde görünür) ve isteğe bağlı ek öğeler (ör. sentetik örnek).
    /// </summary>
    protected ToolStripDropDownButton AddSamples(string folder, Action<string> open, params (string Text, Action Click)[] extras)
    {
        if (Toolbar.Items.Count > 0 && !(Toolbar.Items[^1] is ToolStripButton or ToolStripDropDownButton))
            Toolbar.Items.Add(new ToolStripSeparator());
        var menu = new ToolStripDropDownButton("Örnek fotoğraflar")
        {
            ForeColor = Theme.Ink,
            Padding = new Padding(6, 3, 6, 3),
            Margin = new Padding(2, 1, 2, 2)
        };
        menu.DropDown.BackColor = Theme.Surface;
        string dir = Path.Combine(AppContext.BaseDirectory, "Ornekler", folder);
        string[] files = Directory.Exists(dir)
            ? Directory.GetFiles(dir).Where(f => new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff" }
                .Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => f, StringComparer.CurrentCulture).ToArray()
            : [];
        foreach (string file in files)
        {
            string path = file;
            var item = new ToolStripMenuItem(Path.GetFileNameWithoutExtension(file)) { ForeColor = Theme.Ink };
            item.Click += (_, _) =>
            {
                try { open(path); AppStatus.Report("Örnek açıldı: " + Path.GetFileName(path)); }
                catch (Exception ex) { ShowError(ex); }
            };
            menu.DropDownItems.Add(item);
        }
        if (files.Length == 0)
            menu.DropDownItems.Add(new ToolStripMenuItem("(Ornekler klasörü bulunamadı)") { Enabled = false });
        if (extras.Length > 0)
        {
            menu.DropDownItems.Add(new ToolStripSeparator());
            foreach ((string text, Action click) in extras)
            {
                var item = new ToolStripMenuItem(text) { ForeColor = Theme.Ink };
                item.Click += (_, _) => click();
                menu.DropDownItems.Add(item);
            }
        }
        Toolbar.Items.Add(menu);
        return menu;
    }

    /// <summary>Ornekler/&lt;klasör&gt; içindeki ilk fotoğrafın yolu (yoksa null).</summary>
    protected static string? FirstSample(string folder, string? nameContains = null)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Ornekler", folder);
        if (!Directory.Exists(dir)) return null;
        return Directory.GetFiles(dir).OrderBy(f => f, StringComparer.CurrentCulture)
            .FirstOrDefault(f => nameContains is null || Path.GetFileName(f).Contains(nameContains, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>Araç çubuğuna "Görünüm: Adım adım / Yalnız sonuç" seçimini ekler.</summary>
    protected ComboBox AddStepView()
    {
        AddLabel("Görünüm");
        ComboBox combo = AddCombo("Adım adım", "Yalnız sonuç");
        combo.Width = 115;
        return combo;
    }

    /// <summary>
    /// Sağ panelde ya bütün ara adımları başlıklarıyla bir kolajda (4 sütun) ya da yalnız sonucu gösterir.
    /// Adım başlıkları sonuç metninin sonuna da yazılır.
    /// </summary>
    protected void ShowSteps(ComboBox view, IReadOnlyList<(string Title, OpenCvSharp.Mat Image)> steps, OpenCvSharp.Mat result)
    {
        if (view.SelectedIndex == 0 && steps.Count > 0)
        {
            using OpenCvSharp.Mat montage = Core.ImageHelpers.MakeMontage(steps, Math.Min(4, steps.Count), 480, 380);
            OutputView.SetMat(montage);
        }
        else
            OutputView.SetMat(result);
        if (steps.Count > 0)
            Results.AppendText("\r\n\r\nADIMLAR\r\n" + string.Join("\r\n", steps.Select(s => "  " + s.Title)));
    }

    protected ToolStripLabel AddLabel(string text)
    {
        if (Toolbar.Items.Count > 0 && Toolbar.Items[^1] is ToolStripButton)
            Toolbar.Items.Add(new ToolStripSeparator());
        var label = new ToolStripLabel(text) { ForeColor = Theme.Muted, Margin = new Padding(6, 1, 2, 2) };
        Toolbar.Items.Add(label);
        return label;
    }

    protected NumericUpDown AddNumber(decimal minimum, decimal maximum, decimal value, int width = 76, int decimals = 0)
    {
        var numeric = new NumericUpDown
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = Math.Clamp(value, minimum, maximum),
            Width = width,
            DecimalPlaces = decimals,
            BackColor = Theme.Surface,
            ForeColor = Theme.Ink,
            BorderStyle = BorderStyle.FixedSingle
        };
        Toolbar.Items.Add(new ToolStripControlHost(numeric) { Margin = new Padding(2, 2, 6, 2) });
        return numeric;
    }

    protected ComboBox AddCombo(params string[] items)
    {
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 125,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface,
            ForeColor = Theme.Ink
        };
        combo.Items.AddRange(items);
        if (items.Length > 0) combo.SelectedIndex = 0;
        Toolbar.Items.Add(new ToolStripControlHost(combo) { Margin = new Padding(2, 2, 6, 2) });
        return combo;
    }

    protected void ShowError(Exception exception)
    {
        Results.Text = "HATA\r\n" + exception.Message;
        MessageBox.Show(this, exception.Message, "İşlem tamamlanamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    protected static string PickImage(Control owner, string title) => Core.ImageHelpers.SelectImageFile(owner, title);
}
