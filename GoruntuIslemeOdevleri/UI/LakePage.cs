using GoruntuIslemeOdevleri.Algorithms;
using GoruntuIslemeOdevleri.Core;
using OpenCvSharp;

namespace GoruntuIslemeOdevleri.UI;

internal sealed class LakePage : PageBase
{
    private Mat _source = new();
    private readonly NumericUpDown _realMeters;
    private readonly NumericUpDown _pixelLength;
    private readonly List<Point> _scalePoints = [];
    private bool _selectingScale;

    public LakePage() : base("Uydu görüntüsü (ölçek için iki noktaya tıkla)", "Alan ve çevre sonucu")
    {
        AddButton("Uydu Görüntüsü Aç", (_, _) => LoadImage());
        AddSamples("5-uydu", OpenSample, ("Sentetik örnek (150 piksel = 50 km)", LoadSample));
        AddLabel("Gerçek uzunluk (m)");
        _realMeters = AddNumber(1, 10_000_000, 50_000, 105);
        AddLabel("Piksel uzunluğu");
        _pixelLength = AddNumber(1, 100_000, 150, 85, 1);
        AddButton("Ölçeği 2 Noktayla Seç", (_, _) => StartScaleSelection());
        AddButton("Alan ve Çevreyi Hesapla", (_, _) => Analyze(), true);
        SetGuide(Guides.Lake);
        InputView.ImagePixelClicked += InputViewOnImagePixelClicked;
        if (FirstSample("5-uydu", "Van") is string van) OpenSample(van); else LoadSample();
    }

    /// <summary>
    /// Gerçek örnek. Van Gölü için ölçek hazır gelir: Nemrut kalderası (238,412) – Süphan Dağı (462,270)
    /// arası koordinatlardan ≈ 60 km, görüntüde ≈ 265 piksel.
    /// </summary>
    private void OpenSample(string path)
    {
        ReplaceSource(ImageHelpers.LoadColor(path));
        if (Path.GetFileName(path).Contains("Van", StringComparison.OrdinalIgnoreCase))
        {
            _realMeters.Value = 60_000;
            _pixelLength.Value = (decimal)Math.Round(Math.Sqrt(224 * 224 + 142 * 142), 1);
            Results.Text = "Örnek: Van Gölü (Landsat). Ölçek hazır: Nemrut kalderası – Süphan Dağı ≈ 60 km ≈ 265 piksel.\r\n" +
                           "\"Alan ve Çevreyi Hesapla\"ya basın. Resmî alan ≈ 3.755 km²; sonucunuzu karşılaştırın.";
        }
        else
            Results.Text = "Örnek yüklendi. Ölçeği iki noktayla seçin ve gerçek uzunluğu metre olarak yazın.";
    }

    private void LoadSample()
    {
        ReplaceSource(SyntheticSamples.Lake());
        _realMeters.Value = 50_000;
        _pixelLength.Value = 150;
        Results.Text = "Örnek görüntüde sarı ölçek çizgisi 150 piksel = 50 km.";
    }

    private void LoadImage()
    {
        string path = PickImage(this, "Uydu/göl görüntüsünü seç");
        if (string.IsNullOrWhiteSpace(path)) return;
        ReplaceSource(ImageHelpers.LoadColor(path));
        Results.Text = "Görüntü yüklendi. Gerçek ölçek uzunluğunu yazın ve görüntüde iki uç noktayı seçin.";
    }

    private void ReplaceSource(Mat image)
    {
        _source.Dispose();
        _source = image;
        _scalePoints.Clear();
        _selectingScale = false;
        InputView.SetMat(_source);
    }

    private void StartScaleSelection()
    {
        _scalePoints.Clear();
        _selectingScale = true;
        Results.Text = "Ölçek çizgisinin birinci ve ikinci uç noktasına tıklayın.";
    }

    private void InputViewOnImagePixelClicked(object? sender, Point point)
    {
        if (!_selectingScale) return;
        _scalePoints.Add(point);

        using Mat preview = _source.Clone();
        foreach (Point p in _scalePoints)
            Cv2.Circle(preview, p, 8, new Scalar(0, 0, 255), -1, LineTypes.AntiAlias);

        if (_scalePoints.Count == 2)
        {
            Point a = _scalePoints[0];
            Point b = _scalePoints[1];
            Cv2.Line(preview, a, b, new Scalar(0, 0, 255), 4, LineTypes.AntiAlias);
            double distance = Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
            _pixelLength.Value = Math.Clamp((decimal)distance, _pixelLength.Minimum, _pixelLength.Maximum);
            _selectingScale = false;
            Results.Text = $"Ölçek çizgisi: {distance:0.0} piksel. Şimdi hesaplama yapılabilir.";
        }
        InputView.SetMat(preview);
    }

    private void Analyze()
    {
        try
        {
            using LakeAnalysis analysis = LakeAnalyzer.Analyze(_source, (double)_realMeters.Value, (double)_pixelLength.Value);
            using Mat montage = ImageHelpers.MakeMontage([("Seçilen maske", analysis.Mask), ("Ölçüm", analysis.Annotated)], 1);
            OutputView.SetMat(montage);
            double metersPerPixel = (double)_realMeters.Value / (double)_pixelLength.Value;
            Results.Text = $"Otomatik eşikleme : {analysis.ThresholdMethod}, eşik={analysis.Threshold:0.0}\r\n" +
                           $"Ölçek             : {metersPerPixel:0.000} m/piksel\r\n" +
                           $"Alan              : {analysis.AreaSquareMeters:0} m² = {analysis.AreaSquareMeters / 1_000_000.0:0.000} km²\r\n" +
                           $"Çevre             : {analysis.PerimeterMeters:0} m = {analysis.PerimeterMeters / 1000.0:0.000} km\r\n" +
                           $"Ağırlık merkezi   : ({analysis.Centroid.X:0.0}, {analysis.Centroid.Y:0.0})";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _source.Dispose();
        base.Dispose(disposing);
    }
}
