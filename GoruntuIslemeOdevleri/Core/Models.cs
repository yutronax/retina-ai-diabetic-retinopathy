using OpenCvSharp;

namespace GoruntuIslemeOdevleri.Core;

internal sealed record ThresholdCandidate(
    string Name,
    double Threshold,
    bool Inverted,
    double Score,
    double AreaRatio,
    double CenterDistance,
    Mat Mask);

internal sealed record NutItem(string Kind, double Area, double DiameterPixels, Point2f Center);
internal sealed record NutAnalysis(Mat Annotated, Mat Mask, IReadOnlyList<NutItem> Items, double SplitDiameter) : IDisposable
{
    public int HazelnutCount => Items.Count(x => x.Kind == "Fındık");
    public int WalnutCount => Items.Count(x => x.Kind == "Ceviz");
    /// <summary>Birden çok nesnenin birleştiği, ayrılamayan bölgeler.</summary>
    public int UnseparatedCount => Items.Count(x => x.Kind == "Ayrılamadı");

    public void Dispose()
    {
        Annotated.Dispose();
        Mask.Dispose();
    }
}

internal sealed record NoiseComparison(Mat Noisy, Mat SquareResult, Mat CrossResult, Mat MedianResult,
    double SquarePsnr, double CrossPsnr, double MedianPsnr) : IDisposable
{
    public void Dispose()
    {
        Noisy.Dispose();
        SquareResult.Dispose();
        CrossResult.Dispose();
        MedianResult.Dispose();
    }
}

internal sealed record CoinItem(double Value, string Face, double DiameterPixels, Point2f Center, double FaceConfidence);
internal sealed record CoinAnalysis(Mat Annotated, Mat Mask, IReadOnlyList<CoinItem> Items) : IDisposable
{
    public double TotalLira => Items.Sum(x => x.Value);
    public int HeadsCount => Items.Count(x => x.Face.StartsWith("Yazı", StringComparison.OrdinalIgnoreCase));
    public int TailsCount => Items.Count(x => x.Face.StartsWith("Tura", StringComparison.OrdinalIgnoreCase));

    public void Dispose()
    {
        Annotated.Dispose();
        Mask.Dispose();
    }
}

internal sealed record LakeAnalysis(Mat Annotated, Mat Mask, string ThresholdMethod, double Threshold,
    double AreaSquareMeters, double PerimeterMeters, Point2d Centroid) : IDisposable
{
    /// <summary>Eşik sonrası tüm bölgeler, her biri farklı renkte.</summary>
    public Mat? ColoredRegions { get; init; }
    public int RegionCount { get; init; }
    /// <summary>Karşılaştırma için kontur uzunluğundan hesaplanan çevre.</summary>
    public double ContourPerimeterMeters { get; init; }
    public double CenterDistancePixels { get; init; }

    public void Dispose()
    {
        Annotated.Dispose();
        Mask.Dispose();
        ColoredRegions?.Dispose();
    }
}
