using GoruntuIslemeOdevleri.Algorithms;
using GoruntuIslemeOdevleri.Core;
using OpenCvSharp;

namespace GoruntuIslemeOdevleri.UI;

internal sealed class NoisePage : PageBase
{
    private Mat _source = new();
    private readonly ComboBox _kernel;
    private readonly ComboBox _operation;
    private readonly NumericUpDown _iterations;
    private readonly NumericUpDown[] _ranges;

    public NoisePage() : base("Temiz / kenar görüntüsü", "Karşılaştırma sonucu")
    {
        AddButton("Fotoğraf Aç", (_, _) => LoadImage());
        AddButton("Örnek", (_, _) => LoadSample());
        AddButton("Gürültü Karşılaştır", (_, _) => CompareNoise(), true);
        AddButton("Kırık Kenar Deneyi", (_, _) => BrokenEdgeExperiment());
        AddLabel("Şablon");
        _kernel = AddCombo("9'lu kare (3x3)", "5'li artı");
        AddLabel("İşlem");
        _operation = AddCombo("Aşındır", "Genişlet", "Açma", "Kapama");
        AddLabel("Tur");
        _iterations = AddNumber(1, 10, 1, 52);

        _ranges = new NumericUpDown[6];
        string[] labels = ["Rmin", "Rmax", "Gmin", "Gmax", "Bmin", "Bmax"];
        for (int i = 0; i < labels.Length; i++)
        {
            AddLabel(labels[i]);
            _ranges[i] = AddNumber(0, 255, i % 2 == 0 ? 0 : 255, 58);
        }
        AddButton("Seçili İşlemi Uygula", (_, _) => ApplySelected());
        LoadSample();
    }

    private void LoadSample()
    {
        ReplaceSource(SyntheticSamples.NoiseBase());
        Results.Text = "Temiz örnek yüklendi. Gürültü Karşılaştır ile tuz-biber gürültüsü otomatik eklenir.";
    }

    private void LoadImage()
    {
        string path = PickImage(this, "Gürültü deneyi için görüntü seç");
        if (string.IsNullOrWhiteSpace(path)) return;
        ReplaceSource(ImageHelpers.LoadColor(path));
    }

    private void ReplaceSource(Mat image)
    {
        _source.Dispose();
        _source = image;
        InputView.SetMat(_source);
    }

    private void CompareNoise()
    {
        try
        {
            using NoiseComparison comparison = NoiseAndMorphology.Compare(_source);
            using Mat montage = ImageHelpers.MakeMontage(
            [
                ("Tuz-biber gürültüsü", comparison.Noisy),
                ($"9'lu kare - {comparison.SquarePsnr:0.00} dB", comparison.SquareResult),
                ($"5'li artı - {comparison.CrossPsnr:0.00} dB", comparison.CrossResult),
                ($"Medyan - {comparison.MedianPsnr:0.00} dB", comparison.MedianResult)
            ], 2);
            OutputView.SetMat(montage);
            string winner = new[]
            {
                ("9'lu kare", comparison.SquarePsnr),
                ("5'li artı", comparison.CrossPsnr),
                ("Medyan", comparison.MedianPsnr)
            }.MaxBy(x => x.Item2).Item1;
            Results.Text = $"9'lu kare PSNR : {comparison.SquarePsnr:0.000} dB\r\n" +
                           $"5'li artı PSNR : {comparison.CrossPsnr:0.000} dB\r\n" +
                           $"Medyan PSNR    : {comparison.MedianPsnr:0.000} dB\r\n\r\n" +
                           $"Bu örnekte sayısal olarak en iyi sonuç: {winner}.";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void BrokenEdgeExperiment()
    {
        try
        {
            using Mat broken = SyntheticSamples.BrokenEdges();
            using Mat square = NoiseAndMorphology.CloseBrokenEdges(broken, KernelChoice.Square9, 3);
            using Mat cross = NoiseAndMorphology.CloseBrokenEdges(broken, KernelChoice.Cross5, 3);
            using Mat montage = ImageHelpers.MakeMontage(
                [("Kırık kenarlar", broken), ("9'lu kare ile kapama", square), ("5'li artı ile kapama", cross)], 2);
            InputView.SetMat(broken);
            OutputView.SetMat(montage);
            Results.Text = "Aynı kapama işlemi iki yapısal elemanla uygulandı. Kare şablon köşeleri daha hızlı birleştirir; artı şablon yönsel yapıyı daha fazla korur.";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ApplySelected()
    {
        try
        {
            KernelChoice kernel = _kernel.SelectedIndex == 1 ? KernelChoice.Cross5 : KernelChoice.Square9;
            MorphologyChoice operation = _operation.SelectedIndex switch
            {
                0 => MorphologyChoice.Erode,
                1 => MorphologyChoice.Dilate,
                2 => MorphologyChoice.Open,
                _ => MorphologyChoice.Close
            };
            using Mat result = NoiseAndMorphology.Apply(_source, kernel, operation, (int)_iterations.Value,
                (int)_ranges[0].Value, (int)_ranges[1].Value,
                (int)_ranges[2].Value, (int)_ranges[3].Value,
                (int)_ranges[4].Value, (int)_ranges[5].Value);
            OutputView.SetMat(result);
            Results.Text = $"Uygulanan işlem: {_operation.Text}\r\nŞablon: {_kernel.Text}\r\nTur: {_iterations.Value}\r\n" +
                           $"R=[{_ranges[0].Value},{_ranges[1].Value}], G=[{_ranges[2].Value},{_ranges[3].Value}], B=[{_ranges[4].Value},{_ranges[5].Value}]";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _source.Dispose();
        base.Dispose(disposing);
    }
}
