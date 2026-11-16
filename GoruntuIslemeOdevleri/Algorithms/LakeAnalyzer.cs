using GoruntuIslemeOdevleri.Core;
using OpenCvSharp;

namespace GoruntuIslemeOdevleri.Algorithms;

/// <summary>
/// Ödev 5, PDF'teki adımlarla: eşikle → tüm beyaz bölgeleri bul ve farklı renklere boya →
/// ağırlık merkezi resmin ortasına en yakın bölgeyi göl kabul et → alanı aynı renkli piksel sayısıyla,
/// çevreyi "genişlet ve orijinali çıkar" ile bulunan tek piksellik kenarla ölç.
/// </summary>
internal static class LakeAnalyzer
{
    public static LakeAnalysis Analyze(Mat source, double realScaleMeters, double scalePixels)
    {
        if (realScaleMeters <= 0 || scalePixels <= 0)
            throw new ArgumentOutOfRangeException(nameof(realScaleMeters), "Ölçek uzunlukları sıfırdan büyük olmalıdır.");

        // 1) Eşik: araştırma bölümündeki otomatik seçim (tek kanallı adaylar; tam maske yeniden üretilebilsin diye).
        IReadOnlyList<ThresholdCandidate> candidates = AutomaticThresholding.Evaluate(source);
        ThresholdCandidate best = candidates.First(c => !c.Name.StartsWith("RGB", StringComparison.Ordinal));
        foreach (ThresholdCandidate c in candidates) c.Mask.Dispose();
        var steps = new List<(string Title, Mat Image)> { ("0. Uydu görüntüsü", ImageHelpers.EnsureBgr(source)) };
        using Mat binary = AutomaticThresholding.FullBinary(source, best.Name, best.Threshold, best.Inverted, steps);

        // 2) Bölgeleri etiketle ve her birini farklı renge boya.
        using var labels = new Mat();
        using var stats = new Mat();
        using var centroids = new Mat();
        int count = Cv2.ConnectedComponentsWithStats(binary, labels, stats, centroids);
        var center = new Point2d(source.Width / 2.0, source.Height / 2.0);
        double minArea = source.Width * source.Height * 0.001;

        int lakeLabel = -1;
        double bestDistance = double.MaxValue;
        int regionCount = 0;
        var colored = new Mat(source.Size(), MatType.CV_8UC3, Scalar.All(0));
        var random = new Random(7);
        for (int label = 1; label < count; label++)
        {
            int area = stats.At<int>(label, (int)ConnectedComponentsTypes.Area);
            if (area < minArea) continue;
            regionCount++;
            using var region = new Mat();
            Cv2.Compare(labels, Scalar.All(label), region, CmpTypes.EQ);
            colored.SetTo(new Scalar(random.Next(60, 256), random.Next(60, 256), random.Next(60, 256)), region);

            // 3) Ağırlık merkezi: bölgedeki piksellerin x ve y ortalaması.
            double cx = centroids.At<double>(label, 0), cy = centroids.At<double>(label, 1);
            double distance = Math.Sqrt(Math.Pow(cx - center.X, 2) + Math.Pow(cy - center.Y, 2));
            if (distance < bestDistance) { bestDistance = distance; lakeLabel = label; }
        }
        if (lakeLabel < 0)
        {
            colored.Dispose();
            throw new InvalidOperationException("Göl/arazi alanı tespit edilemedi.");
        }

        // Adım görüntüsü: boyalı bölgeler + her bölgenin ağırlık merkezi (sarı) + resmin ortası (beyaz +)
        Mat regionsStep = colored.Clone();
        for (int label = 1; label < count; label++)
            if (stats.At<int>(label, (int)ConnectedComponentsTypes.Area) >= minArea)
                Cv2.Circle(regionsStep, new Point((int)centroids.At<double>(label, 0), (int)centroids.At<double>(label, 1)),
                    7, new Scalar(0, 255, 255), -1, LineTypes.AntiAlias);
        Cv2.DrawMarker(regionsStep, new Point((int)center.X, (int)center.Y), Scalar.All(255), MarkerTypes.Cross, 30, 3);
        steps.Add(($"4. Bölgeleri boyama: {regionCount} bölge, ağırlık merkezleri", regionsStep));

        var lakeMask = new Mat();
        Cv2.Compare(labels, Scalar.All(lakeLabel), lakeMask, CmpTypes.EQ);
        FillHoles(lakeMask);   // göl içindeki adacıklar gölün alanına dahil
        steps.Add(($"5. Göl = merkeze en yakın bölge ({bestDistance:0} px)", lakeMask.Clone()));

        // 4) Alan = aynı renkli piksel sayısı × (m/piksel)².
        double metersPerPixel = realScaleMeters / scalePixels;
        double pixelArea = Cv2.CountNonZero(lakeMask);
        double areaSquareMeters = pixelArea * metersPerPixel * metersPerPixel;

        // 5) Çevre = (genişletilmiş − orijinal) kenar piksel sayısı × m/piksel.
        using var dilated = new Mat();
        using var edge = new Mat();
        using (Mat k = Cv2.GetStructuringElement(MorphShapes.Cross, new Size(3, 3)))
            Cv2.Dilate(lakeMask, dilated, k);
        Cv2.Subtract(dilated, lakeMask, edge);
        double edgePixels = Cv2.CountNonZero(edge);
        double perimeterMeters = edgePixels * metersPerPixel;
        using (Mat k = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5)))
        {
            var edgeStep = new Mat();
            Cv2.Dilate(edge, edgeStep, k);   // tek piksellik kenar küçük önizlemede görünsün diye kalınlaştırıldı
            steps.Add(($"6. Kenar = genişlet − orijinal ({edgePixels:0} piksel)", edgeStep));
        }

        // Karşılaştırma için kontur uzunluğu (alt-piksel, çaprazları √2 sayar).
        Cv2.FindContours(lakeMask.Clone(), out Point[][] contours, out _, RetrievalModes.External,
            ContourApproximationModes.ApproxNone);
        double contourPerimeterMeters = contours.Length == 0 ? 0 : Cv2.ArcLength(contours.MaxBy(c => c.Length)!, true) * metersPerPixel;

        double cxL = centroids.At<double>(lakeLabel, 0), cyL = centroids.At<double>(lakeLabel, 1);
        var centroid = new Point2d(cxL, cyL);

        using Mat bgr = ImageHelpers.EnsureBgr(source);
        var annotated = bgr.Clone();
        annotated.SetTo(new Scalar(0, 0, 255), edge);
        Cv2.DrawMarker(annotated, new Point((int)center.X, (int)center.Y), new Scalar(255, 255, 255),
            MarkerTypes.Cross, 24, 2);
        Cv2.Circle(annotated, new Point((int)cxL, (int)cyL), 8, new Scalar(0, 255, 255), -1, LineTypes.AntiAlias);
        Cv2.PutText(annotated, $"Alan: {areaSquareMeters / 1_000_000.0:0.00} km2",
            new Point(18, 38), HersheyFonts.HersheySimplex, 0.82, new Scalar(255, 255, 255), 3, LineTypes.AntiAlias);
        Cv2.PutText(annotated, $"Cevre: {perimeterMeters / 1000.0:0.00} km",
            new Point(18, 76), HersheyFonts.HersheySimplex, 0.82, new Scalar(255, 255, 255), 3, LineTypes.AntiAlias);

        steps.Add(($"7. Sonuç: {areaSquareMeters / 1e6:0} km², {perimeterMeters / 1000:0} km", annotated.Clone()));

        return new LakeAnalysis(annotated, lakeMask, best.Name + (best.Inverted ? " (ters)" : ""),
            best.Threshold, areaSquareMeters, perimeterMeters, centroid)
        {
            ColoredRegions = colored,
            RegionCount = regionCount,
            ContourPerimeterMeters = contourPerimeterMeters,
            CenterDistancePixels = bestDistance,
            Steps = steps
        };
    }

    private static void FillHoles(Mat mask)
    {
        Cv2.FindContours(mask.Clone(), out Point[][] contours, out _, RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);
        Cv2.DrawContours(mask, contours, -1, Scalar.All(255), -1);
    }
}
