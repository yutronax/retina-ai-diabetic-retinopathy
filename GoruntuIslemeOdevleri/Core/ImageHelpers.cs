using System.Drawing.Imaging;
using OpenCvSharp;

namespace GoruntuIslemeOdevleri.Core;

internal static class ImageHelpers
{
    public static Mat LoadColor(string path)
    {
        // Cv2.ImRead Türkçe karakterli yolları (ör. "Masaüstü") açamaz; baytlar üzerinden çözülür.
        Mat image = Cv2.ImDecode(File.ReadAllBytes(path), ImreadModes.Color);
        if (image.Empty())
            throw new InvalidOperationException("Görüntü okunamadı: " + path);
        return image;
    }

    public static Bitmap ToBitmap(Mat image)
    {
        if (image.Empty())
            throw new ArgumentException("Boş görüntü Bitmap'e dönüştürülemez.", nameof(image));

        Cv2.ImEncode(".png", image, out byte[] bytes);
        using var stream = new MemoryStream(bytes);
        using var temporary = new Bitmap(stream);
        return new Bitmap(temporary);
    }

    public static Mat FromBitmap(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return Cv2.ImDecode(stream.ToArray(), ImreadModes.Color);
    }

    public static Mat EnsureBgr(Mat input)
    {
        if (input.Channels() == 3)
            return input.Clone();

        var output = new Mat();
        Cv2.CvtColor(input, output, ColorConversionCodes.GRAY2BGR);
        return output;
    }

    public static Mat ResizeToFit(Mat image, int maxWidth = 1100, int maxHeight = 750)
    {
        double scale = Math.Min(1.0, Math.Min(maxWidth / (double)image.Width, maxHeight / (double)image.Height));
        if (Math.Abs(scale - 1.0) < 0.0001)
            return image.Clone();

        var resized = new Mat();
        Cv2.Resize(image, resized, new OpenCvSharp.Size(0, 0), scale, scale, InterpolationFlags.Area);
        return resized;
    }

    public static Mat MakeMontage(IEnumerable<(string Title, Mat Image)> items, int columns = 2, int cellWidth = 520, int cellHeight = 390)
    {
        var list = items.ToList();
        if (list.Count == 0)
            return new Mat(cellHeight, cellWidth, MatType.CV_8UC3, new Scalar(255, 255, 255));

        int rows = (int)Math.Ceiling(list.Count / (double)columns);
        // Başlık boyutu kare genişliğiyle ölçeklenir (520 px karede 13 px); büyük karede yazı okunur kalır.
        float titlePx = Math.Max(13f, cellWidth / 40f);
        int header = (int)(titlePx * 2.9f);
        var canvas = new Mat(rows * cellHeight, columns * cellWidth, MatType.CV_8UC3, new Scalar(245, 245, 245));

        for (int i = 0; i < list.Count; i++)
        {
            int x = (i % columns) * cellWidth;
            int y = (i / columns) * cellHeight;
            int imageTop = y + header;
            int availableHeight = cellHeight - header - 10;

            using Mat bgr = EnsureBgr(list[i].Image);
            double scale = Math.Min((cellWidth - 20) / (double)bgr.Width, availableHeight / (double)bgr.Height);
            int width = Math.Max(1, (int)Math.Round(bgr.Width * scale));
            int height = Math.Max(1, (int)Math.Round(bgr.Height * scale));
            using var resized = new Mat();
            Cv2.Resize(bgr, resized, new OpenCvSharp.Size(width, height), 0, 0, InterpolationFlags.Area);

            int left = x + (cellWidth - width) / 2;
            int top = imageTop + (availableHeight - height) / 2;
            using Mat roi = new(canvas, new Rect(left, top, width, height));
            resized.CopyTo(roi);
        }

        // Başlıklar GDI+ ile yazılır: OpenCV'nin Hershey fontu Türkçe karakterleri (Ö, ç, ş...) çizemez.
        using Bitmap bitmap = ToBitmap(canvas);
        using (Graphics g = Graphics.FromImage(bitmap))
        using (var font = new Font("Segoe UI", titlePx, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(Color.FromArgb(30, 30, 30)))
        {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            for (int i = 0; i < list.Count; i++)
                g.DrawString(list[i].Title, font, brush, (i % columns) * cellWidth + 12, (i / columns) * cellHeight + titlePx * 0.75f);
        }
        canvas.Dispose();
        return FromBitmap(bitmap);
    }

    public static string SelectImageFile(IWin32Window owner, string title = "Görüntü seç")
    {
        using var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Görüntü dosyaları|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|Tüm dosyalar|*.*"
        };
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.FileName : string.Empty;
    }
}
