using System.Globalization;
using System.Text.Json;
using GoruntuIslemeOdevleri.Algorithms;
using GoruntuIslemeOdevleri.Core;
using OpenCvSharp;

namespace GoruntuIslemeOdevleri;

/// <summary>
/// Arayüz açmadan tüm ödev algoritmalarını gerçek fotoğraflar üzerinde çalıştırır.
/// Kullanım: GoruntuIslemeOdevleri.exe --batch &lt;veri klasörü&gt; &lt;çıktı klasörü&gt;
/// </summary>
internal static class BatchRunner
{
    public static int Run(string dataDir, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var results = new Dictionary<string, object>();
        var log = new List<string>();

        void Try(string name, Func<object> action)
        {
            try { results[name] = action(); }
            catch (Exception ex) { log.Add($"{name}: {ex.Message}"); results[name] = new { error = ex.Message }; }
        }

        Try("nuts", () => RunNuts(Path.Combine(dataDir, "nuts"), outDir));
        Try("foreground", () => RunForeground(Path.Combine(dataDir, "person"), outDir));
        Try("noise", () => RunNoise(Path.Combine(dataDir, "sipi", "misc"), outDir));
        Try("coins", () => RunCoins(Path.Combine(dataDir, "coins"), outDir));
        Try("lake", () => RunLake(dataDir, outDir));
        Try("threshold", () => RunThreshold(dataDir, outDir));
        Try("noiseUi", () => RunNoiseUiScreenshot(Path.Combine(dataDir, "sipi", "misc"), outDir));

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(Path.Combine(outDir, "results.json"), JsonSerializer.Serialize(results, options));
        File.WriteAllLines(Path.Combine(outDir, "errors.log"), log);
        return log.Count == 0 ? 0 : 1;
    }

    private static object RunNuts(string dir, string outDir)
    {
        var list = new List<object>();
        foreach (string file in Images(dir))
        {
            using Mat src = Load(file);
            using NutAnalysis a = NutCounter.Analyze(src);
            string stem = Path.GetFileNameWithoutExtension(file);
            Save(outDir, $"nuts_{stem}_before.jpg", src);
            Save(outDir, $"nuts_{stem}_mask.png", a.Mask);
            Save(outDir, $"nuts_{stem}_after.jpg", a.Annotated);
            list.Add(new { file = Path.GetFileName(file), hazelnut = a.HazelnutCount, walnut = a.WalnutCount,
                unseparated = a.UnseparatedCount, splitPixels = a.SplitDiameter,
                pixels = a.Items.Select(x => Math.Round(x.Area)).ToArray(),
                kinds = a.Items.Select(x => x.Kind).ToArray() });
        }
        return list;
    }

    private static object RunForeground(string dir, string outDir)
    {
        var backgrounds = Images(dir).Where(x => Path.GetFileName(x).StartsWith("bg", StringComparison.OrdinalIgnoreCase)).ToList();
        var list = new List<object>();
        foreach (string file in Images(dir).Except(backgrounds))
        {
            using Mat fg = Load(file);
            string stem = Path.GetFileNameWithoutExtension(file);
            Save(outDir, $"fg_{stem}_before.jpg", fg);
            foreach (string bgPath in backgrounds)
            {
                using Mat bgRaw = Load(bgPath);
                // Uydu görüntüsünün üstündeki yazı şeridi ve alttaki logo kırpılır (%5 üst, %7 alt).
                int top = bgRaw.Height * 5 / 100, bottom = bgRaw.Height * 7 / 100;
                using Mat bg = new Mat(bgRaw, new Rect(0, top, bgRaw.Width, bgRaw.Height - top - bottom)).Clone();
                (Mat composite, Mat mask) = ForegroundComposer.Compose(fg, bg);
                string bgStem = Path.GetFileNameWithoutExtension(bgPath);
                Save(outDir, $"fg_{stem}_mask.png", mask);
                Save(outDir, $"fg_{stem}_{bgStem}_after.jpg", composite);
                double ratio = Cv2.CountNonZero(mask) / (double)(mask.Width * mask.Height);
                composite.Dispose(); mask.Dispose();
                list.Add(new { file = Path.GetFileName(file), background = Path.GetFileName(bgPath), foregroundRatio = Math.Round(ratio, 4) });
            }
        }
        return list;
    }

