using GoruntuIslemeOdevleri.Algorithms;
using GoruntuIslemeOdevleri.Core;
using OpenCvSharp;

namespace GoruntuIslemeOdevleri.UI;

internal sealed class ForegroundPage : PageBase
{
    private Mat _foreground = new();
    private Mat _background = new();
    private readonly NumericUpDown _iterations;

    public ForegroundPage() : base("Ön plan fotoğrafı", "Birleştirilmiş sonuç")
    {
        AddButton("Ön Plan Aç", (_, _) => LoadForeground());
        AddButton("Arka Plan Aç", (_, _) => LoadBackground());
        AddSamples("2-arkaplan", OpenSample, ("Sentetik örnek", LoadSample));
        AddLabel("GrabCut turu");
        _iterations = AddNumber(1, 10, 5, 60);
        AddButton("Arka Planı Değiştir", (_, _) => Compose(), true);
        AddButton("Adımları Göster", (_, _) => ShowSteps());
        SetGuide(Guides.Foreground);
        if (FirstSample("2-arkaplan", "Kişi 1") is string person && FirstSample("2-arkaplan", "gökyüzü") is string sky)
        {
            OpenSample(sky);
            OpenSample(person);
        }
        else LoadSample();
    }

    /// <summary>"Arka plan - ..." adlı örnekler arka planı, diğerleri ön planı (kişiyi) değiştirir.</summary>
    private void OpenSample(string path)
    {
        Mat loaded = ImageHelpers.LoadColor(path);
        string name = Path.GetFileNameWithoutExtension(path);
        if (name.StartsWith("Arka plan", StringComparison.CurrentCultureIgnoreCase))
        {
            _background.Dispose();
            _background = loaded;
            Results.Text = $"Yeni arka plan: {name}. \"Arka Planı Değiştir\"e basın.";
        }
        else
        {
            _foreground.Dispose();
            _foreground = loaded;
            InputView.SetMat(_foreground);
            Results.Text = $"Ön plan: {name}. Arka planı da menüden seçip \"Arka Planı Değiştir\"e basın.";
        }
    }

    private void LoadSample()
    {
        (Mat fg, Mat bg) = SyntheticSamples.ForegroundScene();
        _foreground.Dispose();
        _background.Dispose();
        _foreground = fg;
        _background = bg;
        InputView.SetMat(_foreground);
        Results.Text = "Örnek ön plan ve manzara hazır.";
    }

    private void LoadForeground()
    {
        string path = PickImage(this, "Ön plan fotoğrafını seç");
        if (string.IsNullOrWhiteSpace(path)) return;
        _foreground.Dispose();
        _foreground = ImageHelpers.LoadColor(path);
        InputView.SetMat(_foreground);
    }

    private void LoadBackground()
    {
        string path = PickImage(this, "Yeni arka planı seç");
        if (string.IsNullOrWhiteSpace(path)) return;
        _background.Dispose();
        _background = ImageHelpers.LoadColor(path);
        Results.Text = "Arka plan yüklendi.";
    }

    private ForegroundComposer.ForegroundResult Run() =>
        ForegroundComposer.Analyze(_foreground, _background, (int)_iterations.Value);

    private static void Release(ForegroundComposer.ForegroundResult r)
    {
        r.Composite.Dispose();
        r.Alpha.Dispose();
        r.Cutout.Dispose();
        foreach (var step in r.Steps) step.Image.Dispose();
    }

    private void Compose()
    {
        try
        {
            var r = Run();
            OutputView.SetMat(r.Composite);
            Results.Text = Summary(r);
            Release(r);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    /// <summary>Ara aşamaları (renk uzaklığı, duvar maskesi, GrabCut başlangıcı, alfa, kesilen kişi, sonuç) gösterir.</summary>
    private void ShowSteps()
    {
        try
        {
            var r = Run();
            using (Mat montage = ImageHelpers.MakeMontage(r.Steps, 3))
                OutputView.SetMat(montage);
            Results.Text = Summary(r);
            Release(r);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private static string Summary(ForegroundComposer.ForegroundResult r) =>
        $"Duvar rengi (BGR) : {r.WallColor.Val0:0}, {r.WallColor.Val1:0}, {r.WallColor.Val2:0}\r\n" +
        $"Renk uzaklığı eşiği: {r.ColorThreshold:0} (duvar örneklerinin %98'i × 1,5 + 4)\r\n\r\n" +
        "1. Duvar rengi üst ve yan kenarlardan öğrenilir; ışık farkı için konuma bağlı yüzey oturtulur.\r\n" +
        "2. Duvar rengine yakın ve kenara bağlı bölgeler duvar sayılır.\r\n" +
        "3. GrabCut yalnızca sınırdaki belirsiz şeridi yeniden sınıflandırır.\r\n" +
        "4. Kenarda alfa (saydamlık) I = αF + (1-α)W karışım denkleminden hesaplanır.\r\n" +
        "5. Kenardaki duvar rengi çıkarılır (hale temizliği), kişi manzaraya α ile harmanlanır.";

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _foreground.Dispose();
            _background.Dispose();
        }
        base.Dispose(disposing);
    }
}
