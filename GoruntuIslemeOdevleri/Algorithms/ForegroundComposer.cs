using OpenCvSharp;

namespace GoruntuIslemeOdevleri.Algorithms;

internal static class ForegroundComposer
{
    public static (Mat Composite, Mat Mask) Compose(Mat foreground, Mat background, int iterations = 5)
    {
        if (foreground.Empty() || background.Empty())
            throw new ArgumentException("Ön plan ve arka plan görüntüleri boş olamaz.");

        using Mat source = foreground.Channels() == 3 ? foreground.Clone() : ToBgr(foreground);
        using var resizedBackground = new Mat();
        Cv2.Resize(background, resizedBackground, source.Size(), 0, 0, InterpolationFlags.Linear);

        int marginX = Math.Max(2, source.Width / 30);
        int marginY = Math.Max(2, source.Height / 30);
        var rectangle = new Rect(marginX, marginY, source.Width - 2 * marginX, source.Height - 2 * marginY);
        var grabMask = new Mat(source.Size(), MatType.CV_8UC1, Scalar.All((int)GrabCutClasses.BGD));
        using var backgroundModel = new Mat();
        using var foregroundModel = new Mat();

        Cv2.GrabCut(source, grabMask, rectangle, backgroundModel, foregroundModel,
            Math.Clamp(iterations, 1, 10), GrabCutModes.InitWithRect);

        // Düz duvar varsayımı: kenar şeridinden duvar rengini öğren, ona yakın pikselleri kesin arka plan yap
        // ve GrabCut'ı bu maskeyle yeniden çalıştır. Beyaz duvar parçalarının ön planda kalmasını önler.
        using (Mat wallLike = WallColorMask(source, marginX, marginY))
        {
            grabMask.SetTo(Scalar.All((int)GrabCutClasses.BGD), wallLike);
            Cv2.GrabCut(source, grabMask, rectangle, backgroundModel, foregroundModel,
                Math.Clamp(iterations, 1, 10), GrabCutModes.InitWithMask);
        }

        var binaryMask = new Mat();
        using (var one = new Mat(grabMask.Size(), MatType.CV_8UC1, Scalar.All(1)))
            Cv2.BitwiseAnd(grabMask, one, binaryMask);
        binaryMask.ConvertTo(binaryMask, MatType.CV_8UC1, 255);

        using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
        Cv2.MorphologyEx(binaryMask, binaryMask, MorphTypes.Open, kernel);
        Cv2.MorphologyEx(binaryMask, binaryMask, MorphTypes.Close, kernel, iterations: 2);
        Cv2.GaussianBlur(binaryMask, binaryMask, new Size(5, 5), 0);

        var composite = resizedBackground.Clone();
        source.CopyTo(composite, binaryMask);
        grabMask.Dispose();
        return (composite, binaryMask);
    }

    private static Mat WallColorMask(Mat source, int marginX, int marginY, double tolerance = 30)
    {
        using var lab = new Mat();
        Cv2.CvtColor(source, lab, ColorConversionCodes.BGR2Lab);
        using var border = new Mat(source.Size(), MatType.CV_8UC1, Scalar.All(255));
        // Kişi genelde alt kenara kadar uzanır (omuz, kıyafet); duvar rengi üst ve yan şeritlerin
        // yalnızca üst yarısından öğrenilir.
        border[new Rect(marginX, marginY, source.Width - 2 * marginX, source.Height - marginY)]
            .SetTo(Scalar.All(0));
        border[new Rect(0, source.Height / 2, source.Width, source.Height - source.Height / 2)]
            .SetTo(Scalar.All(0));
        Scalar wall = Cv2.Mean(lab, border);

        using var diff = new Mat();
        Cv2.Absdiff(lab, wall, diff);
        Mat[] ch = Cv2.Split(diff);
        using var distance = new Mat();
        Cv2.Add(ch[0], ch[1], distance);
        Cv2.Add(distance, ch[2], distance);
        foreach (Mat c in ch) c.Dispose();

        var wallLike = new Mat();
        Cv2.Threshold(distance, wallLike, tolerance, 255, ThresholdTypes.BinaryInv);
        // Yalnızca ÜST kenara ve yan kenarların ÜST yarısına bağlı duvar bölgeleri alınır. Alt kenara değen
        // omuz/kol/el gibi açık tonlu ten bölgeleri duvara benzese bile arka plan sayılmaz.
        using var labels = new Mat();
        int n = Cv2.ConnectedComponents(wallLike, labels);
        var keep = new HashSet<int>();
        for (int x = 0; x < source.Width; x++) keep.Add(labels.At<int>(0, x));
        for (int y = 0; y < source.Height / 2; y++) { keep.Add(labels.At<int>(y, 0)); keep.Add(labels.At<int>(y, source.Width - 1)); }
        keep.Remove(0);
        var result = new Mat(source.Size(), MatType.CV_8UC1, Scalar.All(0));
        using var part = new Mat();
        foreach (int l in keep)
        {
            Cv2.Compare(labels, Scalar.All(l), part, CmpTypes.EQ);
            Cv2.BitwiseOr(result, part, result);
        }
        wallLike.Dispose();
        return result;
    }

    private static Mat ToBgr(Mat image)
    {
        var output = new Mat();
        Cv2.CvtColor(image, output, ColorConversionCodes.GRAY2BGR);
        return output;
    }
}
