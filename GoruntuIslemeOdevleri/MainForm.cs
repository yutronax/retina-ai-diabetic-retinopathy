using GoruntuIslemeOdevleri.UI;

namespace GoruntuIslemeOdevleri;

internal sealed class MainForm : Form
{
    public MainForm()
    {
        Text = "Görüntü İşleme - 8. Hafta Ödevleri";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new System.Drawing.Size(1180, 760);
        WindowState = FormWindowState.Maximized;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9);

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 68,
            BackColor = Color.FromArgb(24, 37, 58)
        };
        var title = new Label
        {
            Text = "MORFOLOJİK GÖRÜNTÜ İŞLEME LABORATUVARI",
            AutoSize = true,
            Location = new System.Drawing.Point(22, 14),
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.White
        };
        var subtitle = new Label
        {
            Text = "5 uygulama + otomatik eşikleme araştırması | C# Windows Forms + OpenCV",
            AutoSize = true,
            Location = new System.Drawing.Point(25, 43),
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(185, 205, 235)
        };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Padding = new System.Drawing.Point(14, 6)
        };
        tabs.TabPages.Add(CreateTab("1 - Ceviz / Fındık", new NutsPage()));
        tabs.TabPages.Add(CreateTab("2 - Arka Plan", new ForegroundPage()));
        tabs.TabPages.Add(CreateTab("3 - Tuz / Biber", new NoisePage()));
        tabs.TabPages.Add(CreateTab("4 - Bozuk Para", new CoinPage()));
        tabs.TabPages.Add(CreateTab("5 - Uydu Alanı", new LakePage()));
        tabs.TabPages.Add(CreateTab("Araştırma - Eşikleme", new ThresholdResearchPage()));

        Controls.Add(tabs);
        Controls.Add(header);
    }

    private static TabPage CreateTab(string title, Control content)
    {
        var page = new TabPage(title) { BackColor = Color.White, Padding = new Padding(3) };
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        return page;
    }
}
