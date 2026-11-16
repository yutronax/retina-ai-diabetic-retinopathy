using GoruntuIslemeOdevleri.Algorithms;
using GoruntuIslemeOdevleri.Core;
using OpenCvSharp;

namespace GoruntuIslemeOdevleri.UI;

/// <summary>
/// Ödev 3. Sayfa iki görüntü tutar:
///  _clean : açılan temiz fotoğraf (PSNR ölçümü için referans),
///  _work  : üzerinde çalışılan görüntü (gürültü eklenmiş ya da önceki işlemin sonucu). Soldaki görüntü budur.
/// "Seçili İşlemi Uygula" her zaman _work'e uygulanır; "Sonucu Girdi Yap" ile işlemler zincirlenebilir
/// (ör. önce aşındır, sonra genişlet).
/// </summary>
internal sealed class NoisePage : PageBase
{
    private Mat _clean = new();
    private Mat _work = new();
    private Mat? _lastResult;
    private bool _workIsNoisy;
    private readonly NumericUpDown _density;
    private readonly ComboBox _kernel;
    private readonly ComboBox _operation;
    private readonly NumericUpDown _iterations;
    private readonly NumericUpDown[] _ranges;

    public NoisePage() : base("Çalışılan görüntü", "Sonuç")
    {
        AddButton("Fotoğraf Aç", (_, _) => LoadImage());
        AddSamples("3-tuzbiber", OpenSample, ("Sentetik örnek", LoadSample));
        AddLabel("Gürültü %");
        _density = AddNumber(1, 40, 6, 52);
        AddButton("Gürültü Ekle", (_, _) => AddNoise());
        AddButton("Gürültü Karşılaştır", (_, _) => CompareNoise(), true);
        AddButton("Kırık Kenar Deneyi", (_, _) => BrokenEdgeExperiment());
        AddLabel("Şablon");
        _kernel = AddCombo("9'lu kare (3x3)", "5'li artı");
        AddLabel("İşlem");
        _operation = AddCombo("Aşındır", "Genişlet", "Açma", "Kapama", "Medyan");
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
        AddButton("Sonucu Girdi Yap", (_, _) => UseResultAsInput());
        AddButton("Sıfırla", (_, _) => ResetToClean());
        SetGuide(Guides.Noise);
        // Bu sayfada çok sayıda ayar var; tek satıra sığmayanlar taşma okunun içine gizlenmesin, alt satıra geçsin.
        Toolbar.LayoutStyle = ToolStripLayoutStyle.Flow;
        if (FirstSample("3-tuzbiber", "Biber") is string first) OpenSample(first); else LoadSample();
    }

    private void OpenSample(string path)
    {
        SetClean(ImageHelpers.LoadColor(path));
        Results.Text = $"Temiz görüntü: {Path.GetFileNameWithoutExtension(path)}.\r\n" +
                       "\"Gürültü Ekle\" ile tuz-biber gürültüsü ekleyip menüden şablon ve işlem seçerek temizlemeyi dene,\r\n" +
                       "ya da \"Gürültü Karşılaştır\" ile üç yöntemi tek seferde kıyasla.";
    }

    private void LoadSample()
    {
        SetClean(SyntheticSamples.NoiseBase());
        Results.Text = "Temiz örnek yüklendi. \"Gürültü Ekle\" ya da \"Gürültü Karşılaştır\"a bas.";
    }

    private void LoadImage()
    {
        string path = PickImage(this, "Gürültü deneyi için görüntü seç");
        if (string.IsNullOrWhiteSpace(path)) return;
        SetClean(ImageHelpers.LoadColor(path));
        Results.Text = "Görüntü yüklendi. Zaten tuz-biberliyse doğrudan \"Seçili İşlemi Uygula\"ya basabilirsin; " +
                       "temizse önce \"Gürültü Ekle\".";
    }

    private void SetClean(Mat image)
    {
        _clean.Dispose();
        _clean = image;
        SetWork(image.Clone(), noisy: false);
        ClearResult();
    }

    private void SetWork(Mat image, bool noisy)
    {
        _work.Dispose();
        _work = image;
        _workIsNoisy = noisy;
        InputView.SetMat(_work);
    }

    private void ClearResult()
    {
        _lastResult?.Dispose();
        _lastResult = null;
    }

