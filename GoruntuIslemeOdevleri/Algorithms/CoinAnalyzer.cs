using GoruntuIslemeOdevleri.Core;
using OpenCvSharp;

namespace GoruntuIslemeOdevleri.Algorithms;

/// <summary>
/// Ödev 4 – Bozuk para sayma, ders notundaki (8. hafta) tarife göre:
///  a) Paralar beyaz kâğıttan ayrılır, her para alan bulma ile ayrı bir bölge olarak farklı renge boyanır.
///     Her bölgenin en küçük ve en büyük x/y koordinatlarından çap bulunur; çaplar paraların gerçek
///     çaplarıyla ölçeklenerek hangi paranın kaç lira olduğu ve toplam tutar çıkarılır.
///  b) Her paranın kenar deseni (Canny) çıkarılır ve histogramı oluşturulur. Aynı büyüklükteki paralarda
///     yazı yüzleri birbirine, tura yüzleri birbirine benzer histogram verir; böylece yüzler ayrılır.
/// </summary>
internal static class CoinAnalyzer
{
    /// <summary>Türk madeni paralarının resmî çapları (TCMB).</summary>
    private static readonly (double Millimeters, double Lira, string Label)[] TurkishCoinSizes =
    [
        (20.50, 0.25, "25 kr"),
        (23.85, 0.50, "50 kr"),
        (26.15, 1.00, "1 TL")
    ];

    private const int RingCount = 8;      // histogramda merkezden kenara halka sayısı
    private const int AngleBins = 6;      // kenar yönü (radyal ↔ teğet) kutu sayısı

    public static CoinAnalysis Analyze(Mat source, Mat? headsReference = null, Mat? tailsReference = null)
    {
        // Çok büyük fotoğraflar 1600 piksele küçültülür: değer çap ORANLARINDAN bulunduğu için ölçek sonucu değiştirmez.
        using Mat full = ImageHelpers.EnsureBgr(source);
        using Mat bgr = ImageHelpers.ResizeToFit(full, 1600, 1600);
        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);

        // ---- a-1) Paraları kâğıttan ayır: beyaz kâğıt hem parlak hem renksizdir ----
        using Mat mask = ForegroundMask(bgr, gray);

        // ---- a-2) Alan bulma: her bağlı bölge bir para adayı ----
        using var labels = new Mat();
        using var stats = new Mat();
        using var centroids = new Mat();
        int count = Cv2.ConnectedComponentsWithStats(mask, labels, stats, centroids);
        double minArea = bgr.Width * bgr.Height * 0.002;

        var coins = new List<Coin>();
        for (int label = 1; label < count; label++)
        {
            int area = stats.At<int>(label, (int)ConnectedComponentsTypes.Area);
            if (area < minArea) continue;
            int left = stats.At<int>(label, (int)ConnectedComponentsTypes.Left);
            int top = stats.At<int>(label, (int)ConnectedComponentsTypes.Top);
            int width = stats.At<int>(label, (int)ConnectedComponentsTypes.Width);
            int height = stats.At<int>(label, (int)ConnectedComponentsTypes.Height);

            // a-3) Çap = en büyük − en küçük koordinat. Para eğik çekilince elips görünür; kısalan eksen
            //      eğimden, uzun eksen gerçek çaptan gelir, bu yüzden iki farkın büyüğü alınır.
            double diameter = Math.Max(width, height);
            double aspect = Math.Min(width, height) / (double)Math.Max(width, height);
            double fill = area / (Math.PI * width * height / 4.0);        // elips ise ≈ 1
            if (aspect < 0.40 || fill < 0.70) continue;                    // para değil ya da birleşik leke

            var center = new Point2f((float)centroids.At<double>(label, 0), (float)centroids.At<double>(label, 1));
            coins.Add(new Coin(label, new Rect(left, top, width, height), diameter, center, area));
        }

        // ---- a-4) Çapları gerçek çaplarla ölçekle → değer ----
        double pixelsPerMm = FitScale(coins.Select(c => c.Diameter).ToArray());
        foreach (Coin c in coins)
        {
            double mm = c.Diameter / pixelsPerMm;
            var best = TurkishCoinSizes.MinBy(x => Math.Abs(x.Millimeters - mm));
            c.Value = best.Lira;
            c.Label = best.Label;
            c.Millimeters = mm;
        }

        // ---- b) Kenar deseni histogramı → yazı / tura ----
        foreach (Coin c in coins)
            c.Histogram = EdgeHistogram(gray, labels, c);
        AssignFaces(coins, headsReference, tailsReference);

