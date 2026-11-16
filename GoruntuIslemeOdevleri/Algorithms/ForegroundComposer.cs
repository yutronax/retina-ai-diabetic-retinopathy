using OpenCvSharp;

namespace GoruntuIslemeOdevleri.Algorithms;

/// <summary>
/// Ödev 2: düz renkli duvar önündeki kişiyi ayırıp yeni bir manzaranın önüne yerleştirir.
///
/// Aşamalar:
///  1. Duvar rengini öğren: görüntünün üst kenarı ve yan kenarların üst yarısı (kişi genelde
///     alt kenara ve yanlara omuzlarıyla değer, üst kenara değmez).
///  2. Renk uzaklığı: her pikselin Lab uzayında duvar rengine uzaklığı; eşik Otsu ile bulunur.
///  3. Duvar maskesi: duvara benzeyen VE kenardan başlayıp kesintisiz uzanan bölgeler. Kişinin
///     içindeki beyaz gömlek, gri sakal gibi duvar rengindeki kısımlar kenara bağlı olmadığı için
///     arka plan sayılmaz.
///  4. GrabCut ile sınır iyileştirme: kesin duvar / kesin kişi / belirsiz şerit maskesiyle başlatılır.
///  5. Yumuşak kenar (alfa): sınır şeridinde saydamlık renk uzaklığından hesaplanır; saç telleri
///     yarı saydam olur.
///  6. Hale temizliği: kenar piksellerindeki duvar rengi, I = αF + (1-α)B denkleminden F çözülerek
///     çıkarılır (beyaz çerçeve kalmaz).
///  7. Birleştirme: manzara oranı bozulmadan kırpılıp boyutlandırılır, sonuç = αF + (1-α)Manzara.
/// </summary>
internal static class ForegroundComposer
{
    public static (Mat Composite, Mat Mask) Compose(Mat foreground, Mat background, int iterations = 5)
    {
        ForegroundResult r = Analyze(foreground, background, iterations);
        foreach (var step in r.Steps) step.Image.Dispose();
        return (r.Composite, r.Alpha);
    }

    internal sealed record ForegroundResult(Mat Composite, Mat Alpha, Mat Cutout, Scalar WallColor, double ColorThreshold,
        List<(string Title, Mat Image)> Steps);

