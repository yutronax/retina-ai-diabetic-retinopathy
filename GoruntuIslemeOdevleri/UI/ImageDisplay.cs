using System.Drawing.Drawing2D;
using GoruntuIslemeOdevleri.Core;
using CvMat = OpenCvSharp.Mat;
using CvPoint = OpenCvSharp.Point;

namespace GoruntuIslemeOdevleri.UI;

/// <summary>
/// Başlıklı görüntü paneli. Yakınlaştırma: fare tekerleği (imlecin olduğu yere), +/− düğmeleri;
/// kaydırma: sol ya da orta tuşla sürükle; çift tık ya da "Sığdır": tamamını göster.
/// Sürüklemeden yapılan tek tıklama, görüntüdeki piksel koordinatıyla <see cref="ImagePixelClicked"/> olayını verir.
/// </summary>
internal sealed class ImageDisplay : UserControl
{
    private readonly Label _title;
    private readonly Label _zoomLabel;
    private readonly ZoomCanvas _canvas;
    private string _titleText;

    public event EventHandler<CvPoint>? ImagePixelClicked;

    public ImageDisplay(string title)
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Page;
        Padding = new Padding(8, 4, 8, 8);
        _titleText = title;

        var header = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Theme.Page };
        _title = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, AutoSize = true, WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 3, 0, 0)
        };
        _zoomLabel = new Label
        {
            AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.25f),
            Margin = new Padding(0, 5, 6, 0)
        };
        buttons.Controls.Add(_zoomLabel);
        buttons.Controls.Add(SmallButton("−", "Uzaklaştır", () => _canvas!.ZoomBy(1 / 1.25)));
        buttons.Controls.Add(SmallButton("+", "Yakınlaştır", () => _canvas!.ZoomBy(1.25)));
        buttons.Controls.Add(SmallButton("Sığdır", "Tamamını göster (çift tık)", () => _canvas!.Fit()));
        header.Controls.Add(_title);
        header.Controls.Add(buttons);

        _canvas = new ZoomCanvas { Dock = DockStyle.Fill };
        _canvas.PixelClicked += p => ImagePixelClicked?.Invoke(this, p);
        _canvas.ZoomChanged += UpdateZoomLabel;

        Controls.Add(_canvas);
        Controls.Add(header);
        SetTitle(title);
        UpdateZoomLabel();
    }

    public void SetTitle(string title)
    {
        _titleText = title;
        _title.Text = title.ToUpper(new System.Globalization.CultureInfo("tr-TR"));
    }

    public void SetMat(CvMat image)
    {
        _canvas.SetImage(ImageHelpers.ToBitmap(image));
        AppStatus.Image($"{image.Width} × {image.Height} px · {image.Channels()} kanal");
    }

    private void UpdateZoomLabel() =>
        _zoomLabel.Text = _canvas.HasImage ? $"%{_canvas.ZoomPercent:0}" : "";

    private Button SmallButton(string text, string tip, Action click)
    {
        var b = new Button
        {
            Text = text, AutoSize = true, MinimumSize = new System.Drawing.Size(26, 22), Height = 22,
            FlatStyle = FlatStyle.Flat, BackColor = Theme.Surface, ForeColor = Theme.Ink,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold), Margin = new Padding(2, 0, 0, 0),
            Padding = new Padding(4, 0, 4, 0), Cursor = Cursors.Hand, TabStop = false
        };
        b.FlatAppearance.BorderColor = Theme.Line;
        b.FlatAppearance.MouseOverBackColor = Theme.Hover;
        b.Click += (_, _) => click();
        new ToolTip().SetToolTip(b, tip);
        return b;
    }

    /// <summary>Kendi çizimini yapan, yakınlaştırılıp kaydırılabilen görüntü tuvali.</summary>
    private sealed class ZoomCanvas : Control
    {
        private Bitmap? _image;
        private double _scale = 1;          // ekran pikseli / görüntü pikseli
        private PointF _offset;             // görüntünün sol üst köşesinin ekrandaki yeri
        private bool _fitted = true;
        private System.Drawing.Point _down;
        private System.Drawing.Point _last;
        private bool _dragging;
        private const double MaxZoomOverFit = 16;

        public event Action<CvPoint>? PixelClicked;
        public event Action? ZoomChanged;

        public bool HasImage => _image is not null;
        public double ZoomPercent => _scale * 100;

        public ZoomCanvas()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Theme.Window;
            Cursor = Cursors.Cross;
        }

        public void SetImage(Bitmap bitmap)
        {
            Bitmap? old = _image;
            bool sameSize = old is not null && old.Size == bitmap.Size;
            _image = bitmap;
            old?.Dispose();
            if (!sameSize || _fitted) Fit(); else Invalidate();   // aynı boyutta yeni sonuç: bakılan yer korunur
        }

        private double FitScale => _image is null || Width < 2 || Height < 2
            ? 1 : Math.Min(Width / (double)_image.Width, Height / (double)_image.Height);

        public void Fit()
        {
            if (_image is null) return;
            _scale = FitScale;
            _offset = new PointF((float)((Width - _image.Width * _scale) / 2), (float)((Height - _image.Height * _scale) / 2));
            _fitted = true;
            Invalidate();
            ZoomChanged?.Invoke();
        }

        public void ZoomBy(double factor) => ZoomAt(factor, new System.Drawing.Point(Width / 2, Height / 2));

        private void ZoomAt(double factor, System.Drawing.Point screen)
        {
            if (_image is null) return;
            double fit = FitScale;
            double target = Math.Clamp(_scale * factor, fit, fit * MaxZoomOverFit);
            if (Math.Abs(target - fit) < 1e-6) { Fit(); return; }
            // İmlecin altındaki görüntü noktası yakınlaştırmadan sonra da imlecin altında kalsın.
            double ix = (screen.X - _offset.X) / _scale, iy = (screen.Y - _offset.Y) / _scale;
            _scale = target;
            _offset = new PointF((float)(screen.X - ix * _scale), (float)(screen.Y - iy * _scale));
            _fitted = false;
            ClampOffset();
            Invalidate();
            ZoomChanged?.Invoke();
        }

        /// <summary>Görüntü panelden taşmışsa kenarının boşluğa kaymasını engeller; küçükse ortalar.</summary>
        private void ClampOffset()
        {
            if (_image is null) return;
            float w = (float)(_image.Width * _scale), h = (float)(_image.Height * _scale);
            float x = w <= Width ? (Width - w) / 2 : Math.Clamp(_offset.X, Width - w, 0);
            float y = h <= Height ? (Height - h) / 2 : Math.Clamp(_offset.Y, Height - h, 0);
            _offset = new PointF(x, y);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_fitted) Fit(); else { ClampOffset(); Invalidate(); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ZoomAt(e.Delta > 0 ? 1.25 : 1 / 1.25, e.Location);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Focus();   // tekerlek olaylarının bu tuvale gelmesi için
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _down = _last = e.Location;
            _dragging = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Button is not (MouseButtons.Left or MouseButtons.Middle) || _image is null) return;
            if (!_dragging && Math.Abs(e.X - _down.X) + Math.Abs(e.Y - _down.Y) > 4) _dragging = true;
            if (!_dragging) return;
            _offset = new PointF(_offset.X + e.X - _last.X, _offset.Y + e.Y - _last.Y);
            _last = e.Location;
            _fitted = false;
            ClampOffset();
            Cursor = Cursors.SizeAll;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Cursor = Cursors.Cross;
            if (_dragging || e.Button != MouseButtons.Left || _image is null) return;
            double ix = (e.X - _offset.X) / _scale, iy = (e.Y - _offset.Y) / _scale;
            if (ix < 0 || iy < 0 || ix >= _image.Width || iy >= _image.Height) return;
            PixelClicked?.Invoke(new CvPoint((int)ix, (int)iy));
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            Fit();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_image is null) return;
            var g = e.Graphics;
            // Büyütürken pikseller keskin kalsın (piksel incelemek için); küçültürken yumuşak.
            g.InterpolationMode = _scale >= 1.5 ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(_image, new RectangleF(_offset.X, _offset.Y, (float)(_image.Width * _scale), (float)(_image.Height * _scale)));
            if (!_fitted)
            {
                using var font = new Font("Segoe UI", 8.25f);
                TextRenderer.DrawText(g, "Tekerlek: yakınlaştır · Sürükle: kaydır · Çift tık: sığdır", font,
                    new System.Drawing.Point(8, Height - 20), Theme.Muted, Theme.Window);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _image?.Dispose();
            base.Dispose(disposing);
        }
    }
}
