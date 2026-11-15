using GoruntuIslemeOdevleri.Core;
using OpenCvSharp;

namespace GoruntuIslemeOdevleri.Algorithms;

internal static class CoinAnalyzer
{
    private static readonly (double Millimeters, double Lira, string Label)[] TurkishCoinSizes =
    [
        (20.50, 0.25, "25 kr"),
        (23.85, 0.50, "50 kr"),
        (26.15, 1.00, "1 TL")
    ];

    public static CoinAnalysis Analyze(Mat source, Mat? headsReference = null, Mat? tailsReference = null)
    {
        using Mat bgr = ImageHelpers.EnsureBgr(source);
        using var gray = new Mat();
        using var blurred = new Mat();
        var mask = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(gray, blurred, new Size(7, 7), 0);
        Cv2.Threshold(blurred, mask, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
        using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(7, 7));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel, iterations: 2);

        using Mat contourInput = mask.Clone();
        Cv2.FindContours(contourInput, out Point[][] contours, out _, RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        double minArea = bgr.Width * bgr.Height * 0.004;
        var detected = new List<(Point[] Contour, Point2f Center, double Diameter, double Circularity)>();
        foreach (Point[] contour in contours)
        {
            double area = Cv2.ContourArea(contour);
            if (area < minArea)
                continue;

            double perimeter = Math.Max(1, Cv2.ArcLength(contour, true));
            double circularity = 4 * Math.PI * area / (perimeter * perimeter);
            if (circularity < 0.68)
                continue;

            Cv2.MinEnclosingCircle(contour, out Point2f center, out float radius);
            detected.Add((contour, center, radius * 2.0, circularity));
        }

        double largestDiameter = detected.Count == 0 ? 1 : detected.Max(x => x.Diameter);
        double pixelsPerMillimeter = largestDiameter / TurkishCoinSizes.Max(x => x.Millimeters);

        using Mat? headsTemplate = headsReference is null ? null : ExtractLargestCoinPatch(headsReference);
        using Mat? tailsTemplate = tailsReference is null ? null : ExtractLargestCoinPatch(tailsReference);

        var annotated = bgr.Clone();
        var items = new List<CoinItem>();
        int number = 1;
        foreach (var coin in detected.OrderBy(x => x.Center.Y).ThenBy(x => x.Center.X))
        {
            double measuredMillimeters = coin.Diameter / Math.Max(0.0001, pixelsPerMillimeter);
            var denomination = TurkishCoinSizes.MinBy(x => Math.Abs(x.Millimeters - measuredMillimeters));

            using Mat patch = ExtractCoinPatch(gray, coin.Contour);
            (string face, double confidence) = ClassifyFace(patch, headsTemplate, tailsTemplate);
            items.Add(new CoinItem(denomination.Lira, face, coin.Diameter, coin.Center, confidence));

            Scalar color = denomination.Lira switch
            {
                >= 0.99 => new Scalar(30, 80, 230),
                >= 0.49 => new Scalar(20, 170, 220),
                _ => new Scalar(40, 190, 70)
            };
            Cv2.DrawContours(annotated, [coin.Contour], -1, color, 4, LineTypes.AntiAlias);
            string faceShort = face.StartsWith("Yazı") ? "Y" : face.StartsWith("Tura") ? "T" : "?";
            Cv2.PutText(annotated, $"{number}: {denomination.Label} {faceShort}",
                new Point((int)coin.Center.X - 70, (int)coin.Center.Y), HersheyFonts.HersheySimplex,
                0.62, color, 2, LineTypes.AntiAlias);
            number++;
        }

        Cv2.PutText(annotated, $"Toplam: {items.Sum(x => x.Value):0.00} TL",
            new Point(18, 38), HersheyFonts.HersheySimplex, 0.9, new Scalar(25, 25, 25), 3,
            LineTypes.AntiAlias);
        return new CoinAnalysis(annotated, mask, items);
    }

    private static (string Face, double Confidence) ClassifyFace(Mat patch, Mat? heads, Mat? tails)
    {
        if (heads is null || tails is null || heads.Empty() || tails.Empty())
            return ("Belirsiz (referans yok)", 0);

        double headsScore = BestRotationScore(patch, heads);
        double tailsScore = BestRotationScore(patch, tails);
        double margin = Math.Abs(headsScore - tailsScore);
        string face = headsScore >= tailsScore ? "Yazı" : "Tura";
        return (face, margin);
    }

    private static double BestRotationScore(Mat patch, Mat template)
    {
        double best = -1;
        var center = new Point2f(template.Width / 2f, template.Height / 2f);
        for (int angle = 0; angle < 360; angle += 15)
        {
            using Mat rotation = Cv2.GetRotationMatrix2D(center, angle, 1.0);
            using var rotated = new Mat();
            Cv2.WarpAffine(template, rotated, rotation, template.Size(), InterpolationFlags.Linear,
                BorderTypes.Constant, Scalar.All(255));
            using var response = new Mat();
            Cv2.MatchTemplate(patch, rotated, response, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(response, out _, out double maxValue, out _, out _);
            best = Math.Max(best, maxValue);
        }
        return best;
    }

    private static Mat ExtractLargestCoinPatch(Mat image)
    {
        using Mat bgr = ImageHelpers.EnsureBgr(image);
        using var gray = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Threshold(gray, mask, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
        using Mat contourInput = mask.Clone();
        Cv2.FindContours(contourInput, out Point[][] contours, out _, RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);
        Point[]? largest = contours.OrderByDescending(c => Cv2.ContourArea(c)).FirstOrDefault();
        return largest is null ? NormalizePatch(gray) : ExtractCoinPatch(gray, largest);
    }

    private static Mat ExtractCoinPatch(Mat gray, Point[] contour)
    {
        Rect bounds = Cv2.BoundingRect(contour);
        int margin = Math.Max(4, Math.Min(bounds.Width, bounds.Height) / 10);
        int left = Math.Max(0, bounds.Left - margin);
        int top = Math.Max(0, bounds.Top - margin);
        int right = Math.Min(gray.Width, bounds.Right + margin);
        int bottom = Math.Min(gray.Height, bounds.Bottom + margin);
        using var roi = new Mat(gray, new Rect(left, top, right - left, bottom - top));
        return NormalizePatch(roi);
    }

    private static Mat NormalizePatch(Mat input)
    {
        var resized = new Mat();
        Cv2.Resize(input, resized, new Size(160, 160), 0, 0, InterpolationFlags.Area);
        Cv2.EqualizeHist(resized, resized);
        return resized;
    }
}