    private static object RunNoise(string dir, string outDir)
    {
        var images = new[] { "4.2.07.tiff", "house.tiff", "5.2.09.tiff" }
            .Select(x => Path.Combine(dir, x)).Where(File.Exists).ToList();
        var psnr = new List<object>();
        foreach (string file in images)
        {
            using Mat clean = Load(file);
            string stem = Path.GetFileNameWithoutExtension(file).Replace('.', '_');
            foreach (double p in new[] { 0.02, 0.05, 0.10, 0.15, 0.20 })
            {
                using NoiseComparison c = NoiseAndMorphology.Compare(clean, p);
                psnr.Add(new { image = Path.GetFileName(file), density = p, noisy = Psnr(clean, c.Noisy),
                    square9 = c.SquarePsnr, cross5 = c.CrossPsnr, median = c.MedianPsnr });
                if (Math.Abs(p - 0.05) < 1e-9)
                {
                    Save(outDir, $"noise_{stem}_clean.png", clean);
                    Save(outDir, $"noise_{stem}_noisy.png", c.Noisy);
                    Save(outDir, $"noise_{stem}_square9.png", c.SquareResult);
                    Save(outDir, $"noise_{stem}_cross5.png", c.CrossResult);
                    Save(outDir, $"noise_{stem}_median.png", c.MedianResult);
                }
            }
        }

        // Kenar boşluklarını kapatma: gerçek görüntünün Canny kenarlarındaki kopukluklar.
        var edgeResults = new List<object>();
        foreach (string file in images)
        {
            using Mat clean = Load(file);
            using var gray = new Mat();
            using var edges = new Mat();
            Cv2.CvtColor(clean, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Canny(gray, edges, 80, 200);
            using Mat square = NoiseAndMorphology.CloseBrokenEdges(edges, KernelChoice.Square9, 1);
            using Mat cross = NoiseAndMorphology.CloseBrokenEdges(edges, KernelChoice.Cross5, 1);
            string stem = Path.GetFileNameWithoutExtension(file).Replace('.', '_');
            Save(outDir, $"edges_{stem}_canny.png", edges);
            Save(outDir, $"edges_{stem}_square9.png", square);
            Save(outDir, $"edges_{stem}_cross5.png", cross);
            edgeResults.Add(new { image = Path.GetFileName(file), segmentsBefore = Segments(edges),
                segmentsSquare9 = Segments(square), segmentsCross5 = Segments(cross) });
        }
        return new { psnr, edges = edgeResults };
    }

    private static object RunCoins(string dir, string outDir)
    {
        var list = new List<object>();
        foreach (string file in Images(dir))
        {
            using Mat src = Load(file);
            using CoinAnalysis a = CoinAnalyzer.Analyze(src);
            string stem = Path.GetFileNameWithoutExtension(file);
            Save(outDir, $"coins_{stem}_before.jpg", src);
            Save(outDir, $"coins_{stem}_mask.png", a.Mask);
            Save(outDir, $"coins_{stem}_after.jpg", a.Annotated);
            list.Add(new { file = Path.GetFileName(file), total = a.TotalLira,
                tl1 = a.Items.Count(x => x.Value >= 0.99), kr50 = a.Items.Count(x => x.Value is >= 0.49 and < 0.99),
                kr25 = a.Items.Count(x => x.Value < 0.49) });
        }
        return list;
    }

    private static object RunLake(string dataDir, string outDir)
    {
        // Ölçek: Nemrut kalderası merkezi ile Süphan Dağı zirvesi arası ~60 km (koordinatlardan hesaplandı),
        // görüntüde bu iki nokta arası ~265 piksel.
        using Mat src = Load(Path.Combine(dataDir, "van_landsat.jpg"));
        var a = new Point(238, 412);
        var b = new Point(462, 270);
        double pixels = Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
        using LakeAnalysis r = LakeAnalyzer.Analyze(src, 60_000, pixels);
        using Mat annotated = r.Annotated.Clone();
        Cv2.Line(annotated, a, b, new Scalar(0, 255, 255), 3, LineTypes.AntiAlias);
        Save(outDir, "lake_before.jpg", src);
        Save(outDir, "lake_mask.png", r.Mask);
        if (r.ColoredRegions is not null) Save(outDir, "lake_regions.png", r.ColoredRegions);
        Save(outDir, "lake_after.jpg", annotated);
        return new { method = r.ThresholdMethod, threshold = r.Threshold, scalePixels = pixels, scaleMeters = 60_000,
            areaKm2 = r.AreaSquareMeters / 1e6, perimeterKm = r.PerimeterMeters / 1000,
            contourPerimeterKm = r.ContourPerimeterMeters / 1000, regionCount = r.RegionCount,
            centerDistancePixels = r.CenterDistancePixels, referenceAreaKm2 = 3755 };
    }