    public static ForegroundResult Analyze(Mat foreground, Mat background, int iterations = 5)
    {
        if (foreground.Empty() || background.Empty())
            throw new ArgumentException("Ön plan ve arka plan görüntüleri boş olamaz.");

        using Mat source = foreground.Channels() == 3 ? foreground.Clone() : ToBgr(foreground);
        int w = source.Width, h = source.Height;

        // 1) Duvar örnekleri: üst şerit + yan şeritlerin üst yarısı (kişi buralara değmez).
        using var lab = new Mat();
        Cv2.CvtColor(source, lab, ColorConversionCodes.BGR2Lab);
        using var labF = new Mat();
        lab.ConvertTo(labF, MatType.CV_32FC3);
        int band = Math.Max(4, Math.Min(w, h) / 40);
        using var border = new Mat(source.Size(), MatType.CV_8UC1, Scalar.All(0));
        border[new Rect(0, 0, w, band)].SetTo(Scalar.All(255));
        border[new Rect(0, 0, band, h / 2)].SetTo(Scalar.All(255));
        border[new Rect(w - band, 0, band, h / 2)].SetTo(Scalar.All(255));
        Scalar wallBgr = Cv2.Mean(source, border);
        using Mat k5 = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));

        // 2-3) İki geçiş. 1. geçiş: duvar tek renk kabul edilir (kenar örneklerinin ortalaması).
        //      2. geçiş: gerçek duvar düz renkte değildir; ışık bir yandan gelir, köşeler kararır
        //      (vinyetleme). 1. geçişte bulunan duvar piksellerine konuma bağlı 2. derece bir yüzey
        //      oturtulur:  renk(x, y) = c0 + c1·x + c2·y + c3·x² + c4·x·y + c5·y²  (en küçük kareler).
        //      Böylece alt köşedeki koyu duvar da "kendi yerindeki duvar rengiyle" karşılaştırılır.
        using var flatModel = new Mat(source.Size(), MatType.CV_32FC3, Cv2.Mean(lab, border));
        var (distance, colorThreshold, wall) = FindWall(labF, flatModel, border, border, k5);
        using (Mat model = FitWallModel(labF, wall))
        {
            distance.Dispose();
            using Mat firstWall = wall;
            (distance, colorThreshold, wall) = FindWall(labF, model, firstWall, border, k5);
        }
        using var distanceOwner = distance;
        using var wallOwner = wall;
        using var distance8 = new Mat();
        distance.ConvertTo(distance8, MatType.CV_8UC1);

        using var person = new Mat();
        Cv2.BitwiseNot(wall, person);
        KeepLargest(person);

        // 4) GrabCut: kesin duvar (sınırdan uzak duvar), kesin kişi (sınırdan uzak kişi) ve
        //    aradaki belirsiz şerit. Yalnızca şerit yeniden sınıflandırılır.
        int ring = Math.Max(3, Math.Min(w, h) / 80);
        using Mat ringKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(2 * ring + 1, 2 * ring + 1));
        using var sureFg = new Mat();
        using var sureBg = new Mat();
        Cv2.Erode(person, sureFg, ringKernel);
        Cv2.Dilate(person, sureBg, ringKernel);
        Cv2.BitwiseNot(sureBg, sureBg);
        using var grabMask = new Mat(source.Size(), MatType.CV_8UC1, Scalar.All((int)GrabCutClasses.PR_BGD));
        grabMask.SetTo(Scalar.All((int)GrabCutClasses.PR_FGD), person);
        grabMask.SetTo(Scalar.All((int)GrabCutClasses.FGD), sureFg);
        grabMask.SetTo(Scalar.All((int)GrabCutClasses.BGD), sureBg);
        using (var bgModel = new Mat())
        using (var fgModel = new Mat())
            Cv2.GrabCut(source, grabMask, new Rect(), bgModel, fgModel, Math.Clamp(iterations, 1, 10), GrabCutModes.InitWithMask);
        using var refined = new Mat();
        using (var one = new Mat(grabMask.Size(), MatType.CV_8UC1, Scalar.All(1)))
            Cv2.BitwiseAnd(grabMask, one, refined);                    // FGD(1) ve PR_FGD(3) → 1
        refined.ConvertTo(refined, MatType.CV_8UC1, 255);
        Cv2.MorphologyEx(refined, refined, MorphTypes.Close, k5);
        KeepLargest(refined);
        FillInteriorHoles(refined);

        // 5) Alfa (yumuşak kenar): sınır şeridindeki her piksel, kişi rengi F ile duvar rengi W'nin
        //    karışımıdır: I = αF + (1-α)W. F ve W o pikselin hemen içindeki kişi ve hemen dışındaki
        //    duvar renklerinden tahmin edilir (maskeli bulanıklaştırma); α, I'nın W→F doğrusu üzerindeki
        //    izdüşümüdür:  α = ((I-W)·(F-W)) / |F-W|².  Saç telleri yarı saydam, ceket kenarı keskin olur.
        using Mat edgeKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(7, 7));
        using var inner = new Mat();
        using var outer = new Mat();
        Cv2.Erode(refined, inner, edgeKernel);
        Cv2.Dilate(refined, outer, edgeKernel);
        using var edgeBand = new Mat();
        Cv2.Subtract(outer, inner, edgeBand);
        using var outside = new Mat();
        Cv2.BitwiseNot(outer, outside);

        using var sourceF = new Mat();
        source.ConvertTo(sourceF, MatType.CV_32FC3);
        int spread = Math.Max(15, Math.Min(w, h) / 40) | 1;
        using Mat localFg = MaskedBlur(sourceF, inner, spread);
        using Mat localWall = MaskedBlur(sourceF, outside, spread, wallBgr);

        var alpha = new Mat();
        refined.ConvertTo(alpha, MatType.CV_32FC1, 1 / 255.0);
        using (var d = new Mat())
        using (var f = new Mat())
        using (var df = new Mat())
        using (var ff = new Mat())
        using (var projected = new Mat())
        {
            Cv2.Subtract(sourceF, localWall, d);
            Cv2.Subtract(localFg, localWall, f);
            Cv2.Multiply(d, f, df);
            Cv2.Multiply(f, f, ff);
            using Mat dot = SumChannels(df);
            using Mat norm = SumChannels(ff);
            Cv2.Max(norm, 100, norm);                                   // |F-W| < 10 ise bölme kararsızlaşmasın
            Cv2.Divide(dot, norm, projected);
            Cv2.Max(projected, 0, projected);
            Cv2.Min(projected, 1, projected);
            projected.CopyTo(alpha, edgeBand);
        }
        Cv2.GaussianBlur(alpha, alpha, new Size(3, 3), 0);

        // 6) Hale temizliği: aynı denklemden F = (I - (1-α)W) / α. Kenar pikselindeki beyaz duvar
        //    payı çıkarılır; manzaraya geçince kişinin etrafında beyaz çerçeve kalmaz.
        using var alpha3 = new Mat();
        Cv2.Merge([alpha, alpha, alpha], alpha3);
        using var fgColor = new Mat();
        using (var oneMinus = new Mat())
        using (var safeAlpha = new Mat())
        {
            Cv2.Subtract(Scalar.All(1), alpha3, oneMinus);
            Cv2.Multiply(oneMinus, localWall, oneMinus);
            Cv2.Subtract(sourceF, oneMinus, fgColor);
            Cv2.Max(alpha3, 0.05, safeAlpha);
            Cv2.Divide(fgColor, safeAlpha, fgColor);
            Cv2.Max(fgColor, 0, fgColor);
            Cv2.Min(fgColor, 255, fgColor);
        }

        // 7) Manzarayı oranını bozmadan kişinin görüntü boyutuna kırp ve birleştir.
        using Mat landscape = CoverResize(background.Channels() == 3 ? background : ToBgr(background), source.Size());
        using var landscapeF = new Mat();
        landscape.ConvertTo(landscapeF, MatType.CV_32FC3);
        using var blended = new Mat();
        using (var a = new Mat())
        using (var b = new Mat())
        using (var oneMinus = new Mat())
        {
            Cv2.Multiply(alpha3, fgColor, a);
            Cv2.Subtract(Scalar.All(1), alpha3, oneMinus);
            Cv2.Multiply(oneMinus, landscapeF, b);
            Cv2.Add(a, b, blended);
        }
        var composite = new Mat();
        blended.ConvertTo(composite, MatType.CV_8UC3);

        var alpha8 = new Mat();
        alpha.ConvertTo(alpha8, MatType.CV_8UC1, 255);
        alpha.Dispose();

        var cutout = CutoutOnChecker(fgColor, alpha3);

        // Ara görüntüler (rapor ve arayüz için).
        var distanceView = new Mat();
        Cv2.Normalize(distance8, distanceView, 0, 255, NormTypes.MinMax);
        Cv2.ApplyColorMap(distanceView, distanceView, ColormapTypes.Inferno);
        var wallView = source.Clone();
        using (var tint = new Mat(source.Size(), MatType.CV_8UC3, new Scalar(60, 200, 60)))
        using (var mixed = new Mat())
        {
            Cv2.AddWeighted(source, 0.4, tint, 0.6, 0, mixed);
            mixed.CopyTo(wallView, wall);
        }
        var trimap = new Mat(source.Size(), MatType.CV_8UC3, new Scalar(128, 128, 128));
        trimap.SetTo(Scalar.All(0), sureBg);
        trimap.SetTo(Scalar.All(255), sureFg);
        var steps = new List<(string Title, Mat Image)>
        {
            ("1. Duvar rengine uzaklık (koyu: duvara benzer)", distanceView),
            ($"2. Duvar maskesi (yeşil), eşik = {colorThreshold:0}", wallView),
            ("3. GrabCut başlangıcı: siyah duvar, beyaz kişi, gri belirsiz", trimap),
            ("4. Alfa maskesi (yumuşak kenar)", ToBgr(alpha8)),
            ("5. Kesilen kişi (hale temizlenmiş)", cutout.Clone()),
            ("6. Sonuç: kişi manzara önünde", composite.Clone()),
        };

        return new ForegroundResult(composite, alpha8, cutout, wallBgr, colorThreshold, steps);
    }

    /// <summary>Yalnızca maske içindeki piksellerin ortalamasıyla bulanıklaştırır (normalize konvolüsyon).
    /// Maskenin dışındaki noktalara en yakın maske-içi renkler taşınır.</summary>
    private static Mat MaskedBlur(Mat imageF, Mat mask, int size, Scalar? fallback = null)
    {
        using var weight = new Mat();
        mask.ConvertTo(weight, MatType.CV_32FC1, 1 / 255.0);
        using var weight3 = new Mat();
        Cv2.Merge([weight, weight, weight], weight3);
        using var weighted = new Mat();
        Cv2.Multiply(imageF, weight3, weighted);
        var k = new Size(size, size);
        Cv2.GaussianBlur(weighted, weighted, k, 0);
        Cv2.GaussianBlur(weight, weight, k, 0);
        using var empty = new Mat();
        Cv2.Compare(weight, 1e-3, empty, CmpTypes.LT);                 // yakınında hiç maske pikseli yok
        Cv2.Max(weight, 1e-3, weight);
        Cv2.Merge([weight, weight, weight], weight3);
        var result = new Mat();
        Cv2.Divide(weighted, weight3, result);
        if (fallback is Scalar fb)
            result.SetTo(fb, empty);
        return result;
    }

    private static Mat SumChannels(Mat image)
    {
        Mat[] ch = Cv2.Split(image);
        var sum = new Mat();
        Cv2.Add(ch[0], ch[1], sum);
        Cv2.Add(sum, ch[2], sum);
        foreach (Mat c in ch) c.Dispose();
        return sum;
    }

    /// <summary>
    /// Renk uzaklığı haritası ve duvar maskesi. Uzaklık = sqrt(ΔL²/4 + Δa² + Δb²): parlaklık farkı
    /// yarı ağırlıklıdır, çünkü duvardaki gölge ve ışık değişimi renk tonundan çok parlaklığı değiştirir.
    /// Eşik duvarın KENDİ dağılımından bulunur: örnek duvar piksellerinin %98'inin altında kaldığı
    /// uzaklığın 1,5 katı + 4. (Otsu burada işe yaramaz: koyu ceket histogramda çok baskın olduğundan
    /// Otsu "ceket / geri kalan" diye böler ve açık teni de duvar sayar.)
    /// Duvar = eşiğin altında kalan VE üst / yan-üst kenara bağlı bölgeler; kişinin içindeki beyaz
    /// gömlek, gri sakal gibi duvar rengindeki kısımlar kenara bağlı olmadığından duvar sayılmaz.
    /// </summary>
    private static (Mat Distance, double Threshold, Mat Wall) FindWall(Mat labF, Mat model, Mat samples, Mat border, Mat kernel)
    {
        Mat distance = ColorDistance(labF, model);
        distance.GetArray(out float[] all);
        samples.GetArray(out byte[] sample);
        float[] wallValues = all.Where((_, i) => sample[i] != 0).Order().ToArray();
        double p98 = wallValues.Length == 0 ? 10 : wallValues[(int)(wallValues.Length * 0.98)];
        double threshold = Math.Clamp(p98 * 1.5 + 4, 8, 40);

        using var wallLike = new Mat();
        Cv2.Threshold(distance, wallLike, threshold, 255, ThresholdTypes.BinaryInv);
        wallLike.ConvertTo(wallLike, MatType.CV_8UC1);
        Cv2.MorphologyEx(wallLike, wallLike, MorphTypes.Open, kernel);
        return (distance, threshold, BorderConnected(wallLike, border));
    }

    /// <summary>Duvar piksellerine konumun 2. derece polinomunu oturtur (her Lab kanalı ayrı), modeli döndürür.</summary>
    private static Mat FitWallModel(Mat labF, Mat wall)
    {
        int w = labF.Width, h = labF.Height;
        wall.GetArray(out byte[] isWall);
        labF.GetArray(out Vec3f[] pixels);
        int stride = Math.Max(1, (int)Math.Sqrt(w * h / 40000.0));       // ~40 bin örnek yeter
        var rows = new List<int>();
        for (int y = 0; y < h; y += stride)
            for (int x = 0; x < w; x += stride)
                if (isWall[y * w + x] != 0) rows.Add(y * w + x);
        var model = new Mat(labF.Size(), MatType.CV_32FC3);
        if (rows.Count < 50)
        {
            model.SetTo(Cv2.Mean(labF, wall));
            return model;
        }

        static double[] Terms(double x, double y) => [1, x, y, x * x, x * y, y * y];
        using var a = new Mat(rows.Count, 6, MatType.CV_64FC1);
        using var b = new Mat(rows.Count, 3, MatType.CV_64FC1);
        for (int r = 0; r < rows.Count; r++)
        {
            int i = rows[r];
            double[] t = Terms(i % w / (double)w, i / w / (double)h);
            for (int c = 0; c < 6; c++) a.Set(r, c, t[c]);
            for (int c = 0; c < 3; c++) b.Set(r, c, (double)pixels[i][c]);
        }
        using var coef = new Mat();
        Cv2.Solve(a, b, coef, DecompTypes.SVD);
        var k = new double[6, 3];
        for (int r = 0; r < 6; r++)
            for (int c = 0; c < 3; c++) k[r, c] = coef.At<double>(r, c);

        var values = new Vec3f[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double[] t = Terms(x / (double)w, y / (double)h);
                var v = new Vec3f();
                for (int c = 0; c < 3; c++)
                {
                    double sum = 0;
                    for (int r = 0; r < 6; r++) sum += t[r] * k[r, c];
                    v[c] = (float)sum;
                }
                values[y * w + x] = v;
            }
        model.SetArray(values);
        return model;
    }

    private static Mat ColorDistance(Mat labF, Mat model)
    {
        using var diff = new Mat();
        Cv2.Subtract(labF, model, diff);
        Cv2.Multiply(diff, diff, diff);
        Mat[] ch = Cv2.Split(diff);
        var sum = new Mat();
        Cv2.AddWeighted(ch[0], 0.25, ch[1], 1.0, 0, sum);              // L farkının karesi 1/4 → L yarı ağırlıklı
        Cv2.Add(sum, ch[2], sum);
        Cv2.Sqrt(sum, sum);
        foreach (Mat c in ch) c.Dispose();
        return sum;
    }

    /// <summary>Maskedeki, kenar şeridine (seed) değen bağlı bileşenleri döndürür.</summary>
    private static Mat BorderConnected(Mat mask, Mat seed)
    {
        using var labels = new Mat();
        int n = Cv2.ConnectedComponents(mask, labels);
        labels.GetArray(out int[] lbl);
        seed.GetArray(out byte[] s);
        var keep = new bool[n];
        for (int i = 0; i < lbl.Length; i++)
            if (s[i] != 0 && lbl[i] != 0) keep[lbl[i]] = true;
        var result = new byte[lbl.Length];
        for (int i = 0; i < lbl.Length; i++)
            if (keep[lbl[i]] && lbl[i] != 0) result[i] = 255;
        var output = new Mat(mask.Size(), MatType.CV_8UC1);
        output.SetArray(result);
        return output;
    }

    private static void KeepLargest(Mat mask)
    {
        using var labels = new Mat();
        using var stats = new Mat();
        using var cents = new Mat();
        int n = Cv2.ConnectedComponentsWithStats(mask, labels, stats, cents);
        if (n <= 2) return;
        int best = 1;
        for (int i = 2; i < n; i++)
            if (stats.At<int>(i, (int)ConnectedComponentsTypes.Area) > stats.At<int>(best, (int)ConnectedComponentsTypes.Area))
                best = i;
        Cv2.Compare(labels, Scalar.All(best), mask, CmpTypes.EQ);
    }

    /// <summary>Kişinin içinde kalan, görüntü kenarına ulaşmayan boşlukları doldurur.</summary>
    private static void FillInteriorHoles(Mat mask)
    {
        using var inverse = new Mat();
        Cv2.BitwiseNot(mask, inverse);
        using var edge = new Mat(mask.Size(), MatType.CV_8UC1, Scalar.All(255));
        edge[new Rect(1, 1, mask.Width - 2, mask.Height - 2)].SetTo(Scalar.All(0));
        using Mat outside = BorderConnected(inverse, edge);
        Cv2.BitwiseNot(outside, mask);
    }

    /// <summary>Görüntüyü oranını koruyarak hedefi tamamen kaplayacak şekilde büyütür ve ortadan kırpar.</summary>
    private static Mat CoverResize(Mat image, Size target)
    {
        double scale = Math.Max(target.Width / (double)image.Width, target.Height / (double)image.Height);
        int width = Math.Max(target.Width, (int)Math.Ceiling(image.Width * scale));
        int height = Math.Max(target.Height, (int)Math.Ceiling(image.Height * scale));
        using var resized = new Mat();
        Cv2.Resize(image, resized, new Size(width, height), 0, 0, InterpolationFlags.Area);
        var roi = new Rect((width - target.Width) / 2, (height - target.Height) / 2, target.Width, target.Height);
        return new Mat(resized, roi).Clone();
    }

    /// <summary>Kesilen ön planı saydamlığı gösteren damalı zemin üzerinde verir.</summary>
    private static Mat CutoutOnChecker(Mat fgColor, Mat alpha3)
    {
        var checker = new Mat(fgColor.Size(), MatType.CV_32FC3);
        int cell = Math.Max(8, Math.Min(fgColor.Width, fgColor.Height) / 24);
        for (int y = 0; y < fgColor.Height; y += cell)
            for (int x = 0; x < fgColor.Width; x += cell)
            {
                bool light = ((x / cell) + (y / cell)) % 2 == 0;
                var rect = new Rect(x, y, Math.Min(cell, fgColor.Width - x), Math.Min(cell, fgColor.Height - y));
                checker[rect].SetTo(light ? Scalar.All(235) : Scalar.All(200));
            }
        using var a = new Mat();
        using var oneMinus = new Mat();
        Cv2.Multiply(alpha3, fgColor, a);
        Cv2.Subtract(Scalar.All(1), alpha3, oneMinus);
        Cv2.Multiply(oneMinus, checker, checker);
        Cv2.Add(a, checker, checker);
        var result = new Mat();
        checker.ConvertTo(result, MatType.CV_8UC3);
        checker.Dispose();
        return result;
    }

    private static Mat ToBgr(Mat image)
    {
        var output = new Mat();
        Cv2.CvtColor(image, output, ColorConversionCodes.GRAY2BGR);
        return output;
    }
}
