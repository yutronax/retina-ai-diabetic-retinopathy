using GoruntuIslemeOdevleri.Core;
using OpenCvSharp;

namespace GoruntuIslemeOdevleri.Algorithms;

internal static class AutomaticThresholding
{
    public static IReadOnlyList<ThresholdCandidate> Evaluate(Mat source)
    {
        if (source.Empty())
            throw new ArgumentException("Eşikleme için görüntü boş olamaz.", nameof(source));

        using Mat bgr = ImageHelpers.EnsureBgr(source);
        var channels = BuildChannels(bgr);
        var candidates = new List<ThresholdCandidate>();

        foreach ((string name, Mat channel) in channels)
        {
            using (channel)
            using (var blurred = new Mat())
            {
                Cv2.GaussianBlur(channel, blurred, new Size(5, 5), 0);
                foreach (bool inverted in new[] { false, true })
                {
                    var binary = new Mat();
                    ThresholdTypes type = (inverted ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary) |
                                          ThresholdTypes.Otsu;
                    double threshold = Cv2.Threshold(blurred, binary, 0, 255, type);

                    using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
                    Cv2.MorphologyEx(binary, binary, MorphTypes.Open, kernel);
                    Cv2.MorphologyEx(binary, binary, MorphTypes.Close, kernel, iterations: 2);

                    (double score, double areaRatio, double centerDistance, Mat selectedMask) =
                        ScoreAndSelect(binary);
                    binary.Dispose();
                    candidates.Add(new ThresholdCandidate(name, threshold, inverted, score,
                        areaRatio, centerDistance, selectedMask));
                }
            }
        }

        candidates.AddRange(ThreeChannelCandidates(bgr));
        return candidates.OrderByDescending(x => x.Score).ToList();
    }

    public static ThresholdCandidate Best(Mat source)
    {
        IReadOnlyList<ThresholdCandidate> candidates = Evaluate(source);
        if (candidates.Count == 0)
            throw new InvalidOperationException("Geçerli bir eşikleme adayı üretilemedi.");

        ThresholdCandidate best = candidates[0];
        foreach (ThresholdCandidate candidate in candidates.Skip(1))
            candidate.Mask.Dispose();
        return best;
    }

    /// <summary>
    /// Seçilmiş adayın kanal/eşik/kutup bilgisiyle, tek bölgeye indirgenmemiş TAM ikili maskeyi yeniden üretir
    /// (göl ödevinde tüm bölgeleri renklendirip içlerinden doğru olanı seçmek için).
    /// </summary>
    public static Mat FullBinary(Mat source, string channelName, double threshold, bool inverted,
        List<(string Title, Mat Image)>? steps = null)
    {
        using Mat bgr = ImageHelpers.EnsureBgr(source);
        var channels = BuildChannels(bgr);
        Mat? chosen = null;
        foreach ((string name, Mat channel) in channels)
        {
            if (name == channelName) chosen = channel;
            else channel.Dispose();
        }
        if (chosen is null)
            throw new ArgumentException("Bilinmeyen kanal: " + channelName, nameof(channelName));

        using (chosen)
        using (var blurred = new Mat())
        {
            Cv2.GaussianBlur(chosen, blurred, new Size(5, 5), 0);
            var binary = new Mat();
            Cv2.Threshold(blurred, binary, threshold, 255, inverted ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary);
            steps?.Add(($"1. Tek kanal: {channelName}", chosen.Clone()));
            steps?.Add(($"2. Eşikleme (T={threshold:0}{(inverted ? ", ters" : "")})", binary.Clone()));
            using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
            Cv2.MorphologyEx(binary, binary, MorphTypes.Open, kernel);
            Cv2.MorphologyEx(binary, binary, MorphTypes.Close, kernel, iterations: 2);
            steps?.Add(("3. Gürültü temizliği (açma + kapama)", binary.Clone()));
            return binary;
        }
    }

