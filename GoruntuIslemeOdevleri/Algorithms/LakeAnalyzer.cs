using GoruntuIslemeOdevleri.Core;
using OpenCvSharp;

namespace GoruntuIslemeOdevleri.Algorithms;

internal static class LakeAnalyzer
{
    public static LakeAnalysis Analyze(Mat source, double realScaleMeters, double scalePixels)
    {
        if (realScaleMeters <= 0 || scalePixels <= 0)
            throw new ArgumentOutOfRangeException(nameof(realScaleMeters), "Ölçek uzunlukları sıfırdan büyük olmalıdır.");

        ThresholdCandidate best = AutomaticThresholding.Best(source);
        Mat mask = best.Mask;
        using Mat contourInput = mask.Clone();
        Cv2.FindContours(contourInput, out Point[][] contours, out _, RetrievalModes.External,
            ContourApproximationModes.ApproxNone);
        Point[]? lakeContour = contours.OrderByDescending(c => Cv2.ContourArea(c)).FirstOrDefault();
        if (lakeContour is null)
        {
            mask.Dispose();
            throw new InvalidOperationException("Göl/arazi alanı tespit edilemedi.");
        }

        double metersPerPixel = realScaleMeters / scalePixels;
        double pixelArea = Cv2.ContourArea(lakeContour);
        double pixelPerimeter = Cv2.ArcLength(lakeContour, true);
        double areaSquareMeters = pixelArea * metersPerPixel * metersPerPixel;
        double perimeterMeters = pixelPerimeter * metersPerPixel;

        Moments moments = Cv2.Moments(lakeContour);
        var centroid = Math.Abs(moments.M00) < 0.0001
            ? new Point2d(source.Width / 2.0, source.Height / 2.0)
            : new Point2d(moments.M10 / moments.M00, moments.M01 / moments.M00);

        using Mat bgr = ImageHelpers.EnsureBgr(source);
        var annotated = bgr.Clone();
        Cv2.DrawContours(annotated, [lakeContour], -1, new Scalar(0, 0, 255), 5, LineTypes.AntiAlias);
        Cv2.Circle(annotated, new Point((int)centroid.X, (int)centroid.Y), 8,
            new Scalar(0, 255, 255), -1, LineTypes.AntiAlias);
        Cv2.PutText(annotated, $"Alan: {areaSquareMeters / 1_000_000.0:0.00} km2",
            new Point(18, 38), HersheyFonts.HersheySimplex, 0.82, new Scalar(20, 20, 20), 3,
            LineTypes.AntiAlias);
        Cv2.PutText(annotated, $"Cevre: {perimeterMeters / 1000.0:0.00} km",
            new Point(18, 76), HersheyFonts.HersheySimplex, 0.82, new Scalar(20, 20, 20), 3,
            LineTypes.AntiAlias);

        return new LakeAnalysis(annotated, mask, best.Name + (best.Inverted ? " (ters)" : ""),
            best.Threshold, areaSquareMeters, perimeterMeters, centroid);
    }
}
