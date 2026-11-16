using GoruntuIslemeOdevleri.Core;
using OpenCvSharp;

namespace GoruntuIslemeOdevleri.Algorithms;

internal enum KernelChoice { Square9, Cross5 }
internal enum MorphologyChoice { Erode, Dilate, Open, Close, Median }

internal static class NoiseAndMorphology
{
    public static Mat AddSaltPepper(Mat source, double probability = 0.06, int seed = 42)
    {
        var result = source.Clone();
        var random = new Random(seed);
        int count = (int)(result.Width * result.Height * Math.Clamp(probability, 0, 0.4));
        for (int i = 0; i < count; i++)
        {
            int x = random.Next(result.Width);
            int y = random.Next(result.Height);
            byte value = random.Next(2) == 0 ? (byte)0 : (byte)255;
            if (result.Channels() == 1)
                result.Set(y, x, value);
            else
                result.Set(y, x, new Vec3b(value, value, value));
        }
        return result;
    }

    public static NoiseComparison Compare(Mat clean, double probability = 0.06)
    {
        Mat noisy = AddSaltPepper(clean, probability);
        Mat square = RemoveNoiseWithMorphology(noisy, KernelChoice.Square9);
        Mat cross = RemoveNoiseWithMorphology(noisy, KernelChoice.Cross5);
        var median = new Mat();
        Cv2.MedianBlur(noisy, median, 3);
        return new NoiseComparison(noisy, square, cross, median,
            ComputePsnr(clean, square), ComputePsnr(clean, cross), ComputePsnr(clean, median));
    }

    public static Mat Apply(Mat source, KernelChoice kernelChoice, MorphologyChoice operation, int iterations,
        int rMin = 0, int rMax = 255, int gMin = 0, int gMax = 255, int bMin = 0, int bMax = 255)
    {
        var detailed = ApplyDetailed(source, kernelChoice, operation, iterations, rMin, rMax, gMin, gMax, bMin, bMax);
        detailed.MaskBefore?.Dispose();
        detailed.MaskAfter?.Dispose();
        return detailed.Result;
    }

    /// <summary>
    /// Menüden seçilen şablon ve işlemi uygular.
    /// Renk aralığı tam (0-255) ise işlem doğrudan görüntüye uygulanır: her kanalda aşındırma = komşuların
    /// en küçüğü, genişletme = en büyüğü.
    /// Renk aralığı daraltılmışsa yalnız R, G, B değerleri aralıkta kalan pikseller "nesne" (birbirine komşu
    /// aynı küme) sayılır. Aşındırma/genişletme bu kümenin maskesine uygulanır; maskeye EKLENEN pikseller
    /// komşu nesne piksellerinin ortalama rengini, maskeden ÇIKARILAN pikseller komşu nesne-dışı piksellerin
    /// ortalama rengini alır. Böylece sonuç siyah-beyaz maske değil, renkli görüntü olarak kalır.
    /// </summary>
    public static (Mat Result, Mat? MaskBefore, Mat? MaskAfter) ApplyDetailed(Mat source, KernelChoice kernelChoice,
        MorphologyChoice operation, int iterations,
        int rMin = 0, int rMax = 255, int gMin = 0, int gMax = 255, int bMin = 0, int bMax = 255)
    {
        using Mat kernel = CreateKernel(kernelChoice);
        iterations = Math.Clamp(iterations, 1, 10);
        bool ranged = rMin > 0 || rMax < 255 || gMin > 0 || gMax < 255 || bMin > 0 || bMax < 255;

        if (!ranged)
        {
            var output = new Mat();
            ApplyMorphology(source, output, operation, kernel, iterations);
            return (output, null, null);
        }

        using Mat bgr = ImageHelpers.EnsureBgr(source);
        var before = new Mat();
        Cv2.InRange(bgr, new Scalar(bMin, gMin, rMin), new Scalar(bMax, gMax, rMax), before);
        var after = new Mat();
        ApplyMorphology(before, after, operation, kernel, iterations);

        using var added = new Mat();
        using var removed = new Mat();
        using var outside = new Mat();
        Cv2.Subtract(after, before, added);
        Cv2.Subtract(before, after, removed);
        Cv2.BitwiseNot(before, outside);

        // Açma/kapamada değişen piksel, aşındırma+genişletme turları kadar uzakta komşu bulabilir.
        int window = 2 * (2 * iterations) + 3;
        var result = bgr.Clone();
        using (Mat insideColor = NeighbourMean(bgr, before, window))
            insideColor.CopyTo(result, added);
        using (Mat outsideColor = NeighbourMean(bgr, outside, window))
            outsideColor.CopyTo(result, removed);
        return (result, before, after);
    }