    /// <summary>
    /// PDF'teki öneri: tek kanal yerine R, G, B kanallarının her birine ayrı Otsu eşiği uygulayıp
    /// üç koşulu birlikte (VE) sağlayan pikselleri almak. Koyu ve açık hedef için iki aday döner.
    /// </summary>
    private static IEnumerable<ThresholdCandidate> ThreeChannelCandidates(Mat bgr)
    {
        Mat[] ch = Cv2.Split(bgr);
        try
        {
            foreach (bool inverted in new[] { true, false })
            {
                var combined = new Mat(bgr.Size(), MatType.CV_8UC1, Scalar.All(255));
                var thresholds = new double[3];
                for (int i = 0; i < 3; i++)
                {
                    using var blurred = new Mat();
                    using var binary = new Mat();
                    Cv2.GaussianBlur(ch[i], blurred, new Size(5, 5), 0);
                    thresholds[i] = Cv2.Threshold(blurred, binary, 0, 255,
                        (inverted ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary) | ThresholdTypes.Otsu);
                    Cv2.BitwiseAnd(combined, binary, combined);
                }
                using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
                Cv2.MorphologyEx(combined, combined, MorphTypes.Open, kernel);
                Cv2.MorphologyEx(combined, combined, MorphTypes.Close, kernel, iterations: 2);
                (double score, double areaRatio, double centerDistance, Mat selected) = ScoreAndSelect(combined);
                combined.Dispose();
                string name = $"RGB 3 eşik (B<{thresholds[0]:0}, G<{thresholds[1]:0}, R<{thresholds[2]:0})";
                if (!inverted) name = name.Replace('<', '>');
                yield return new ThresholdCandidate(name, thresholds.Average(), inverted, score, areaRatio,
                    centerDistance, selected);
            }
        }
        finally
        {
            foreach (Mat m in ch) m.Dispose();
        }
    }

    private static List<(string Name, Mat Channel)> BuildChannels(Mat bgr)
    {
        var result = new List<(string, Mat)>();

        var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        result.Add(("Gri", gray));

        Mat[] bgrChannels = Cv2.Split(bgr);
        result.Add(("Mavi (B)", bgrChannels[0]));
        result.Add(("Yeşil (G)", bgrChannels[1]));
        result.Add(("Kırmızı (R)", bgrChannels[2]));

        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);
        Mat[] hsvChannels = Cv2.Split(hsv);
        result.Add(("HSV-H", hsvChannels[0]));
        result.Add(("HSV-S", hsvChannels[1]));
        result.Add(("HSV-V", hsvChannels[2]));
        return result;
    }

    private static (double Score, double AreaRatio, double CenterDistance, Mat Mask) ScoreAndSelect(Mat binary)
    {
        using Mat contourInput = binary.Clone();
        Cv2.FindContours(contourInput, out Point[][] contours, out _, RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        double imageArea = binary.Width * (double)binary.Height;
        var imageCenter = new Point2d(binary.Width / 2.0, binary.Height / 2.0);
        double diagonal = Math.Sqrt(binary.Width * binary.Width + binary.Height * binary.Height);

        double bestScore = double.NegativeInfinity;
        double bestAreaRatio = 0;
        double bestDistance = 1;
        Point[]? bestContour = null;

        foreach (Point[] contour in contours)
        {
            double area = Cv2.ContourArea(contour);
            double ratio = area / imageArea;
            if (ratio < 0.003 || ratio > 0.82)
                continue;

            Moments moments = Cv2.Moments(contour);
            if (Math.Abs(moments.M00) < 0.0001)
                continue;

            var centroid = new Point2d(moments.M10 / moments.M00, moments.M01 / moments.M00);
            double centerDistance = Math.Sqrt(Math.Pow(centroid.X - imageCenter.X, 2) +
                                              Math.Pow(centroid.Y - imageCenter.Y, 2)) / (diagonal / 2.0);
            Rect bounds = Cv2.BoundingRect(contour);
            bool touchesBorder = bounds.Left <= 1 || bounds.Top <= 1 ||
                                 bounds.Right >= binary.Width - 1 || bounds.Bottom >= binary.Height - 1;
            double perimeter = Math.Max(1, Cv2.ArcLength(contour, true));
            double compactness = Math.Clamp(4 * Math.PI * area / (perimeter * perimeter), 0, 1);
            double usefulArea = 1.0 - Math.Min(1.0, Math.Abs(ratio - 0.22) / 0.55);

            double score = 2.4 * (1 - Math.Min(centerDistance, 1)) +
                           1.1 * usefulArea + 0.45 * compactness - (touchesBorder ? 1.8 : 0);
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestAreaRatio = ratio;
            bestDistance = centerDistance;
            bestContour = contour;
        }

        var selected = Mat.Zeros(binary.Size(), MatType.CV_8UC1).ToMat();
        if (bestContour is not null)
            Cv2.DrawContours(selected, [bestContour], -1, Scalar.All(255), -1, LineTypes.AntiAlias);
        else
            bestScore = -100;

        return (bestScore, bestAreaRatio, bestDistance, selected);
    }
}
