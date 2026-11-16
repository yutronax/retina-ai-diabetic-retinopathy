using OpenCvSharp;

namespace GoruntuIslemeOdevleri.Core;

internal static class SyntheticSamples
{
    public static Mat Nuts(int seed = 1, int hazelnuts = 7, int walnuts = 4)
    {
        var random = new Random(seed);
        var image = new Mat(720, 1000, MatType.CV_8UC3, new Scalar(244, 244, 244));
        Cv2.Rectangle(image, new Rect(18, 18, 964, 684), new Scalar(225, 225, 225), 4);

        var occupied = new List<(Point Center, int Radius)>();
        void DrawNut(bool walnut)
        {
            int radius = walnut ? random.Next(47, 58) : random.Next(26, 35);
            Point center;
            int guard = 0;
            do
            {
                center = new Point(random.Next(radius + 35, image.Width - radius - 35),
                    random.Next(radius + 35, image.Height - radius - 35));
                guard++;
            } while (occupied.Any(o => Distance(o.Center, center) < o.Radius + radius + 22) && guard < 500);

            occupied.Add((center, radius));
            // Gerçek renklere yakın (BGR): ceviz sarımsı bej, fındık kırmızımsı kahve.
            Scalar color = walnut
                ? new Scalar(random.Next(95, 120), random.Next(140, 165), random.Next(175, 200))
                : new Scalar(random.Next(30, 50), random.Next(70, 90), random.Next(150, 175));
            Cv2.Ellipse(image, center, new Size(radius, (int)(radius * random.NextDouble() * 0.18 + radius * 0.86)),
                random.Next(-25, 25), 0, 360, color, -1, LineTypes.AntiAlias);
            Cv2.Ellipse(image, center, new Size(radius, radius), 0, 0, 360, new Scalar(75, 95, 115), 2, LineTypes.AntiAlias);
            Cv2.Line(image, new Point(center.X - radius / 2, center.Y), new Point(center.X + radius / 2, center.Y),
                new Scalar(70, 95, 125), 2, LineTypes.AntiAlias);
        }

        for (int i = 0; i < hazelnuts; i++) DrawNut(false);
        for (int i = 0; i < walnuts; i++) DrawNut(true);

        for (int i = 0; i < 45; i++)
        {
            var p = new Point(random.Next(20, 980), random.Next(20, 700));
            Cv2.Circle(image, p, random.Next(1, 4), new Scalar(190, 190, 190), -1);
        }
        return image;
    }

    public static (Mat Foreground, Mat Background) ForegroundScene()
    {
        var foreground = new Mat(720, 920, MatType.CV_8UC3, new Scalar(238, 232, 220));
        Cv2.Circle(foreground, new Point(460, 215), 88, new Scalar(120, 150, 190), -1, LineTypes.AntiAlias);
        Cv2.Ellipse(foreground, new Point(460, 500), new Size(160, 245), 0, 0, 360,
            new Scalar(95, 120, 175), -1, LineTypes.AntiAlias);
        Cv2.Rectangle(foreground, new Rect(365, 270, 190, 60), new Scalar(70, 90, 140), -1);

        var background = new Mat(720, 920, MatType.CV_8UC3, new Scalar(230, 190, 110));
        Cv2.Rectangle(background, new Rect(0, 420, 920, 300), new Scalar(85, 155, 85), -1);
        Cv2.Circle(background, new Point(740, 130), 58, new Scalar(80, 220, 250), -1, LineTypes.AntiAlias);
        Point[] mountain = [new(0, 500), new(210, 220), new(410, 500), new(620, 250), new(920, 520)];
        Cv2.FillPoly(background, [mountain], new Scalar(105, 105, 125), LineTypes.AntiAlias);
        return (foreground, background);
    }

    public static Mat NoiseBase()
    {
        var image = new Mat(680, 920, MatType.CV_8UC3, new Scalar(230, 235, 240));
        Cv2.Rectangle(image, new Rect(90, 90, 280, 210), new Scalar(80, 175, 230), -1, LineTypes.AntiAlias);
        Cv2.Circle(image, new Point(650, 200), 125, new Scalar(190, 90, 80), -1, LineTypes.AntiAlias);
        Cv2.Ellipse(image, new Point(470, 500), new Size(260, 105), -8, 0, 360,
            new Scalar(85, 185, 105), -1, LineTypes.AntiAlias);
        Cv2.PutText(image, "GORUNTU ISLEME", new Point(165, 390), HersheyFonts.HersheySimplex,
            1.45, new Scalar(50, 50, 50), 3, LineTypes.AntiAlias);
        return image;
    }

