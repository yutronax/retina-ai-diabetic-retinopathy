using GoruntuIslemeOdevleri.Core;
using OpenCvSharp;

namespace GoruntuIslemeOdevleri.Algorithms;

internal enum KernelChoice { Square9, Cross5 }
internal enum MorphologyChoice { Erode, Dilate, Open, Close }

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
        using Mat kernel = CreateKernel(kernelChoice);
        var output = new Mat();

        if (source.Channels() == 3 && (rMin > 0 || rMax < 255 || gMin > 0 || gMax < 255 || bMin > 0 || bMax < 255))
        {
            using var mask = new Mat();
            Cv2.InRange(source, new Scalar(bMin, gMin, rMin), new Scalar(bMax, gMax, rMax), mask);
            ApplyMorphology(mask, output, operation, kernel, iterations);
            return output;
        }

        ApplyMorphology(source, output, operation, kernel, iterations);
        return output;
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
        }
    }

    private static double ComputePsnr(Mat reference, Mat candidate)
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
