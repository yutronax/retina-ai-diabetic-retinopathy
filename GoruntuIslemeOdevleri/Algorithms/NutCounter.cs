using GoruntuIslemeOdevleri.Core;
using OpenCvSharp;

namespace GoruntuIslemeOdevleri.Algorithms;

internal static class NutCounter
{
    public static NutAnalysis Analyze(Mat source)
    {
        using Mat bgr = ImageHelpers.EnsureBgr(source);
        using var gray = new Mat();
        using var blurred = new Mat();
        var mask = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);
        Cv2.Threshold(blurred, mask, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

        // Beyaz/gri zemin renksizdir, ceviz ve fındık kahverengidir: doygunluk (HSV-S) kanalındaki
        // Otsu maskesi, parlama yüzünden gri eşiklemede kopan bölgeleri geri kazandırır.
        using (var hsv = new Mat())
        using (var saturationMask = new Mat())
        {
            Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);
            using Mat saturation = Cv2.Split(hsv)[1];
            Cv2.GaussianBlur(saturation, saturation, new Size(5, 5), 0);
            Cv2.Threshold(saturation, saturationMask, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            Cv2.BitwiseOr(mask, saturationMask, mask);
        }

        using Mat openKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
        using Mat closeKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(9, 9));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, openKernel);
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, closeKernel, iterations: 2);
        FillHoles(mask);

        // Birbirine değen nesneler tek leke olur; mesafe dönüşümüyle bulunan çekirdeklere göre ayrılır.
        using Mat separated = SplitTouching(mask);
        using Mat contourInput = separated.Clone();
        Cv2.FindContours(contourInput, out Point[][] contours, out _, RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        // PDF yöntemi: her bölge ayrı bir renge boyanır ve o renkteki piksel sayısı (alan) ölçülür.
        double minArea = bgr.Width * bgr.Height * 0.0012;
        var regions = new List<(Point[] Contour, double Pixels, Point2f Center, double Diameter, bool Separable)>();
        using var regionMask = new Mat(mask.Size(), MatType.CV_8UC1);
        foreach (Point[] contour in contours)
        {
            regionMask.SetTo(Scalar.All(0));
            Cv2.DrawContours(regionMask, [contour], -1, Scalar.All(255), -1);
            double pixels = Cv2.CountNonZero(regionMask);
            if (pixels < minArea)
                continue;

            // Yuvarlaklığı düşük bölge birden çok nesnenin birleşimidir: sayılmaz, "ayrılamadı" olarak işaretlenir.
            double perimeter = Math.Max(Cv2.ArcLength(contour, true), 1);
            double circularity = 4 * Math.PI * Cv2.ContourArea(contour) / (perimeter * perimeter);
            Cv2.MinEnclosingCircle(contour, out Point2f center, out float radius);
            regions.Add((contour, pixels, center, radius * 2.0, circularity >= 0.55));
        }

        var separable = regions.Where(r => r.Separable).ToList();
        double split = FindLargestGapSplit(separable.Select(x => x.Pixels).ToArray());
        var annotated = bgr.Clone();
        using var overlay = bgr.Clone();
        var items = new List<NutItem>();
        var random = new Random(3);

        int number = 1;
        foreach (var r in regions.OrderBy(x => x.Center.Y).ThenBy(x => x.Center.X))
        {
            string kind = !r.Separable ? "Ayrılamadı" : r.Pixels >= split ? "Ceviz" : "Fındık";
            Scalar fill = new(random.Next(40, 256), random.Next(40, 256), random.Next(40, 256));
            Cv2.DrawContours(overlay, [r.Contour], -1, r.Separable ? fill : new Scalar(160, 160, 160), -1);
            Scalar color = kind switch { "Ceviz" => new Scalar(40, 70, 230), "Fındık" => new Scalar(30, 185, 70), _ => new Scalar(90, 90, 90) };
            Cv2.DrawContours(annotated, [r.Contour], -1, color, 4, LineTypes.AntiAlias);
            string tag = kind switch { "Ceviz" => "C", "Fındık" => "F", _ => "?" };
            Cv2.PutText(annotated, $"{number}:{tag} {r.Pixels / 1000:0}k px", new Point((int)r.Center.X - 60, (int)r.Center.Y),
                HersheyFonts.HersheySimplex, 0.7, color, 2, LineTypes.AntiAlias);
            items.Add(new NutItem(kind, r.Pixels, r.Diameter, r.Center));
            number++;
        }
        Cv2.AddWeighted(annotated, 0.55, overlay, 0.45, 0, annotated);

        Cv2.PutText(annotated, $"Findik: {items.Count(x => x.Kind == "Fındık")}  Ceviz: {items.Count(x => x.Kind == "Ceviz")}  Ayrilamadi: {items.Count(x => x.Kind == "Ayrılamadı")}",
            new Point(18, 38), HersheyFonts.HersheySimplex, 0.9, new Scalar(20, 20, 20), 3,
            LineTypes.AntiAlias);
        mask.Dispose();
        return new NutAnalysis(annotated, separated.Clone(), items, split);
    }

    /// <summary>Nesne içindeki kapalı delikleri (parlama, gölge) doldurur: kenardan ulaşılamayan siyah = delik.</summary>
    private static void FillHoles(Mat mask)
    {
        using Mat flood = mask.Clone();
        using var floodMask = new Mat(mask.Rows + 2, mask.Cols + 2, MatType.CV_8UC1, Scalar.All(0));
        for (int x = 0; x < mask.Cols; x++)
        {
            if (flood.At<byte>(0, x) == 0) Cv2.FloodFill(flood, floodMask, new Point(x, 0), Scalar.All(255));
            if (flood.At<byte>(mask.Rows - 1, x) == 0) Cv2.FloodFill(flood, floodMask, new Point(x, mask.Rows - 1), Scalar.All(255));
        }
        for (int y = 0; y < mask.Rows; y++)
        {
            if (flood.At<byte>(y, 0) == 0) Cv2.FloodFill(flood, floodMask, new Point(0, y), Scalar.All(255));
            if (flood.At<byte>(y, mask.Cols - 1) == 0) Cv2.FloodFill(flood, floodMask, new Point(mask.Cols - 1, y), Scalar.All(255));
        }
        using var holes = new Mat();
        Cv2.BitwiseNot(flood, holes);
        Cv2.BitwiseOr(mask, holes, mask);
    }

    internal static Mat SplitTouching(Mat mask)
    {
        // 1) Mesafe dönüşümü: her nesne pikselinin zemine uzaklığı; nesne merkezlerinde tepe yapar.
        using var distance = new Mat();
        Cv2.DistanceTransform(mask, distance, DistanceTypes.L2, DistanceTransformMasks.Mask5);
        Cv2.MinMaxLoc(distance, out _, out double maxDistance);

        // 2) Çekirdekler: tepelerin üst kısmı. Birbirine değen nesneler arasındaki "boğaz" daha sığ
        //    olduğu için bu eşikte her nesne ayrı bir çekirdek bırakır.
        //    Tek bir oran büyük cevizle küçük fındığı aynı anda ayıramaz; bu yüzden 0,30-0,85 arası
        //    oranlar denenir ve en çok ayrı çekirdek veren oran seçilir.
        using var seeds = new Mat();
        int bestCount = -1;
        for (double ratio = 0.30; ratio <= 0.851; ratio += 0.05)
        {
            using var candidate = new Mat();
            Cv2.Threshold(distance, candidate, ratio * maxDistance, 255, ThresholdTypes.Binary);
            candidate.ConvertTo(candidate, MatType.CV_8UC1);
            using var lbl = new Mat();
            using var stats = new Mat();
            using var cents = new Mat();
            int n = Cv2.ConnectedComponentsWithStats(candidate, lbl, stats, cents);
            int valid = 0;
            for (int i = 1; i < n; i++)
                if (stats.At<int>(i, (int)ConnectedComponentsTypes.Area) >= 25) valid++;
            if (valid > bestCount) { bestCount = valid; candidate.CopyTo(seeds); }
        }
        using var seedLabels = new Mat();
        int count = Cv2.ConnectedComponents(seeds, seedLabels);

        // 3) Maske içindeki her piksel en yakın çekirdeğe atanır (çekirdek dışı = 0 olan görüntüde
        //    DistanceTransformWithLabels en yakın sıfır bileşeninin etiketini verir).
        using var notSeeds = new Mat();
        Cv2.BitwiseNot(seeds, notSeeds);
        using var toSeed = new Mat();
        using var nearest = new Mat();
        Cv2.DistanceTransformWithLabels(notSeeds, toSeed, nearest, DistanceTypes.L2,
            DistanceTransformMasks.Mask5, DistanceTransformLabelTypes.CComp);

        // nearest etiketleri sıfır-bileşen sırasına göredir; çekirdek etiketine eşlemek için her
        // çekirdeğin bir pikselinden okunur.
        var result = new Mat(mask.Size(), MatType.CV_8UC1, Scalar.All(0));
        using var region = new Mat();
        using var seedPixel = new Mat();
        using Mat k3 = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
        for (int label = 1; label < count; label++)
        {
            Cv2.Compare(seedLabels, Scalar.All(label), seedPixel, CmpTypes.EQ);
            using var nonZero = new Mat();
            Cv2.FindNonZero(seedPixel, nonZero);
            Point any = nonZero.At<Point>(0);
            int nearestLabel = nearest.At<int>(any.Y, any.X);
            Cv2.Compare(nearest, Scalar.All(nearestLabel), region, CmpTypes.EQ);
            Cv2.BitwiseAnd(region, mask, region);
            Cv2.Erode(region, region, k3);                       // komşu nesneler arasında boşluk bırak
            Cv2.BitwiseOr(result, region, result);
        }
        return result;
    }

    private static double FindLargestGapSplit(double[] values)
    {
        if (values.Length == 0) return double.MaxValue;
        if (values.Length == 1) return values[0] * 1.25;

        double[] sorted = values.OrderBy(x => x).ToArray();
        int splitIndex = 0;
        double largestGap = double.MinValue;
        for (int i = 0; i < sorted.Length - 1; i++)
        {
            double gap = sorted[i + 1] - sorted[i];
            if (gap > largestGap)
            {
                largestGap = gap;
                splitIndex = i;
            }
        }

        double relativeGap = largestGap / Math.Max(1, sorted.Average());
        return relativeGap >= 0.12
            ? (sorted[splitIndex] + sorted[splitIndex + 1]) / 2.0
            : sorted.Average();
    }
}
