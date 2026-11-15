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

    private void Compose()
    {
        try
        {
            (Mat composite, Mat mask) = ForegroundComposer.Compose(_foreground, _background, (int)_iterations.Value);
            using (composite)
            using (mask)
            using (Mat cutout = CutoutOnChecker(_foreground, mask))
            using (Mat montage = ImageHelpers.MakeMontage(
                       [("Kesilen ön plan", cutout), ("Ön plan maskesi", mask), ("Sonuç", composite)], 3))
                OutputView.SetMat(montage);
            Results.Text = "GrabCut ile ön plan ayrıldı, maske açma-kapama işlemleriyle temizlendi ve yeni arka plana aktarıldı.\r\n" +
                           "Gerçek fotoğrafta nesne görüntü kenarına değerse sonuç zayıflar; daha sonra etkileşimli maske eklenebilir.";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    /// <summary>Maskeyle kesilen ön planı saydamlığı gösteren damalı zemin üzerinde verir.</summary>
    private static Mat CutoutOnChecker(Mat foreground, Mat mask)
    {
        using Mat bgr = ImageHelpers.EnsureBgr(foreground);
        var result = new Mat(bgr.Size(), MatType.CV_8UC3);
        int cell = Math.Max(8, Math.Min(bgr.Width, bgr.Height) / 24);
        for (int y = 0; y < bgr.Height; y += cell)
            for (int x = 0; x < bgr.Width; x += cell)
            {
                bool light = ((x / cell) + (y / cell)) % 2 == 0;
                var rect = new OpenCvSharp.Rect(x, y, Math.Min(cell, bgr.Width - x), Math.Min(cell, bgr.Height - y));
                result[rect].SetTo(light ? new Scalar(235, 235, 235) : new Scalar(200, 200, 200));
            }
        bgr.CopyTo(result, mask);
        return result;
    }

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
