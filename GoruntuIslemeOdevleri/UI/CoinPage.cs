using GoruntuIslemeOdevleri.Algorithms;
using GoruntuIslemeOdevleri.Core;
using OpenCvSharp;

namespace GoruntuIslemeOdevleri.UI;

internal sealed class CoinPage : PageBase
{
    private Mat _source = new();
    private Mat _headsReference = new();
    private Mat _tailsReference = new();

    public CoinPage() : base("Bozuk para görüntüsü", "Değer ve yazı-tura sonucu")
    {
        AddButton("Fotoğraf Aç", (_, _) => LoadSource());
        AddButton("Yazı Referansı", (_, _) => LoadReference(true));
        AddButton("Tura Referansı", (_, _) => LoadReference(false));
        AddButton("Örnekleri Yükle", (_, _) => LoadSample());
        AddButton("Paraları Analiz Et", (_, _) => Analyze(), true);
        LoadSample();
    }

    private void LoadSample()
    {
        _source.Dispose();
        _headsReference.Dispose();
        _tailsReference.Dispose();
        _source = SyntheticSamples.Coins();
        _headsReference = SyntheticSamples.CoinReference(true);
        _tailsReference = SyntheticSamples.CoinReference(false);
        InputView.SetMat(_source);
        Results.Text = "Örnek paralar ve yazı/tura referansları yüklendi.";
    }

    private void LoadSource()
    {
        string path = PickImage(this, "Bozuk para fotoğrafını seç");
        if (string.IsNullOrWhiteSpace(path)) return;
        _source.Dispose();
        _source = ImageHelpers.LoadColor(path);
        InputView.SetMat(_source);
    }

    private void LoadReference(bool heads)
    {
        string path = PickImage(this, heads ? "Tek bir yazı yüzü referansı seç" : "Tek bir tura yüzü referansı seç");
        if (string.IsNullOrWhiteSpace(path)) return;
        Mat loaded = ImageHelpers.LoadColor(path);
        if (heads)
        {
            _headsReference.Dispose();
            _headsReference = loaded;
        }
        else
        {
            _tailsReference.Dispose();
            _tailsReference = loaded;
        }
        Results.Text = (heads ? "Yazı" : "Tura") + " referansı yüklendi.";
    }

    private void Analyze()
    {
        try
        {
            Mat? heads = _headsReference.Empty() ? null : _headsReference;
            Mat? tails = _tailsReference.Empty() ? null : _tailsReference;
            using CoinAnalysis analysis = CoinAnalyzer.Analyze(_source, heads, tails);
            OutputView.SetMat(analysis.Annotated);
            Results.Text = $"Para adedi : {analysis.Items.Count}\r\n" +
                           $"Toplam     : {analysis.TotalLira:0.00} TL\r\n" +
                           $"Yazı/Tura  : {analysis.HeadsCount}/{analysis.TailsCount}\r\n\r\n" +
                           string.Join("\r\n", analysis.Items.Select((x, i) =>
                               $"{i + 1,2}. {x.Value:0.00} TL  {x.Face,-8} çap={x.DiameterPixels,6:0.0}px  fark={x.FaceConfidence:0.000}"));
        }
        catch (Exception ex) { ShowError(ex); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _source.Dispose();
            _headsReference.Dispose();
            _tailsReference.Dispose();
        }
        base.Dispose(disposing);
    }
}
