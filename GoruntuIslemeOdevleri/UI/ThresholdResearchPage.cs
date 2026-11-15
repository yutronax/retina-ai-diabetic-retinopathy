using GoruntuIslemeOdevleri.Algorithms;
using GoruntuIslemeOdevleri.Core;
using OpenCvSharp;

namespace GoruntuIslemeOdevleri.UI;

internal sealed class ThresholdResearchPage : PageBase
{
    private Mat _source = new();

    public ThresholdResearchPage() : base("Araştırılacak görüntü", "En iyi eşikleme adayları")
    {
        AddButton("Görüntü Aç", (_, _) => LoadImage());
        AddButton("Göl Örneği", (_, _) => ReplaceSource(SyntheticSamples.Lake()));
        AddButton("Ceviz-Fındık Örneği", (_, _) => ReplaceSource(SyntheticSamples.Nuts()));
        AddButton("Otomatik Eşiği Araştır", (_, _) => Evaluate(), true);
        ReplaceSource(SyntheticSamples.Lake());
    }

    private void LoadImage()
    {
        string path = PickImage(this, "Eşikleme araştırması için görüntü seç");
        if (string.IsNullOrWhiteSpace(path)) return;
        ReplaceSource(ImageHelpers.LoadColor(path));
    }

    private void ReplaceSource(Mat image)
    {
        _source.Dispose();
        _source = image;
        InputView.SetMat(_source);
        Results.Text = "Görüntü hazır. Gri, RGB ve HSV kanalları Otsu yöntemiyle iki kutupta sınanacak.";
    }

    private void Evaluate()
    {
        IReadOnlyList<ThresholdCandidate>? candidates = null;
        try
        {
            candidates = AutomaticThresholding.Evaluate(_source);
            var panels = candidates.Take(6)
                .Select(x => ($"{x.Name}{(x.Inverted ? " ters" : "")} | T={x.Threshold:0} | S={x.Score:0.00}", x.Mask))
                .ToList();
            using Mat montage = ImageHelpers.MakeMontage(panels, 2);
            OutputView.SetMat(montage);

            var lines = new List<string>
            {
                "KANAL/KUTUP             EŞİK     PUAN   ALAN%  MERKEZ UZAK.",
                new string('-', 62)
            };
            lines.AddRange(candidates.Select(x =>
                $"{(x.Name + (x.Inverted ? " (ters)" : "")),-22} {x.Threshold,6:0.0}  {x.Score,7:0.000}  {x.AreaRatio * 100,6:0.00}  {x.CenterDistance,8:0.000}"));
            lines.Add("\r\nSeçim ölçütü: merkeze yakınlık + kullanılabilir alan oranı + kompaktlık - kenara temas cezası.");
            Results.Text = string.Join("\r\n", lines);
        }
        catch (Exception ex) { ShowError(ex); }
        finally
        {
            if (candidates is not null)
                foreach (ThresholdCandidate candidate in candidates) candidate.Mask.Dispose();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _source.Dispose();
        base.Dispose(disposing);
    }
}
