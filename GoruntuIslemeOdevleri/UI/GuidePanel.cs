namespace GoruntuIslemeOdevleri.UI;

/// <summary>Bir sekmenin rehber içeriği: ne işe yarar, adım adım kullanım, arkadaki teori ve ipuçları.</summary>
internal sealed record GuideContent(
    string Title,
    string Goal,
    string[] Steps,
    (string Term, string Explanation)[] Theory,
    string[] Tips);

/// <summary>Sekmenin sağında duran, açılıp kapanabilen "Rehber" paneli.</summary>
internal sealed class GuidePanel : Panel
{
    private readonly FlowLayoutPanel _flow;

    public GuidePanel()
    {
        Dock = DockStyle.Right;
        Width = 360;
        BackColor = Theme.GuideBack;
        Padding = new Padding(0);

        var header = new Label
        {
            Text = "REHBER  ·  KULLANIM VE KONU ANLATIMI",
            Dock = DockStyle.Top,
            Height = 26,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            ForeColor = Theme.Muted,
            BackColor = Theme.Panel,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0)
        };
        var accentLine = new Panel { Dock = DockStyle.Top, Height = 2, BackColor = Theme.Accent };
        _flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(16, 12, 12, 16)
        };
        _flow.Resize += (_, _) => FitWidths();
        BackColor = Theme.Page;
        Controls.Add(_flow);
        Controls.Add(accentLine);
        Controls.Add(header);
    }

    public void SetContent(GuideContent c)
    {
        _flow.SuspendLayout();
        _flow.Controls.Clear();
        AddText(c.Title, 13, FontStyle.Bold, Theme.Ink, 2);
        AddText(c.Goal, 10, FontStyle.Regular, Theme.Muted, 14);

        AddSection("Adım adım kullanım");
        for (int i = 0; i < c.Steps.Length; i++)
            AddStep(i + 1, c.Steps[i]);

        AddSection("Nasıl çalışır?");
        foreach ((string term, string explanation) in c.Theory)
        {
            AddText(term, 10, FontStyle.Bold, Theme.Accent, 0);
            AddText(explanation, 9.5f, FontStyle.Regular, Theme.Ink, 10);
        }

        if (c.Tips.Length > 0)
        {
            AddSection("İpuçları");
            foreach (string tip in c.Tips)
                AddCallout("💡  " + tip);
        }
        _flow.ResumeLayout();
        FitWidths();
    }

    private void AddSection(string text)
    {
        var label = new Label
        {
            Text = text.ToUpper(new System.Globalization.CultureInfo("tr-TR")),
            AutoSize = false,
            Height = 30,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Theme.Accent,
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0, 10, 0, 6)
        };
        label.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Line, 1);
            e.Graphics.DrawLine(pen, 0, label.Height - 1, label.Width, label.Height - 1);
        };
        _flow.Controls.Add(label);
    }

    private void AddStep(int number, string text)
    {
        var row = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var badge = new Label
        {
            Text = number.ToString(),
            Size = new System.Drawing.Size(26, 26),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Theme.Accent,
            Margin = new Padding(0, 1, 6, 0)
        };
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9.75f),
            ForeColor = Theme.Ink,
            Margin = new Padding(0, 3, 0, 0)
        };
        row.Controls.Add(badge, 0, 0);
        row.Controls.Add(label, 1, 0);
        _flow.Controls.Add(row);
    }

    private void AddCallout(string text)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Theme.Ink,
            BackColor = Theme.TipBack,
            Padding = new Padding(10, 8, 10, 8),
            Margin = new Padding(0, 0, 0, 8)
        };
        _flow.Controls.Add(label);
    }

    private void AddText(string text, float size, FontStyle style, Color color, int bottom)
    {
        _flow.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", size, style),
            ForeColor = color,
            Margin = new Padding(0, 0, 0, bottom)
        });
    }

    /// <summary>Etiketlerin panel genişliğinde satır kaydırması için en büyük genişliği ayarlar.</summary>
    private void FitWidths()
    {
        int width = Math.Max(120, _flow.ClientSize.Width - _flow.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
        foreach (Control c in _flow.Controls)
        {
            switch (c)
            {
                case TableLayoutPanel row:
                    row.Width = width;
                    if (row.GetControlFromPosition(1, 0) is Label l)
                        l.MaximumSize = new System.Drawing.Size(width - 40, 0);
                    break;
                case Label label when label.AutoSize:
                    label.MaximumSize = new System.Drawing.Size(width, 0);
                    break;
                case Label fixedLabel:
                    fixedLabel.Width = width;
                    break;
            }
        }
    }
}