    /// <summary>
    /// Kenar açıklığı deneyi: 2 piksel kalınlığında çizgiler; her satırda 2, 4, 6 ve 8 piksellik açıklıklar.
    /// Üst satır yatay, orta satır dikey, alt satır çapraz (45°) çizgiler. 3x3 kapama her turda yaklaşık
    /// 2 piksellik açıklık kapatır; kare şablon çapraz açıklıklarda artı şablondan belirgin üstündür.
    /// </summary>
    public static Mat BrokenEdges()
    {
        var image = new Mat(600, 900, MatType.CV_8UC1, Scalar.Black);
        int[] gaps = [2, 4, 6, 8];
        for (int i = 0; i < gaps.Length; i++)
        {
            int g = gaps[i];
            int x0 = 60 + i * 210;

            // Yatay çizgi, ortasında g piksellik açıklık.
            int mid = x0 + 70;
            Cv2.Line(image, new Point(x0, 110), new Point(mid - 1, 110), Scalar.All(255), 2);
            Cv2.Line(image, new Point(mid + g, 110), new Point(x0 + 150, 110), Scalar.All(255), 2);

            // Dikey çizgi.
            int vx = x0 + 70, vmid = 280;
            Cv2.Line(image, new Point(vx, 190), new Point(vx, vmid - 1), Scalar.All(255), 2);
            Cv2.Line(image, new Point(vx, vmid + g), new Point(vx, 370), Scalar.All(255), 2);

            // Çapraz (45°) çizgi; açıklık çizgi boyunca g piksel.
            int d = (int)Math.Round(g / Math.Sqrt(2));
            var start = new Point(x0 + 10, 570);
            var cut = new Point(x0 + 70, 510);
            Cv2.Line(image, start, cut, Scalar.All(255), 2);
            Cv2.Line(image, new Point(cut.X + Math.Max(1, d), cut.Y - Math.Max(1, d)), new Point(x0 + 140, 440), Scalar.All(255), 2);
        }
        return image;
    }

    /// <summary>BrokenEdges çizimindeki her açıklığın bulunduğu hücre: (yön, açıklık px, hücre dikdörtgeni).</summary>
    public static IEnumerable<(string Direction, int Gap, Rect Cell)> BrokenEdgeCells()
    {
        int[] gaps = [2, 4, 6, 8];
        for (int i = 0; i < gaps.Length; i++)
        {
            int x0 = 60 + i * 210;
            yield return ("Yatay", gaps[i], new Rect(x0 - 5, 90, 165, 40));
            yield return ("Dikey", gaps[i], new Rect(x0 + 50, 180, 40, 200));
            yield return ("Çapraz", gaps[i], new Rect(x0, 430, 150, 150));
        }
    }

    public static Mat Coins()
    {
        var image = new Mat(760, 1080, MatType.CV_8UC3, new Scalar(248, 248, 248));
        DrawCoin(image, new Point(155, 180), 67, "Y", new Scalar(105, 150, 185));
        DrawCoin(image, new Point(375, 185), 78, "T", new Scalar(80, 150, 195));
        DrawCoin(image, new Point(635, 180), 90, "Y", new Scalar(70, 160, 205));
        DrawCoin(image, new Point(905, 185), 67, "T", new Scalar(105, 150, 185));
        DrawCoin(image, new Point(245, 520), 78, "Y", new Scalar(80, 150, 195));
        DrawCoin(image, new Point(540, 510), 90, "T", new Scalar(70, 160, 205));
        DrawCoin(image, new Point(845, 515), 90, "Y", new Scalar(70, 160, 205));
        return image;
    }

    public static Mat CoinReference(bool heads)
    {
        var image = new Mat(260, 260, MatType.CV_8UC3, new Scalar(248, 248, 248));
        DrawCoin(image, new Point(130, 130), 105, heads ? "Y" : "T", new Scalar(70, 160, 205));
        return image;
    }

    public static Mat Lake()
    {
        var image = new Mat(760, 1100, MatType.CV_8UC3, new Scalar(95, 155, 95));
        var random = new Random(9);
        for (int i = 0; i < 180; i++)
        {
            var p = new Point(random.Next(image.Width), random.Next(image.Height));
            Cv2.Circle(image, p, random.Next(4, 20), new Scalar(90 + random.Next(-15, 16), 145 + random.Next(-15, 16), 90), -1);
        }

        Point[] lake =
        [
            new(265, 190), new(415, 120), new(610, 145), new(780, 245), new(845, 370),
            new(775, 520), new(590, 625), new(410, 580), new(250, 470), new(190, 325)
        ];
        Cv2.FillPoly(image, [lake], new Scalar(205, 145, 70), LineTypes.AntiAlias);
        Cv2.Polylines(image, [lake], true, new Scalar(225, 170, 90), 5, LineTypes.AntiAlias);
        Cv2.Line(image, new Point(850, 690), new Point(1000, 690), new Scalar(0, 235, 255), 8, LineTypes.AntiAlias);
        Cv2.PutText(image, "50 km", new Point(865, 660), HersheyFonts.HersheySimplex, 0.8,
            new Scalar(0, 235, 255), 2, LineTypes.AntiAlias);
        return image;
    }

    private static void DrawCoin(Mat image, Point center, int radius, string face, Scalar color)
    {
        Cv2.Circle(image, center, radius, color, -1, LineTypes.AntiAlias);
        Cv2.Circle(image, center, radius, new Scalar(75, 85, 95), 4, LineTypes.AntiAlias);
        Cv2.Circle(image, center, (int)(radius * 0.72), new Scalar(105, 125, 145), 2, LineTypes.AntiAlias);
        double fontScale = radius / 48.0;
        int baseline;
        Size textSize = Cv2.GetTextSize(face, HersheyFonts.HersheyDuplex, fontScale, 4, out baseline);
        var origin = new Point(center.X - textSize.Width / 2, center.Y + textSize.Height / 2);
        Cv2.PutText(image, face, origin, HersheyFonts.HersheyDuplex, fontScale,
            new Scalar(45, 55, 65), 4, LineTypes.AntiAlias);
    }

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
