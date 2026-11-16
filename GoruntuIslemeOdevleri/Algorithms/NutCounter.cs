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
        double grayThreshold = Cv2.Threshold(blurred, mask, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
        double saturationThreshold;

        // Beyaz/gri zemin renksizdir, ceviz ve fındık kahverengidir: doygunluk (HSV-S) kanalındaki
        // Otsu maskesi, parlama yüzünden gri eşiklemede kopan bölgeleri geri kazandırır.
        using (var hsv = new Mat())
        using (var saturationMask = new Mat())
        {
            Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);
            using Mat saturation = Cv2.Split(hsv)[1];
            Cv2.GaussianBlur(saturation, saturation, new Size(5, 5), 0);
            saturationThreshold = Cv2.Threshold(saturation, saturationMask, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            Cv2.BitwiseOr(mask, saturationMask, mask);
        }
        using Mat rawMask = mask.Clone();                               // adım görüntüleri için: temizlik öncesi

        using Mat openKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
        using Mat closeKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(9, 9));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, openKernel);
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, closeKernel);
        FillHoles(mask, bgr);

        // Birbirine değen nesneler tek leke olur; mesafe dönüşümünün yerel tepeleri işaretçi alınıp
        // watershed ile ayrılır.
        using Mat separated = SplitTouching(mask);
        using Mat contourInput = separated.Clone();
        Cv2.FindContours(contourInput, out Point[][] contours, out _, RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        // PDF yöntemi: her bölge ayrı bir renge boyanır ve o renkteki piksel sayısı (alan) ölçülür.
        double minArea = bgr.Width * bgr.Height * 0.0012;
        var regions = new List<(Point[] Contour, double Pixels, Point2f Center, double Diameter, bool Separable, double Redness, double Hue)>();
        using var regionMask = new Mat(mask.Size(), MatType.CV_8UC1);
        using var labImage = new Mat();
        using var hsvImage = new Mat();
        Cv2.CvtColor(bgr, labImage, ColorConversionCodes.BGR2Lab);
        Cv2.CvtColor(bgr, hsvImage, ColorConversionCodes.BGR2HSV);
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

            // Renk: kenardaki gölge ve zemin karışmasın diye bölgenin iç kısmından ölçülür.
            int shrink = Math.Max(3, (int)(Math.Sqrt(pixels / Math.PI) * 0.15));
            using (Mat k = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(2 * shrink + 1, 2 * shrink + 1)))
                Cv2.Erode(regionMask, regionMask, k);
            double redness = Cv2.Mean(labImage, regionMask).Val1 - 128;   // Lab a: + kırmızı, - yeşil
            double hue = Cv2.Mean(hsvImage, regionMask).Val0;             // 0-180; küçük = kırmızıya yakın
            regions.Add((contour, pixels, center, radius * 2.0, circularity >= 0.55, redness, hue));
        }

        var separable = regions.Where(r => r.Separable).ToList();
        (double split, double gapRatio) = FindLargestGapSplit(separable.Select(x => x.Pixels).ToArray());

        // Fotoğrafta gerçekten iki tür var mı? Büyüklük yalnız GÖRELİDİR: tek tür olan bir fotoğrafta da
        // "küçükler" ve "büyükler" çıkar ve en küçük cevizler fındık sanılır. Bu yüzden büyüklük ayrımı
        // ancak renkle de doğrulanırsa kabul edilir: fındık kırmızımsı kahve, ceviz sarımsı bej olduğundan
        // küçük grup büyük gruptan belirgin şekilde daha kırmızı olmalıdır (Lab a farkı ≥ 3).
        var small = separable.Where(r => r.Pixels < split).ToList();
        var large = separable.Where(r => r.Pixels >= split).ToList();
        double rednessGap = small.Count > 0 && large.Count > 0
            ? small.Average(r => r.Redness) - large.Average(r => r.Redness) : 0;
        bool twoTypes = gapRatio >= 1.25 && rednessGap >= 3;
        string singleKind = "";
        string decision;
        if (separable.Count == 0)
            decision = "Ayrı bir nesne bulunamadı (zemin beyaz değilse ya da nesneler üst üsteyse sayım yapılamaz).";
        else if (twoTypes)
            decision = $"İki tür var: alanlar arasında {gapRatio:0.00} kat sıçrama ve küçük grup {rednessGap:0.0} birim daha kırmızı. " +
                       $"Eşik {split:0} px'in altı fındık, üstü ceviz.";
        else
        {
            // Tek tür: hangisi olduğuna ortalama renk tonuyla karar verilir (fındık kırmızıya yakın, H < 14).
            double meanHue = separable.Count > 0 ? separable.Average(r => r.Hue) : 0;
            singleKind = meanHue < 14 ? "Fındık" : "Ceviz";
            split = double.NaN;
            decision = gapRatio >= 1.25
                ? $"Büyüklükler {gapRatio:0.00} kat ayrışıyor ama renk farkı yok ({rednessGap:0.0} < 3): hepsi aynı tür, boyları farklı. "
                : $"Belirgin büyüklük sıçraması yok ({gapRatio:0.00} kat): hepsi aynı tür. ";
            decision += $"Ortalama renk tonu H = {meanHue:0.0} → hepsi {singleKind.ToLowerInvariant()}.";
        }
        var annotated = bgr.Clone();
        using var overlay = bgr.Clone();
        var colored = new Mat(bgr.Size(), MatType.CV_8UC3, Scalar.All(255));   // ödevdeki "renklendirme" görüntüsü
        var items = new List<NutItem>();
        var random = new Random(3);

        int number = 1;
        foreach (var r in regions.OrderBy(x => x.Center.Y).ThenBy(x => x.Center.X))
        {
            string kind = !r.Separable ? "Ayrılamadı" : !twoTypes ? singleKind : r.Pixels >= split ? "Ceviz" : "Fındık";
            Scalar fill = new(random.Next(40, 256), random.Next(40, 256), random.Next(40, 256));
            Cv2.DrawContours(overlay, [r.Contour], -1, r.Separable ? fill : new Scalar(160, 160, 160), -1);
            Cv2.DrawContours(colored, [r.Contour], -1, r.Separable ? fill : new Scalar(160, 160, 160), -1);
            Cv2.PutText(colored, $"{number}: {r.Pixels:0} px", new Point((int)r.Center.X - 70, (int)r.Center.Y + 8),
                HersheyFonts.HersheySimplex, 0.8, new Scalar(20, 20, 20), 2, LineTypes.AntiAlias);
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
        var steps = BuildSteps(bgr, rawMask, mask, separated, colored, annotated);
        mask.Dispose();
        return new NutAnalysis(annotated, separated.Clone(), items, split)
            { Steps = steps, GrayThreshold = grayThreshold, SaturationThreshold = saturationThreshold, Decision = decision };
    }

    /// <summary>
    /// Ödevde anlatılan aşamaların ara görüntüleri (zemin beyaz, nesneler siyah gösterilir):
    /// eşikleme → kenar çizgileri → noktasal gölgelerin silinmesi → değen nesnelerin ayrılması →
    /// bölgelerin renklendirilmesi → sonuç.
    /// </summary>
    private static List<(string Title, Mat Image)> BuildSteps(Mat bgr, Mat rawMask, Mat cleanMask, Mat separated,
        Mat colored, Mat annotated)
    {
        static Mat WhiteBackground(Mat objectMask)
        {
            using var inverted = new Mat();
            Cv2.BitwiseNot(objectMask, inverted);
            var bgrImage = new Mat();
            Cv2.CvtColor(inverted, bgrImage, ColorConversionCodes.GRAY2BGR);
            return bgrImage;
        }

        // 2) Kenar çizgileri: eşiklenmiş maskedeki her lekenin dış sınırı, beyaz zemin üzerinde.
        var edges = new Mat(bgr.Size(), MatType.CV_8UC3, Scalar.All(255));
        using (Mat contourInput = rawMask.Clone())
        {
            Cv2.FindContours(contourInput, out Point[][] rawContours, out _, RetrievalModes.List,
                ContourApproximationModes.ApproxNone);
            Cv2.DrawContours(edges, rawContours, -1, Scalar.All(0), 2, LineTypes.AntiAlias);
        }

        // 3) Temizlik: silinen noktasal gölgeler kırmızı, doldurulan delikler mavi gösterilir.
        Mat cleaned = WhiteBackground(cleanMask);
        using (var removed = new Mat())
        using (var filled = new Mat())
        using (Mat grow = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(7, 7)))
        {
            Cv2.Subtract(rawMask, cleanMask, removed);
            Cv2.Subtract(cleanMask, rawMask, filled);
            Cv2.Dilate(removed, removed, grow);                         // tek piksellik noktalar görünsün
            cleaned.SetTo(new Scalar(40, 40, 230), removed);
            cleaned.SetTo(new Scalar(230, 120, 30), filled);
        }

        return
        [
            ("1. Eşikleme: zemin beyaz, nesneler siyah", WhiteBackground(rawMask)),
            ("2. Kenar çizgileri", edges),
            ("3. Noktasal gölgeler silindi (kırmızı: silinen, mavi: doldurulan)", cleaned),
            ("4. Değen nesneler ayrıldı (watershed)", WhiteBackground(separated)),
            ("5. Bölgeler renklendirildi ve piksel sayıldı", colored),
            ("6. Sonuç: ceviz / fındık", annotated.Clone()),
        ];
    }

    /// <summary>
    /// Nesne içindeki kapalı delikleri (parlama, gölge) doldurur: kenardan ulaşılamayan siyah = delik.
    /// Birbirine değen nesnelerin arasında kalan zemin cebi de kapalı görünür; onu doldurmak nesneleri
    /// kaynaştırır. Rengi zemine benzeyen ve parlama lekesinden büyük delikler bu yüzden boş bırakılır.
    /// </summary>
    private static void FillHoles(Mat mask, Mat bgr)
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
        using var lbl = new Mat();
        using var stats = new Mat();
        using var cents = new Mat();
        int n = Cv2.ConnectedComponentsWithStats(holes, lbl, stats, cents);
        if (n <= 1) return;

        // Zemin rengi: maske dışındaki piksellerin ortalaması (gri ve HSV doygunluğu).
        using var gray = new Mat();
        using var hsv = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);
        using Mat saturation = Cv2.Split(hsv)[1];
        using var background = new Mat();
        Cv2.BitwiseOr(mask, holes, background);
        Cv2.BitwiseNot(background, background);
        double bgGray = Cv2.Mean(gray, background).Val0;
        double bgSat = Cv2.Mean(saturation, background).Val0;

        double minPocket = mask.Width * mask.Height * 0.0002;
        using var hole = new Mat();
        for (int i = 1; i < n; i++)
        {
            Cv2.Compare(lbl, Scalar.All(i), hole, CmpTypes.EQ);
            bool large = stats.At<int>(i, (int)ConnectedComponentsTypes.Area) >= minPocket;
            bool backgroundLike = Cv2.Mean(gray, hole).Val0 >= bgGray - 30 && Cv2.Mean(saturation, hole).Val0 <= bgSat + 30;
            if (!(large && backgroundLike))
                Cv2.BitwiseOr(mask, hole, mask);
        }
    }

    internal static Mat SplitTouching(Mat mask)
    {
        // 1) Mesafe dönüşümü: her nesne pikselinin zemine uzaklığı; her nesnenin merkezinde bir tepe olur.
        using var distance = new Mat();
        Cv2.DistanceTransform(mask, distance, DistanceTypes.L2, DistanceTransformMasks.Mask5);
        Cv2.MinMaxLoc(distance, out _, out double maxDistance);
        var result = new Mat(mask.Size(), MatType.CV_8UC1, Scalar.All(0));
        if (maxDistance < 1) return result;

        // 2) İşaretçiler: yerel tepeler. Tek bir genel eşik (ör. maxDistance'ın %50'si) büyük cevizle
        //    küçük fındığı aynı anda ayıramaz: düşük eşikte temas bölgesi de kalır, yüksek eşikte fındık
        //    tepesi kaybolur. Bunun yerine her piksel, çevresindeki en yüksek değere eşitse tepe sayılır.
        //    Komşuluk yarıçapı en büyük nesnenin yarıçapının yaklaşık yarısıdır; aynı nesne içinde ikinci
        //    bir tepe çıkmasını engeller, komşu nesnenin merkezine ise ulaşmaz.
        int radius = Math.Max(3, (int)(maxDistance * 0.45));
        using Mat peakKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(2 * radius + 1, 2 * radius + 1));
        using var localMax = new Mat();
        Cv2.Dilate(distance, localMax, peakKernel);
        using var diff = new Mat();
        Cv2.Subtract(localMax, distance, diff);
        using var peaks = new Mat();
        Cv2.Threshold(diff, peaks, 0.5, 255, ThresholdTypes.BinaryInv);   // tepe: localMax - distance < 0,5
        peaks.ConvertTo(peaks, MatType.CV_8UC1);
        using var highEnough = new Mat();
        Cv2.Threshold(distance, highEnough, maxDistance * 0.2, 255, ThresholdTypes.Binary);
        highEnough.ConvertTo(highEnough, MatType.CV_8UC1);
        Cv2.BitwiseAnd(peaks, highEnough, peaks);
        Cv2.BitwiseAnd(peaks, mask, peaks);
        using Mat merge = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
        Cv2.Dilate(peaks, peaks, merge, iterations: 2);                    // düzlük tepeleri tek işaretçi olsun
        Cv2.BitwiseAnd(peaks, mask, peaks);

        using var markers = new Mat();
        int count = Cv2.ConnectedComponents(peaks, markers, PixelConnectivity.Connectivity8, MatType.CV_32SC1);

        // 3) Watershed (mesafe yüzeyinde): işaretçiler, zemine en uzak pikselden başlayarak dışa doğru
        //    büyür. İki nesnenin temas çizgisi mesafe yüzeyinde bir "vadi"dir; komşu işaretçilerin
        //    bölgeleri orada buluşur ve sınır nesneler arasındaki boğaza çizilir. Yalnızca maske içi
        //    pikseller büyütüldüğü için zemin hiçbir nesneye katılmaz.
        //    (OpenCV'nin Cv2.Watershed'i komşu piksel renk farkına baktığından cevizin dokusunu kenar
        //    sanıyordu; bu yüzden taşırma burada mesafe değerine göre yapılır.)
        int w = mask.Cols, h = mask.Rows;
        mask.GetArray(out byte[] inside);
        distance.GetArray(out float[] depth);
        markers.GetArray(out int[] labels);
        var queue = new PriorityQueue<int, float>();
        var peak = new float[count];                                    // her işaretçinin tepe yüksekliği
        for (int i = 0; i < labels.Length; i++)
            if (labels[i] > 0)
            {
                queue.Enqueue(i, -depth[i]);
                peak[labels[i]] = Math.Max(peak[labels[i]], depth[i]);
            }
        // İki bölgenin ilk buluştuğu noktanın yüksekliği = aralarındaki "eyer" (boğaz kalınlığı).
        var saddle = new Dictionary<(int, int), float>();
        while (queue.TryDequeue(out int p, out _))
        {
            int x = p % w, y = p / w;
            int lab = labels[p];
            void Grow(int q)
            {
                if (inside[q] == 0) return;
                if (labels[q] == 0)
                {
                    labels[q] = lab;
                    queue.Enqueue(q, -depth[q]);
                }
                else if (labels[q] != lab)
                {
                    var key = (Math.Min(lab, labels[q]), Math.Max(lab, labels[q]));
                    float level = Math.Min(depth[p], depth[q]);
                    if (!saddle.TryGetValue(key, out float old) || level > old) saddle[key] = level;
                }
            }
            if (x > 0) Grow(p - 1);
            if (x < w - 1) Grow(p + 1);
            if (y > 0) Grow(p - w);
            if (y < h - 1) Grow(p + w);
        }

        // 4) Küçük parçaları birleştir: üç nesnenin birleştiği köşede (koyu gölgeli kama) maske kalın
        //    olduğundan mesafe yüzeyinde sahte bir tepe çıkabilir. Böyle bir parça, bölgelerin ortanca
        //    alanının yarısından küçüktür ve tek başına bir kuruyemiş değildir; en kalın boğazla (en
        //    yüksek eyer) bağlı olduğu komşusuna katılır. Değmeyen (komşusuz) bölgelere dokunulmaz.
        var area = new double[count];
        foreach (int l in labels) if (l > 0) area[l]++;
        var parent = Enumerable.Range(0, count).ToArray();
        int Find(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
        double[] sortedAreas = area.Skip(1).Where(a => a > 0).Order().ToArray();
        double medianArea = sortedAreas.Length == 0 ? 0 : sortedAreas[sortedAreas.Length / 2];
        foreach (int r in Enumerable.Range(1, count - 1).OrderBy(r => area[r]))
        {
            int root = Find(r);
            var neighbours = saddle.Where(kv => Find(kv.Key.Item1) == root ^ Find(kv.Key.Item2) == root)
                .Select(kv => (Other: Find(Find(kv.Key.Item1) == root ? kv.Key.Item2 : kv.Key.Item1), Level: kv.Value))
                .ToList();
            if (neighbours.Count == 0) continue;
            if (area[root] >= 0.5 * medianArea) continue;
            int target = neighbours.MaxBy(n => n.Level).Other;
            parent[root] = target;
            area[target] += area[root];
            area[root] = 0;
        }
        for (int i = 0; i < labels.Length; i++)
            if (labels[i] > 0) labels[i] = Find(labels[i]);
        markers.SetArray(labels);

        using var region = new Mat();
        using Mat k5 = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
        for (int label = 1; label < count; label++)
        {
            Cv2.Compare(markers, Scalar.All(label), region, CmpTypes.EQ);
            Cv2.Erode(region, region, k5);                               // komşu nesneler arasında boşluk bırak
            Cv2.BitwiseOr(result, region, result);
        }
        return result;
    }

    /// <summary>Sıralı alanlar arasındaki en büyük oransal sıçrama: (eşik = iki alanın geometrik ortası, sıçrama oranı).</summary>
    private static (double Split, double Ratio) FindLargestGapSplit(double[] values)
    {
        if (values.Length < 2) return (double.MaxValue, 1);

        // Boşluk fark (b - a) değil oran (b / a) olarak ölçülür: büyük nesnelerin alanları arasındaki
        // mutlak fark doğal olarak büyüktür (ör. 73k ile 98k'lık iki ceviz arası 25k), oysa ceviz ile
        // fındık arasındaki asıl ayrım "kaç kat büyük" sorusudur (49k fındık → 73k ceviz = 1,5 kat).
        double[] sorted = values.OrderBy(x => x).ToArray();
        int splitIndex = 0;
        double largestRatio = double.MinValue;
        for (int i = 0; i < sorted.Length - 1; i++)
        {
            double ratio = sorted[i + 1] / Math.Max(1, sorted[i]);
            if (ratio > largestRatio)
            {
                largestRatio = ratio;
                splitIndex = i;
            }
        }
        return (Math.Sqrt(sorted[splitIndex] * sorted[splitIndex + 1]), largestRatio);
    }
}