    private static object RunThreshold(string dataDir, string outDir)
    {
        using Mat src = Load(Path.Combine(dataDir, "van_landsat.jpg"));
        IReadOnlyList<ThresholdCandidate> candidates = AutomaticThresholding.Evaluate(src);
        var rows = candidates.Select(c => new { channel = c.Name, inverted = c.Inverted, threshold = c.Threshold,
            score = double.IsFinite(c.Score) ? c.Score : -100, areaRatio = c.AreaRatio }).ToList();
        Save(outDir, "threshold_best_mask.png", candidates[0].Mask);
        Save(outDir, "threshold_worst_mask.png", candidates[^1].Mask);
        foreach (ThresholdCandidate c in candidates) c.Mask.Dispose();
        return rows;
    }

    /// <summary>
    /// Ödev 3'ün menüsünü (şablon, işlem, tur, R/G/B aralığı) gerçek arayüzden görüntü olarak kaydeder:
    /// Peppers'ta yalnız kırmızı pikseller (R 150-255, G 0-90, B 0-90) seçilip 5'li artı ile 2 tur genişletilir.
    /// </summary>
    private static object RunNoiseUiScreenshot(string sipiDir, string outDir)
    {
        using Mat peppers = Load(Path.Combine(sipiDir, "4.2.07.tiff"));
        using var form = new Form
        {
            Size = new System.Drawing.Size(1500, 860), StartPosition = FormStartPosition.Manual,
            Location = new System.Drawing.Point(-4000, -4000), ShowInTaskbar = false,
            Font = new System.Drawing.Font("Segoe UI", 9), BackColor = System.Drawing.Color.White
        };
        var page = new UI.NoisePage { Dock = DockStyle.Fill };
        form.Controls.Add(page);
        form.Show();
        Application.DoEvents();
        int[] ranges = [150, 255, 0, 90, 0, 90];
        page.Demo(peppers, kernelIndex: 1, operationIndex: 1, iterations: 2, ranges);
        Application.DoEvents();
        using var bmp = new System.Drawing.Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        page.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, page.Width, page.Height));
        bmp.Save(Path.Combine(outDir, "noise_ui_menu.png"), System.Drawing.Imaging.ImageFormat.Png);
        form.Close();
        return new { kernel = "5'li artı", operation = "Genişlet", iterations = 2, r = "150-255", g = "0-90", b = "0-90" };
    }

    private static int Segments(Mat binary)
    {
        using var labels = new Mat();
        return Cv2.ConnectedComponents(binary, labels) - 1;
    }

    private static double Psnr(Mat a, Mat b) => Cv2.PSNR(a, b);

    private static IEnumerable<string> Images(string dir) =>
        Directory.Exists(dir)
            ? Directory.GetFiles(dir).Where(f => new[] { ".jpg", ".jpeg", ".png", ".tiff", ".tif" }
                .Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => f, StringComparer.Ordinal)
            : [];

    // Türkçe karakterli yollarda Cv2.ImRead başarısız olduğu için baytlar üzerinden okunur/yazılır.
    private static Mat Load(string path)
    {
        Mat m = Cv2.ImDecode(File.ReadAllBytes(path), ImreadModes.Color);
        if (m.Empty()) throw new InvalidOperationException("Görüntü okunamadı: " + path);
        return m;
    }

    private static void Save(string dir, string name, Mat image)
    {
        Cv2.ImEncode(Path.GetExtension(name), image, out byte[] bytes);
        File.WriteAllBytes(Path.Combine(dir, name), bytes);
    }
}
