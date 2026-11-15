namespace GoruntuIslemeOdevleri.UI;

internal abstract class PageBase : UserControl
{
    protected readonly FlowLayoutPanel Toolbar;
    protected readonly ImageDisplay InputView;
    protected readonly ImageDisplay OutputView;
    protected readonly TextBox Results;

    protected PageBase(string inputTitle, string outputTitle)
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;

        Toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 76,
            Padding = new Padding(10, 12, 10, 8),
            AutoScroll = true,
            WrapContents = false,
            BackColor = Color.FromArgb(231, 236, 245)
        };

        Results = new TextBox
        {
            Dock = DockStyle.Bottom,
            Height = 148,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(251, 251, 252),
            ForeColor = Color.FromArgb(30, 36, 48),
            Font = new Font("Consolas", 10),
            BorderStyle = BorderStyle.FixedSingle
        };

        InputView = new ImageDisplay(inputTitle);
        OutputView = new ImageDisplay(outputTitle);
        var images = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(6)
        };
        images.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        images.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        images.Controls.Add(InputView, 0, 0);
        images.Controls.Add(OutputView, 1, 0);

        Controls.Add(images);
        Controls.Add(Results);
        Controls.Add(Toolbar);
    }

    protected Button AddButton(string text, EventHandler click, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 40,
            Padding = new Padding(12, 0, 12, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Color.FromArgb(32, 105, 220) : Color.White,
            ForeColor = primary ? Color.White : Color.FromArgb(35, 45, 62),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Margin = new Padding(5, 0, 5, 0)
        };
        button.FlatAppearance.BorderColor = primary ? Color.FromArgb(32, 105, 220) : Color.FromArgb(175, 185, 200);
        button.Click += click;
        Toolbar.Controls.Add(button);
        return button;
    }

    protected Label AddLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(45, 55, 70),
            Margin = new Padding(10, 10, 2, 0)
        };
        Toolbar.Controls.Add(label);
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
            Height = 36,
            DecimalPlaces = decimals,
            Margin = new Padding(2, 5, 4, 0)
        };
        Toolbar.Controls.Add(numeric);
        return numeric;
    }

    protected ComboBox AddCombo(params string[] items)
    {
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 125,
            Height = 36,
            Margin = new Padding(2, 5, 4, 0)
        };
        combo.Items.AddRange(items);
        if (items.Length > 0) combo.SelectedIndex = 0;
        Toolbar.Controls.Add(combo);
        return combo;
    }

    protected void ShowError(Exception exception)
    {
        Results.Text = "HATA\r\n" + exception.Message;
        MessageBox.Show(this, exception.Message, "İşlem tamamlanamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    protected static string PickImage(Control owner, string title) => Core.ImageHelpers.SelectImageFile(owner, title);
}
