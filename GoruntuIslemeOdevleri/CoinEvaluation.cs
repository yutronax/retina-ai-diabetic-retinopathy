using System.Text.Json;
using GoruntuIslemeOdevleri.Algorithms;
using GoruntuIslemeOdevleri.Core;
using OpenCvSharp;

namespace GoruntuIslemeOdevleri;

/// <summary>
/// Bozuk para algoritmasını etiketli Roboflow "Bozuk Paralar" veri setinde ölçer.
/// Kullanım: GoruntuIslemeOdevleri.exe --coineval &lt;veri seti kökü&gt; &lt;çıktı klasörü&gt;
/// Yalnızca 25 kr, 50 kr ve 1 TL içeren fotoğraflar alınır (5 ve 10 kuruş ödevin kapsamında değil).
/// </summary>
internal static class CoinEvaluation
{
    // Roboflow sınıf sırası: 0=1 lira, 1=10 kurus, 2=25 kurus, 3=5 kurus, 4=50 kurus
    private static readonly Dictionary<int, double> Values = new() { [0] = 1.00, [2] = 0.25, [4] = 0.50 };

    public static int Run(string datasetRoot, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var rows = new List<Row>();
        foreach (string labels in Directory.GetDirectories(datasetRoot).Select(d => Path.Combine(d, "labels")).Where(Directory.Exists))
        {
            string images = Path.Combine(Path.GetDirectoryName(labels)!, "images");
            foreach (string label in Directory.GetFiles(labels, "*.txt").OrderBy(f => f, StringComparer.Ordinal))
            {
                int[] classes = File.ReadAllLines(label).Where(l => l.Trim().Length > 0)
                    .Select(l => int.Parse(l.Split(' ')[0])).ToArray();
                if (classes.Length == 0 || classes.Any(c => !Values.ContainsKey(c))) continue;
                string image = Path.Combine(images, Path.GetFileNameWithoutExtension(label) + ".jpg");
                if (!File.Exists(image)) continue;

                using Mat src = Cv2.ImDecode(File.ReadAllBytes(image), ImreadModes.Color);
                using CoinAnalysis a = CoinAnalyzer.Analyze(src);
                var truth = (tl: classes.Count(c => c == 0), kr50: classes.Count(c => c == 4), kr25: classes.Count(c => c == 2));
                var found = (tl: a.Items.Count(x => x.Value >= 0.99), kr50: a.Items.Count(x => x.Value is >= 0.49 and < 0.99),
                             kr25: a.Items.Count(x => x.Value < 0.49));
                double trueTotal = classes.Sum(c => Values[c]);
                rows.Add(new Row(Path.GetFileName(image), classes.Length, a.Items.Count, trueTotal, a.TotalLira,
                    truth == found, Math.Abs(trueTotal - a.TotalLira) < 0.005));
                if (rows.Count <= 8)
                {
                    Cv2.ImEncode(".jpg", a.Annotated, out byte[] bytes);
                    File.WriteAllBytes(Path.Combine(outDir, $"coin_eval_{rows.Count}.jpg"), bytes);
                }
            }
        }

        int n = rows.Count;
        var summary = new
        {
            images = n,
            countCorrect = rows.Count(r => r.TrueCount == r.FoundCount),
            classesCorrect = rows.Count(r => r.ClassesExact),
            totalCorrect = rows.Count(r => r.TotalExact),
            coinsTrue = rows.Sum(r => r.TrueCount),
            coinsFound = rows.Sum(r => r.FoundCount),
            meanAbsTotalError = n == 0 ? 0 : rows.Average(r => Math.Abs(r.TrueTotal - r.FoundTotal)),
            rows
        };
        File.WriteAllText(Path.Combine(outDir, "coin_eval.json"),
            JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    /// <summary>
    /// Sentetik örnek: cevap biliniyor (2×25 kr, 2×50 kr, 3×1 TL = 4,50 TL; 4 yazı, 3 tura).
    /// Referanslı ve referanssız iki çalıştırmanın sonucunu ve görüntülerini yazar.
    /// </summary>
    public static int RunSynthetic(string outDir)
    {
        Directory.CreateDirectory(outDir);
        using Mat coins = SyntheticSamples.Coins();
        using Mat heads = SyntheticSamples.CoinReference(true);
        using Mat tails = SyntheticSamples.CoinReference(false);
        var lines = new List<string>();
        foreach ((string name, Mat? h, Mat? t) in new[] { ("referansli", heads, tails), ("referanssiz", (Mat?)null, (Mat?)null) })
        {
            using CoinAnalysis a = CoinAnalyzer.Analyze(coins, h, t);
            lines.Add($"[{name}] adet={a.Items.Count} toplam={a.TotalLira:0.00} TL yazi={a.HeadsCount} tura={a.TailsCount}");
            lines.AddRange(a.Items.Select(x => $"   {x.Value:0.00} TL  {x.Face}  cap={x.DiameterPixels:0.0}px  ({x.Center.X:0},{x.Center.Y:0})"));
            Cv2.ImEncode(".png", a.Annotated, out byte[] bytes);
            File.WriteAllBytes(Path.Combine(outDir, $"sentetik_{name}.png"), bytes);
            Cv2.ImEncode(".png", a.Mask, out bytes);
            File.WriteAllBytes(Path.Combine(outDir, $"sentetik_{name}_bolgeler.png"), bytes);
        }
        File.WriteAllLines(Path.Combine(outDir, "sentetik.txt"), lines);
        return 0;
    }

    private sealed record Row(string File, int TrueCount, int FoundCount, double TrueTotal, double FoundTotal,
        bool ClassesExact, bool TotalExact);
}