    /// <summary>Her piksel için pencere içindeki, maskede işaretli komşuların ortalama rengi.</summary>
    private static Mat NeighbourMean(Mat bgr, Mat mask, int window)
    {
        using var colorF = new Mat();
        bgr.ConvertTo(colorF, MatType.CV_32FC3);
        using var weight = new Mat();
        mask.ConvertTo(weight, MatType.CV_32FC1, 1 / 255.0);
        using var weight3 = new Mat();
        Cv2.Merge([weight, weight, weight], weight3);
        using var weighted = new Mat();
        Cv2.Multiply(colorF, weight3, weighted);
        var size = new Size(window, window);
        Cv2.BoxFilter(weighted, weighted, MatType.CV_32F, size, normalize: false);
        Cv2.BoxFilter(weight3, weight3, MatType.CV_32F, size, normalize: false);
        using var fallback = new Mat();
        Cv2.MedianBlur(bgr, fallback, 3);
        using var empty = new Mat();
        Cv2.Compare(weight3, 0.5, empty, CmpTypes.LT);
        Cv2.Max(weight3, 1, weight3);
        using var mean = new Mat();
        Cv2.Divide(weighted, weight3, mean);
        var result = new Mat();
        mean.ConvertTo(result, MatType.CV_8UC3);
        using Mat emptyGray = new();
        Cv2.CvtColor(empty, emptyGray, ColorConversionCodes.BGR2GRAY);
        fallback.CopyTo(result, emptyGray);                            // yakında hiç komşu yoksa medyan
        return result;
    }

    /// <summary>Kenar görüntüsündeki ayrı parça (bağlı bileşen) sayısı: az parça = boşluklar kapanmış.</summary>
    public static int CountSegments(Mat binary)
    {
        using var labels = new Mat();
        return Cv2.ConnectedComponents(binary, labels) - 1;
    }

    /// <summary>
    /// Kopuk çizgi uçları: tam 1 komşusu olan kenar pikselleri. Her açıklık iki uç bırakır; kapatma
    /// başarılıysa uç sayısı azalır.
    /// </summary>
    public static int CountEndpoints(Mat binary)
    {
        using var bin = new Mat();
        Cv2.Threshold(binary, bin, 0, 1, ThresholdTypes.Binary);
        using var neighbours = new Mat();
        using var k = new Mat(3, 3, MatType.CV_32FC1, Scalar.All(1));
        k.Set(1, 1, 0f);
        Cv2.Filter2D(bin, neighbours, MatType.CV_32F, k);
        using var one = new Mat();
        Cv2.InRange(neighbours, 0.5, 1.5, one);
        using var bin8 = new Mat();
        bin.ConvertTo(bin8, MatType.CV_8UC1, 255);
        Cv2.BitwiseAnd(one, bin8, one);
        return Cv2.CountNonZero(one);
    }

    /// <summary>Gerçek bir fotoğrafın Canny kenarları (kopuklukları olan kenar görüntüsü).</summary>
    public static Mat EdgesOf(Mat source)
    {
        using Mat bgr = ImageHelpers.EnsureBgr(source);
        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(gray, gray, new Size(3, 3), 0);
        var edges = new Mat();
        Cv2.Canny(gray, edges, 80, 200);
        return edges;
    }

    public static Mat CloseBrokenEdges(Mat source, KernelChoice choice, int iterations = 2)
    {
        using Mat kernel = CreateKernel(choice);
        var output = new Mat();
        Cv2.MorphologyEx(source, output, MorphTypes.Close, kernel, iterations: iterations);
        return output;
    }

