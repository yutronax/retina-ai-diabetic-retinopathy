namespace GoruntuIslemeOdevleri.UI;

/// <summary>
/// Başlangıç ekranı (masaüstü "başlangıç penceresi" düzeni): solda modüller, sağda kullanım akışı ve sözlük.
/// </summary>
internal sealed class StartPage : UserControl
{
    public StartPage(Action<int> openModule)
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Page;
        Padding = new Padding(48, 36, 48, 24);

        var title = new Label
        {
            Text = "Görüntü İşleme Laboratuvarı",
            Dock = DockStyle.Top, Height = 42,
            Font = new Font("Segoe UI Semibold", 20f), ForeColor = Theme.Ink
        };
        var subtitle = new Label
        {
            Text = "8. hafta ödevleri · morfolojik işlemler, bölge bulma ve ölçüm. Bir modül seçerek başla; her modülün sağında kullanım ve konu anlatımı var.",
            Dock = DockStyle.Top, Height = 40,
            Font = new Font("Segoe UI", 10f), ForeColor = Theme.Muted
        };

        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 12, 0, 0) };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 460));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // --- Sol: modüller ---
        var left = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(0, 0, 24, 0) };
        left.Controls.Add(Heading("Modül aç"));
        (string Icon, string Title, string Text, string Key, int Index)[] modules =
        [
            ("", "Ceviz / Fındık", "Bölgeleri renklendir, piksel sayısıyla ayır", "Ctrl+2", 1),
            ("", "Arka Plan", "Kişiyi duvardan ayır, yeni arka plana koy", "Ctrl+3", 2),
            ("", "Tuz / Biber", "Aşındırma, genişletme ve medyan; PSNR", "Ctrl+4", 3),
            ("", "Bozuk Para", "Çaptan değer, toplam tutar, yazı-tura", "Ctrl+5", 4),
            ("", "Uydu Alanı", "Ölçek çizgisiyle alan (km²) ve çevre (km)", "Ctrl+6", 5),
            ("", "Eşikleme", "16 eşik adayını puanla, en iyisini bul", "Ctrl+7", 6),
        ];
        foreach (var m in modules)
            left.Controls.Add(new ModuleRow(m.Icon, m.Title, m.Text, m.Key, () => openModule(m.Index)));

        // --- Sağ: akış + sözlük ---
        var right = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        right.Controls.Add(Heading("Her modülde aynı akış"));
        string[] flow =
        [
            "Aç — araç çubuğundaki \"Aç\" ile kendi fotoğrafını ya da \"Örnek\" ile hazır görüntüyü yükle.",
            "Ayarla — gerekirse şablon, tur, ölçek gibi değerleri değiştir.",
            "Çalıştır — turkuaz renkli ana düğmeye bas.",
            "Yorumla — solda girdi, sağda sonuç; altta sayılar ve açıklama, en altta durum çubuğu.",
        ];
        for (int i = 0; i < flow.Length; i++)
            right.Controls.Add(Line($"{i + 1}.  {flow[i]}", Theme.Ink, 10f, 4));
        right.Controls.Add(Spacer(14));
        right.Controls.Add(Heading("Kısa sözlük"));
        (string Term, string Meaning)[] glossary =
        [
            ("Piksel", "Görüntünün en küçük noktası; renklide R, G, B değerleri 0-255."),
            ("Eşikleme", "Eşikten parlak pikseller beyaz, koyular siyah olur. Otsu eşiği kendi seçer."),
            ("Maske", "Siyah-beyaz görüntü: beyaz = ilgilendiğimiz bölge."),
            ("Aşındırma / genişletme", "Şablonla beyaz bölgeleri inceltme / kalınlaştırma."),
            ("Açma / kapama", "Aşındır+genişlet kırıntı siler; genişlet+aşındır boşluk kapatır."),
            ("Şablon", "Bakılan komşular: 9'lu kare (3×3) ya da 5'li artı."),
            ("Ağırlık merkezi", "Bölgedeki piksellerin x ve y ortalaması."),
            ("PSNR", "İki görüntünün benzerliği (dB); yüksek olan daha iyi."),
        ];
        var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 0) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        foreach ((string term, string meaning) in glossary)
        {
            table.Controls.Add(Line(term, Theme.Accent, 9.5f, 6, FontStyle.Bold));
            table.Controls.Add(Line(meaning, Theme.Ink, 9.5f, 6));
        }
        right.Controls.Add(table);
        right.Controls.Add(Spacer(14));
        right.Controls.Add(Heading("Kısayollar"));
        right.Controls.Add(Line("Ctrl+1…7  modül değiştir     F1  rehberi aç/kapat     Alt  menü", Theme.Muted, 9.5f, 0));

        columns.Controls.Add(left, 0, 0);
        columns.Controls.Add(right, 1, 0);

        Controls.Add(columns);
        Controls.Add(subtitle);
        Controls.Add(title);
    }

    private static Label Heading(string text) => new()
    {
        Text = text.ToUpper(new System.Globalization.CultureInfo("tr-TR")),
        AutoSize = true,
        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
        ForeColor = Theme.Muted,
        Margin = new Padding(0, 0, 0, 10)
    };

    private static Label Line(string text, Color color, float size, int bottom, FontStyle style = FontStyle.Regular) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new System.Drawing.Size(720, 0),
        Font = new Font("Segoe UI", size, style),
        ForeColor = color,
        Margin = new Padding(0, 0, 8, bottom)
    };

    private static Control Spacer(int h) => new Panel { Height = h, Width = 10, Margin = new Padding(0) };

    /// <summary>Tıklanabilir modül satırı: ikon, başlık, açıklama, kısayol.</summary>
    private sealed class ModuleRow : Control
    {
        private readonly string _icon, _title, _text, _key;
        private bool _hover;
        private static readonly Font IconF = Theme.IconFont(18);
        private static readonly Font TitleF = new("Segoe UI", 10.5f, FontStyle.Bold);
        private static readonly Font TextF = new("Segoe UI", 9f);

        public ModuleRow(string icon, string title, string text, string key, Action click)
        {
            (_icon, _title, _text, _key) = (icon, title, text, key);
            Size = new System.Drawing.Size(420, 62);
            Margin = new Padding(0, 0, 0, 6);
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            Click += (_, _) => click();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(_hover ? Theme.Hover : Theme.Surface)) g.FillRectangle(b, ClientRectangle);
            if (_hover) using (var a = new SolidBrush(Theme.Accent)) g.FillRectangle(a, 0, 0, 3, Height);
            TextRenderer.DrawText(g, _icon, IconF, new Rectangle(10, 0, 44, Height), Theme.Accent,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, _title, TitleF, new System.Drawing.Point(62, 11), Theme.Ink);
            TextRenderer.DrawText(g, _text, TextF, new System.Drawing.Point(63, 34), Theme.Muted);
            TextRenderer.DrawText(g, _key, TextF, new Rectangle(0, 0, Width - 12, Height), Theme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }
    }
}