        // ---- Çıktı görüntüleri ----
        Mat colored = ColorRegions(labels, coins, bgr.Size());
        Mat annotated = Annotate(bgr, colored, coins, pixelsPerMm);
        var items = coins.OrderBy(c => c.Center.Y).ThenBy(c => c.Center.X)
            .Select(c => new CoinItem(c.Value, c.Face, c.Diameter, c.Center, c.FaceDistance)).ToList();
        return new CoinAnalysis(annotated, colored, items);
    }

    /// <summary>Beyaz kâğıt = parlak (V yüksek) ve renksiz (S düşük). Para bunun dışında kalan bölgedir.</summary>
    private static Mat ForegroundMask(Mat bgr, Mat gray)
    {
        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);
        var mask = new Mat();
        Cv2.Threshold(blurred, mask, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

        // Pirinç/sarı kısımlar griye göre kâğıda yakın kalabilir; doygunluk onları geri kazandırır.
        using (var hsv = new Mat())
        using (var satMask = new Mat())
        {
            Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);
            using Mat s = Cv2.Split(hsv)[1];
            Cv2.GaussianBlur(s, s, new Size(5, 5), 0);
            Cv2.Threshold(s, satMask, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            Cv2.BitwiseOr(mask, satMask, mask);
        }

        using Mat open = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
        using Mat close = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(9, 9));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, open);
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, close, iterations: 2);
        FillHoles(mask);
        // Ödev paraların kâğıt üzerinde ayrık durduğunu varsayar; birbirine değen paralar tek bölge olur ve
        // elips/doluluk kontrolünde elenir. (Mesafe dönüşümüyle ayırma, eğik çekilmiş tek parayı ikiye bölüyordu.)
        return mask;
    }

    /// <summary>
    /// Çapları gerçek çaplarla ölçekleme: her para ve her olası tür için bir "piksel/mm" adayı üretilir;
    /// tüm paraların en yakın gerçek çapa göreli uzaklığının karesi toplamı en küçük olan aday seçilir.
    /// "En büyük para 1 TL'dir" varsayımı gerekmez. Eşitlikte büyük değerli yorum seçilir.
    /// </summary>
    private static double FitScale(double[] diameters)
    {
        if (diameters.Length == 0) return 1;
        double bestScale = diameters.Max() / TurkishCoinSizes[^1].Millimeters;
        double bestCost = double.MaxValue;
        foreach (double d in diameters)
        foreach (var size in TurkishCoinSizes.Reverse())   // 1 TL'den başla: eşitlikte büyük yorum kalır
        {
            double scale = d / size.Millimeters;
            double cost = diameters.Sum(x =>
            {
                double mm = x / scale;
                return TurkishCoinSizes.Min(t => Math.Pow((mm - t.Millimeters) / t.Millimeters, 2));
            });
            if (cost < bestCost - 1e-9) { bestCost = cost; bestScale = scale; }
        }
        return bestScale;
    }

    /// <summary>
    /// Kenar deseni histogramı (döndürmeden etkilenmez):
    ///  • 8 halka: merkezden kenara her halkadaki kenar pikseli yoğunluğu (desen nerede yoğun?)
    ///  • 6 kutu: kenarın yönü ile merkeze doğru yön arasındaki açı (desen radyal mi, teğet mi?)
    /// Paranın dış çerçevesi (yüzde 8'lik kenar şeridi) her iki yüzde aynı olduğu için dışarıda bırakılır.
    /// </summary>
    private static double[] EdgeHistogram(Mat gray, Mat labels, Coin coin)
    {
        const int size = 128;
        using var roi = new Mat(gray, coin.Box);
        using var patch = new Mat();
        Cv2.Resize(roi, patch, new Size(size, size), 0, 0, InterpolationFlags.Area);
        Cv2.EqualizeHist(patch, patch);
        using var edges = new Mat();
        Cv2.Canny(patch, edges, 60, 160);
        using var gx = new Mat();
        using var gy = new Mat();
        Cv2.Sobel(patch, gx, MatType.CV_32F, 1, 0);
        Cv2.Sobel(patch, gy, MatType.CV_32F, 0, 1);

        var hist = new double[RingCount + AngleBins];
        var ringArea = new double[RingCount];
        double c = (size - 1) / 2.0, rMax = c * 0.92;
        int edgeCount = 0;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            double dx = x - c, dy = y - c, r = Math.Sqrt(dx * dx + dy * dy);
            if (r >= rMax) continue;
            int ring = Math.Min(RingCount - 1, (int)(r / rMax * RingCount));
            ringArea[ring]++;
            if (edges.At<byte>(y, x) == 0) continue;
            edgeCount++;
            hist[ring]++;
            double ex = gx.At<float>(y, x), ey = gy.At<float>(y, x);
            double cos = r < 1e-6 ? 1 : Math.Abs(ex * dx + ey * dy) / (Math.Sqrt(ex * ex + ey * ey) * r + 1e-9);
            double angle = Math.Acos(Math.Clamp(cos, 0, 1));                  // 0 = radyal, π/2 = teğet
            hist[RingCount + Math.Min(AngleBins - 1, (int)(angle / (Math.PI / 2) * AngleBins))]++;
        }
        for (int i = 0; i < RingCount; i++) hist[i] /= Math.Max(1, ringArea[i]);        // halka yoğunluğu
        double ringSum = hist.Take(RingCount).Sum();
        for (int i = 0; i < RingCount; i++) hist[i] /= Math.Max(1e-9, ringSum);
        for (int i = RingCount; i < hist.Length; i++) hist[i] /= Math.Max(1, edgeCount);
        return hist;
    }

    /// <summary>Ki-kare uzaklığı: iki histogram ne kadar farklı (0 = aynı).</summary>
    private static double ChiSquare(double[] a, double[] b) =>
        a.Zip(b, (x, y) => x + y < 1e-12 ? 0 : (x - y) * (x - y) / (x + y)).Sum() / 2;

    /// <summary>
    /// Ders notundaki fikir: aynı büyüklükteki paraların yazı yüzleri birbirine, tura yüzleri birbirine benzer
    /// histogram verir. Bu yüzden:
    ///  1) Her türdeki (25 kr / 50 kr / 1 TL) paralar histogram benzerliğine göre iki gruba ayrılır.
    ///     En farklı iki para tohum olur, diğerleri yakın olduğu tohumun grubuna girer.
    ///  2) Yazı ve tura referansı verildiyse, hangi grubun yazı hangisinin tura olduğu grupların ortalama
    ///     histogramının referanslara uzaklığıyla belirlenir. Referans yoksa gruplar "Yüz A / Yüz B" kalır.
    /// Tek paralı bir türde gruplama yapılamaz; referans varsa doğrudan yakın olana atanır.
    /// </summary>
    private static void AssignFaces(List<Coin> coins, Mat? headsRef, Mat? tailsRef)
    {
        double[]? heads = headsRef is null || headsRef.Empty() ? null : ReferenceHistogram(headsRef);
        double[]? tails = tailsRef is null || tailsRef.Empty() ? null : ReferenceHistogram(tailsRef);
        bool named = heads is not null && tails is not null;

        foreach (var group in coins.GroupBy(c => c.Label))
        {
            var list = group.ToList();
            if (list.Count < 2)
            {
                foreach (Coin c in list)
                {
                    if (!named) { c.Face = "Belirsiz"; continue; }
                    double dh = ChiSquare(c.Histogram, heads!), dt = ChiSquare(c.Histogram, tails!);
                    c.Face = dh <= dt ? "Yazı" : "Tura";
                    c.FaceDistance = Math.Abs(dh - dt);
                }
                continue;
            }

            (Coin a, Coin b) = list.SelectMany(x => list, (x, y) => (x, y))
                .MaxBy(p => ChiSquare(p.x.Histogram, p.y.Histogram));
            var groupA = new List<Coin>();
            var groupB = new List<Coin>();
            foreach (Coin c in list)
            {
                double da = ChiSquare(c.Histogram, a.Histogram), db = ChiSquare(c.Histogram, b.Histogram);
                (da <= db ? groupA : groupB).Add(c);
                c.FaceDistance = Math.Abs(da - db);
            }

            string nameA = "Yüz A", nameB = "Yüz B";
            if (named)
            {
                // A grubunun "yazıya yakınlık farkı" B'ninkinden küçükse A = yazı.
                double Lean(List<Coin> g) => g.Average(c => ChiSquare(c.Histogram, heads!) - ChiSquare(c.Histogram, tails!));
                bool aIsHeads = groupB.Count == 0 || Lean(groupA) <= Lean(groupB);
                (nameA, nameB) = aIsHeads ? ("Yazı", "Tura") : ("Tura", "Yazı");
            }
            groupA.ForEach(c => c.Face = nameA);
            groupB.ForEach(c => c.Face = nameB);
        }
    }

    private static double[] ReferenceHistogram(Mat reference)
    {
        using Mat bgr = ImageHelpers.EnsureBgr(reference);
        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        using Mat mask = ForegroundMask(bgr, gray);
        using var labels = new Mat();
        using var stats = new Mat();
        using var centroids = new Mat();
        int n = Cv2.ConnectedComponentsWithStats(mask, labels, stats, centroids);
        int best = Enumerable.Range(1, Math.Max(0, n - 1))
            .DefaultIfEmpty(0).MaxBy(i => i == 0 ? 0 : stats.At<int>(i, (int)ConnectedComponentsTypes.Area));
        Rect box = best == 0
            ? new Rect(0, 0, gray.Width, gray.Height)
            : new Rect(stats.At<int>(best, 0), stats.At<int>(best, 1), stats.At<int>(best, 2), stats.At<int>(best, 3));
        var coin = new Coin(best, box, (box.Width + box.Height) / 2.0, new Point2f(), 0);
        return EdgeHistogram(gray, labels, coin);
    }

    /// <summary>Alan bulma sonucu: her para bölgesi farklı bir renge boyanır (ders notundaki gösterim).</summary>
    private static Mat ColorRegions(Mat labels, List<Coin> coins, Size size)
    {
        var colored = new Mat(size, MatType.CV_8UC3, Scalar.All(0));
        var random = new Random(11);
        using var region = new Mat();
        foreach (Coin c in coins)
        {
            Cv2.Compare(labels, Scalar.All(c.RegionId), region, CmpTypes.EQ);
            colored.SetTo(new Scalar(random.Next(60, 256), random.Next(60, 256), random.Next(60, 256)), region);
        }
        return colored;
    }

    private static Mat Annotate(Mat bgr, Mat colored, List<Coin> coins, double pixelsPerMm)
    {
        // Renkli bölgeler yalnızca paraların üstüne yarı saydam bindirilir; kâğıt olduğu gibi kalır.
        var annotated = bgr.Clone();
        using (var blended = new Mat())
        using (var regionMask = new Mat())
        {
            Cv2.AddWeighted(bgr, 0.55, colored, 0.45, 0, blended);
            Cv2.CvtColor(colored, regionMask, ColorConversionCodes.BGR2GRAY);
            blended.CopyTo(annotated, regionMask);
        }
        int number = 1;
        foreach (Coin c in coins.OrderBy(c => c.Center.Y).ThenBy(c => c.Center.X))
        {
            Scalar color = c.Value switch
            {
                >= 0.99 => new Scalar(30, 80, 230),
                >= 0.49 => new Scalar(20, 170, 220),
                _ => new Scalar(40, 190, 70)
            };
            // En küçük/en büyük koordinatlar: çapın ölçüldüğü kutu
            Cv2.Rectangle(annotated, c.Box, color, 3, LineTypes.AntiAlias);
            string face = c.Face switch { "Yazı" => "Y", "Tura" => "T", "Yüz A" => "A", "Yüz B" => "B", _ => "?" };
            Cv2.PutText(annotated, $"{number}: {c.Label} {face}", new Point(c.Box.Left, Math.Max(20, c.Box.Top - 8)),
                HersheyFonts.HersheySimplex, 0.6, color, 2, LineTypes.AntiAlias);
            number++;
        }
        Cv2.PutText(annotated, $"Toplam: {coins.Sum(c => c.Value):0.00} TL   Olcek: {pixelsPerMm:0.0} px/mm",
            new Point(18, 38), HersheyFonts.HersheySimplex, 0.85, new Scalar(25, 25, 25), 3, LineTypes.AntiAlias);
        return annotated;
    }

    private static void FillHoles(Mat mask)
    {
        Cv2.FindContours(mask.Clone(), out Point[][] contours, out _, RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);
        Cv2.DrawContours(mask, contours, -1, Scalar.All(255), -1);
    }

    private sealed class Coin(int label, Rect box, double diameter, Point2f center, double area)
    {
        /// <summary>Alan bulma sonucundaki bölge numarası.</summary>
        public int RegionId { get; } = label;
        public Rect Box { get; } = box;
        public double Diameter { get; } = diameter;
        public Point2f Center { get; } = center;
        public double Area { get; } = area;
        public double Value { get; set; }
        /// <summary>Para türü: "25 kr", "50 kr", "1 TL".</summary>
        public string Label { get; set; } = "";
        public double Millimeters { get; set; }
        public double[] Histogram { get; set; } = [];
        public string Face { get; set; } = "Belirsiz";
        public double FaceDistance { get; set; }
    }
}
