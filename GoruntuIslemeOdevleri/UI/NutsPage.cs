using GoruntuIslemeOdevleri.Algorithms;
using GoruntuIslemeOdevleri.Core;
using OpenCvSharp;

namespace GoruntuIslemeOdevleri.UI;

internal sealed class NutsPage : PageBase
{
    private Mat _source = new();

    public NutsPage() : base("Girdi: ceviz ve fındık", "Tespit sonucu")
    {
        AddButton("Fotoğraf Aç", LoadImage);
        AddButton("Örnek Üret", (_, _) => LoadSample(1));
        AddButton("Analiz Et", (_, _) => Analyze(), true);
        AddButton("5 Örneği Doğrula", (_, _) => ValidateFive());
        LoadSample(1);
    }

    private void LoadImage(object? sender, EventArgs e)
    {
        string path = PickImage(this, "Ceviz-fındık fotoğrafı seç");
        if (string.IsNullOrWhiteSpace(path)) return;
        ReplaceSource(ImageHelpers.LoadColor(path));
        Results.Text = "Fotoğraf yüklendi. Analiz Et düğmesine basın.";
    }

    private void LoadSample(int seed)
    {
        ReplaceSource(SyntheticSamples.Nuts(seed));
        Results.Text = "Örnek görüntü hazır. Beklenen: 7 fındık, 4 ceviz.";
    }

    private void ReplaceSource(Mat image)
    {
        _source.Dispose();
        _source = image;
        InputView.SetMat(_source);
    }

    private void Analyze()
    {
        try
        {
            using NutAnalysis analysis = NutCounter.Analyze(_source);
            OutputView.SetMat(analysis.Annotated);
            Results.Text = $"Fındık sayısı : {analysis.HazelnutCount}\r\n" +
                           $"Ceviz sayısı  : {analysis.WalnutCount}\r\n" +
                           $"Boyut ayırma eşiği: {analysis.SplitDiameter:0.0} piksel\r\n\r\n" +
                           string.Join("\r\n", analysis.Items.Select((x, i) =>
                               $"{i + 1,2}. {x.Kind,-7} alan={x.Area,8:0} px²  çap={x.DiameterPixels,6:0.0} px"));
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ValidateFive()
    {
        try
        {
            var panels = new List<(string, Mat)>();
            var lines = new List<string> { "5 ÖRNEKLİK DOĞRULAMA", "No | Beklenen F/C | Bulunan F/C | Durum" };
            for (int seed = 1; seed <= 5; seed++)
            {
                int expectedH = 5 + seed;
                int expectedW = 2 + seed % 3;
                using Mat sample = SyntheticSamples.Nuts(seed, expectedH, expectedW);
                using NutAnalysis analysis = NutCounter.Analyze(sample);
                panels.Add(($"Örnek {seed}: F={analysis.HazelnutCount}, C={analysis.WalnutCount}", analysis.Annotated.Clone()));
                bool passed = analysis.HazelnutCount == expectedH && analysis.WalnutCount == expectedW;
                lines.Add($"{seed,2} | {expectedH,3}/{expectedW,-3}     | {analysis.HazelnutCount,3}/{analysis.WalnutCount,-3}     | {(passed ? "BAŞARILI" : "KONTROL")}");
            }

            using Mat montage = ImageHelpers.MakeMontage(panels, 2);
            OutputView.SetMat(montage);
            Results.Text = string.Join("\r\n", lines);
            foreach (var panel in panels) panel.Item2.Dispose();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _source.Dispose();
        base.Dispose(disposing);
    }
}