    private void AddNoise()
    {
        try
        {
            double density = (double)_density.Value / 100.0;
            SetWork(NoiseAndMorphology.AddSaltPepper(_clean, density), noisy: true);
            ClearResult();
            Results.Text = $"Temiz görüntüye %{_density.Value} yoğunlukta tuz-biber gürültüsü eklendi " +
                           $"(PSNR {NoiseAndMorphology.ComputePsnr(_clean, _work):0.00} dB).\r\n" +
                           "Şimdi şablon ve işlem seçip \"Seçili İşlemi Uygula\"ya bas. Örnek: önce Aşındır, " +
                           "\"Sonucu Girdi Yap\", sonra Genişlet.";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ResetToClean()
    {
        SetWork(_clean.Clone(), noisy: false);
        ClearResult();
        OutputView.SetMat(_clean);
        Results.Text = "Çalışılan görüntü temiz hâline döndürüldü.";
    }

    private void UseResultAsInput()
    {
        if (_lastResult is null)
        {
            Results.Text = "Önce \"Seçili İşlemi Uygula\" ile bir sonuç üret.";
            return;
        }
        SetWork(_lastResult, _workIsNoisy);
        _lastResult = null;
        Results.Text = "Sonuç yeni girdi oldu. Sıradaki işlemi seçip uygula (ör. aşındırmadan sonra genişlet).";
    }

    private void CompareNoise()
    {
        try
        {
            double density = (double)_density.Value / 100.0;
            using NoiseComparison comparison = NoiseAndMorphology.Compare(_clean, density);
            SetWork(comparison.Noisy.Clone(), noisy: true);
            ClearResult();
            using Mat montage = ImageHelpers.MakeMontage(
            [
                ($"Tuz-biber gürültüsü (%{_density.Value})", comparison.Noisy),
                ($"9'lu kare açma+kapama - {comparison.SquarePsnr:0.00} dB", comparison.SquareResult),
                ($"5'li artı açma+kapama - {comparison.CrossPsnr:0.00} dB", comparison.CrossResult),
                ($"Medyan 3x3 - {comparison.MedianPsnr:0.00} dB", comparison.MedianResult)
            ], 2);
            OutputView.SetMat(montage);
            var ranking = new[]
            {
                ("9'lu kare", comparison.SquarePsnr),
                ("5'li artı", comparison.CrossPsnr),
                ("Medyan", comparison.MedianPsnr)
            }.OrderByDescending(x => x.Item2).ToArray();
            double noisyPsnr = NoiseAndMorphology.ComputePsnr(_clean, comparison.Noisy);
            Results.Text = $"Gürültülü görüntü PSNR : {noisyPsnr:0.00} dB\r\n" +
                           $"9'lu kare (açma+kapama): {comparison.SquarePsnr:0.00} dB\r\n" +
                           $"5'li artı (açma+kapama): {comparison.CrossPsnr:0.00} dB\r\n" +
                           $"Medyan (3x3)           : {comparison.MedianPsnr:0.00} dB\r\n\r\n" +
                           $"Sıralama: {string.Join(" > ", ranking.Select(x => x.Item1))}\r\n" +
                           $"Şablonlar arasında daha iyi: {(comparison.SquarePsnr >= comparison.CrossPsnr ? "9'lu kare" : "5'li artı")}.\r\n" +
                           "Açma (aşındır→genişlet) beyaz noktaları (tuz), kapama (genişlet→aşındır) siyah noktaları (biber) siler. " +
                           "Medyan ise komşuları sıralayıp ortadakini aldığı için uç değerleri doğrudan atar ve ayrıntıyı daha az bozar.";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    /// <summary>
    /// Kırık kenarları iki şablonla kapama. İki görüntüde denenir: kasıtlı kopukluklar içeren sentetik
    /// çizim ve açık olan fotoğrafın Canny kenarları. Başarı, kalan çizgi ucu ve parça sayısıyla ölçülür.
    /// </summary>
    private void BrokenEdgeExperiment()
    {
        try
        {
            int iterations = (int)_iterations.Value;
            using Mat synthetic = SyntheticSamples.BrokenEdges();
            using Mat edges = NoiseAndMorphology.EdgesOf(_clean);
            var panels = new List<(string, Mat)>();
            var lines = new List<string> { $"KENAR AÇIKLIKLARINI KAPAMA (kapama = genişlet→aşındır, {iterations} tur)",
                "Uç: kopuk çizgi ucu sayısı, Parça: ayrı çizgi sayısı (ikisi de azsa daha iyi kapanmış)", "" };

            foreach (var (name, image) in new[] { ("Sentetik çizim", synthetic), ("Fotoğrafın kenarları", edges) })
            {
                using Mat square = NoiseAndMorphology.CloseBrokenEdges(image, KernelChoice.Square9, iterations);
                using Mat cross = NoiseAndMorphology.CloseBrokenEdges(image, KernelChoice.Cross5, iterations);
                int e0 = NoiseAndMorphology.CountEndpoints(image), s0 = NoiseAndMorphology.CountSegments(image);
                int e1 = NoiseAndMorphology.CountEndpoints(square), s1 = NoiseAndMorphology.CountSegments(square);
                int e2 = NoiseAndMorphology.CountEndpoints(cross), s2 = NoiseAndMorphology.CountSegments(cross);
                panels.Add(($"{name}: {s0} parça", image.Clone()));
                panels.Add(($"9'lu kare: {s1} parça", square.Clone()));
                panels.Add(($"5'li artı: {s2} parça", cross.Clone()));

                if (ReferenceEquals(image, synthetic))
                {
                    // Her açıklık kendi hücresinde: hücrede tek parça kaldıysa açıklık kapanmıştır.
                    lines.Add("Sentetik çizim (sütunlar soldan sağa 2, 4, 6, 8 px açıklık):");
                    lines.Add("  Yön     Açıklık  9'lu kare  5'li artı");
                    int closedSquare = 0, closedCross = 0;
                    foreach (var (direction, gap, cell) in SyntheticSamples.BrokenEdgeCells())
                    {
                        bool sq = IsClosed(square, cell), cr = IsClosed(cross, cell);
                        closedSquare += sq ? 1 : 0;
                        closedCross += cr ? 1 : 0;
                        lines.Add($"  {direction,-7} {gap,3} px   {(sq ? "kapandı" : "açık"),-9}  {(cr ? "kapandı" : "açık")}");
                    }
                    string winner = closedSquare == closedCross ? "eşit" : closedSquare > closedCross ? "9'lu kare" : "5'li artı";
                    lines.Add($"  Kapanan açıklık: kare {closedSquare}/12, artı {closedCross}/12 → daha iyi kapatan: {winner}");
                }
                else
                {
                    string better = s1 < s2 || (s1 == s2 && e1 <= e2) ? "9'lu kare" : "5'li artı";
                    lines.Add($"{name}: önce {s0} parça / {e0} uç; kare {s1} / {e1}; artı {s2} / {e2} → daha iyi kapatan: {better}");
                }
                lines.Add("");
            }

            using Mat montage = ImageHelpers.MakeMontage(panels, 3);
            OutputView.SetMat(montage);
            foreach (var panel in panels) panel.Item2.Dispose();
            lines.Add("Kare şablon 8 komşunun hepsine genişlediği için açıklığın üstündeki köprüyü tam doldurur; ardından gelen " +
                      "aşındırma köprüyü silemez. Artı şablonun köşeleri boş olduğundan köprü ince kalır ve aşındırma onu yeniden açar. " +
                      "Bu yüzden aynı tur sayısında kare daha geniş açıklıkları kapatır; bedeli çizgileri kalınlaştırması ve köşeleri yuvarlamasıdır.");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private static bool IsClosed(Mat image, OpenCvSharp.Rect cell)
    {
        using Mat roi = new(image, cell);
        return NoiseAndMorphology.CountSegments(roi.Clone()) == 1;
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
                3 => MorphologyChoice.Close,
                _ => MorphologyChoice.Median
            };
            int[] r = _ranges.Select(x => (int)x.Value).ToArray();
            if (r[0] > r[1] || r[2] > r[3] || r[4] > r[5])
            {
                Results.Text = "Renk aralığında en küçük değer en büyükten büyük olamaz.";
                return;
            }

            var (result, maskBefore, maskAfter) = NoiseAndMorphology.ApplyDetailed(_work, kernel, operation,
                (int)_iterations.Value, r[0], r[1], r[2], r[3], r[4], r[5]);
            ClearResult();
            _lastResult = result;

            string info = $"Uygulanan işlem: {_operation.Text}, şablon: {_kernel.Text}, tur: {_iterations.Value}\r\n";
            if (maskBefore is not null && maskAfter is not null)
            {
                int before = Cv2.CountNonZero(maskBefore), after = Cv2.CountNonZero(maskAfter);
                using Mat montage = ImageHelpers.MakeMontage(
                [
                    ("Aralıktaki pikseller (beyaz)", maskBefore),
                    ($"İşlem sonrası maske", maskAfter),
                    ("Sonuç", result)
                ], 3);
                OutputView.SetMat(montage);
                info += $"R=[{r[0]},{r[1]}], G=[{r[2]},{r[3]}], B=[{r[4]},{r[5]}] aralığındaki pikseller komşu sayıldı.\r\n" +
                        $"Bu aralıkta {before} piksel vardı, işlemden sonra {after} piksel ({after - before:+0;-0;0}).\r\n";
                maskBefore.Dispose();
                maskAfter.Dispose();
            }
            else
            {
                OutputView.SetMat(result);
                info += "Renk aralığı tam (0-255): işlem her kanala doğrudan uygulandı.\r\n";
            }

            if (_workIsNoisy)
                info += $"PSNR (temiz görüntüye göre): girdi {NoiseAndMorphology.ComputePsnr(_clean, _work):0.00} dB → " +
                        $"sonuç {NoiseAndMorphology.ComputePsnr(_clean, result):0.00} dB\r\n";
            info += "\"Sonucu Girdi Yap\" ile bu sonucun üzerine başka bir işlem uygulayabilirsin.";
            Results.Text = info;
        }
        catch (Exception ex) { ShowError(ex); }
    }

    /// <summary>Sunum için: menüdeki seçimleri koddan ayarlayıp "Seçili İşlemi Uygula" ile aynı yolu çalıştırır.</summary>
    internal void Demo(Mat source, int kernelIndex, int operationIndex, int iterations, int[] ranges)
    {
        SetClean(source.Clone());
        _kernel.SelectedIndex = kernelIndex;
        _operation.SelectedIndex = operationIndex;
        _iterations.Value = iterations;
        for (int i = 0; i < _ranges.Length; i++) _ranges[i].Value = ranges[i];
        ApplySelected();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clean.Dispose();
            _work.Dispose();
            _lastResult?.Dispose();
        }
        base.Dispose(disposing);
    }
}