    private static Mat RemoveNoiseWithMorphology(Mat noisy, KernelChoice choice)
    {
        using Mat kernel = CreateKernel(choice);
        var result = new Mat();
        Cv2.MorphologyEx(noisy, result, MorphTypes.Open, kernel);
        Cv2.MorphologyEx(result, result, MorphTypes.Close, kernel);
        return result;
    }

    private static Mat CreateKernel(KernelChoice choice) => choice switch
    {
        KernelChoice.Cross5 => Cv2.GetStructuringElement(MorphShapes.Cross, new Size(3, 3)),
        _ => Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3))
    };

    private static void ApplyMorphology(Mat source, Mat output, MorphologyChoice operation, Mat kernel, int iterations)
    {
        iterations = Math.Clamp(iterations, 1, 10);
        switch (operation)
        {
            case MorphologyChoice.Erode:
                Cv2.Erode(source, output, kernel, iterations: iterations);
                break;
            case MorphologyChoice.Dilate:
                Cv2.Dilate(source, output, kernel, iterations: iterations);
                break;
            case MorphologyChoice.Open:
                Cv2.MorphologyEx(source, output, MorphTypes.Open, kernel, iterations: iterations);
                break;
            case MorphologyChoice.Close:
                Cv2.MorphologyEx(source, output, MorphTypes.Close, kernel, iterations: iterations);
                break;
            case MorphologyChoice.Median:
                using (Mat current = source.Clone())
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        using Mat next = MedianWithKernel(current, kernel);
                        next.CopyTo(current);
                    }
                    current.CopyTo(output);
                }
                break;
        }
    }

    /// <summary>
    /// Şablonlu medyan: şablonun işaretli her hücresi için görüntünün o yöne kaydırılmış bir kopyası alınır
    /// (9'lu kare → 9 kopya, 5'li artı → 5 kopya). Her pikselde bu değerler küçükten büyüğe sıralanır ve
    /// ortadaki alınır. Sıralama, kopyalar üzerinde ikili Min/Max karşılaştırmalarıyla (sıralama ağı) yapılır.
    /// Aşındırma bu listenin en küçüğünü, genişletme en büyüğünü alır; medyan ortadakini.
    /// </summary>
    private static Mat MedianWithKernel(Mat source, Mat kernel)
    {
        int ax = kernel.Cols / 2, ay = kernel.Rows / 2;
        using var padded = new Mat();
        Cv2.CopyMakeBorder(source, padded, ay, ay, ax, ax, BorderTypes.Replicate);
        var shifted = new List<Mat>();
        for (int y = 0; y < kernel.Rows; y++)
            for (int x = 0; x < kernel.Cols; x++)
                if (kernel.At<byte>(y, x) != 0)
                    shifted.Add(new Mat(padded, new Rect(x, y, source.Width, source.Height)).Clone());

        using var low = new Mat();
        for (int i = 0; i < shifted.Count; i++)
            for (int j = 0; j < shifted.Count - 1 - i; j++)
            {
                Cv2.Min(shifted[j], shifted[j + 1], low);
                Cv2.Max(shifted[j], shifted[j + 1], shifted[j + 1]);
                low.CopyTo(shifted[j]);
            }
        Mat median = shifted[shifted.Count / 2].Clone();
        foreach (Mat m in shifted) m.Dispose();
        return median;
    }

    public static double ComputePsnr(Mat reference, Mat candidate)
    {
        using var difference = new Mat();
        Cv2.Absdiff(reference, candidate, difference);
        difference.ConvertTo(difference, MatType.CV_32F);
        Cv2.Multiply(difference, difference, difference);
        Scalar sum = Cv2.Sum(difference);
        double squaredError = sum.Val0 + sum.Val1 + sum.Val2 + sum.Val3;
        if (squaredError <= 1e-10)
            return 99;

        double mse = squaredError / (reference.Total() * reference.Channels());
        return 10.0 * Math.Log10(255.0 * 255.0 / mse);
    }
}
