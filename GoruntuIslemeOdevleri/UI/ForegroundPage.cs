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
        AddButton("Örnekleri Yükle", (_, _) => LoadSample());
        AddLabel("GrabCut turu");
        _iterations = AddNumber(1, 10, 5, 60);
        AddButton("Arka Planı Değiştir", (_, _) => Compose(), true);
        LoadSample();
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
            using (Mat montage = ImageHelpers.MakeMontage([("Ön plan maskesi", mask), ("Sonuç", composite)], 1))
                OutputView.SetMat(montage);
            Results.Text = "GrabCut ile ön plan ayrıldı, maske açma-kapama işlemleriyle temizlendi ve yeni arka plana aktarıldı.\r\n" +
                           "Gerçek fotoğrafta nesne görüntü kenarına değerse sonuç zayıflar; daha sonra etkileşimli maske eklenebilir.";
        }
        catch (Exception ex) { ShowError(ex); }
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
