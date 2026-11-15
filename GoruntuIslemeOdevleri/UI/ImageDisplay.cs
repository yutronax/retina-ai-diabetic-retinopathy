using GoruntuIslemeOdevleri.Core;
using CvMat = OpenCvSharp.Mat;
using CvPoint = OpenCvSharp.Point;

namespace GoruntuIslemeOdevleri.UI;

internal sealed class ImageDisplay : UserControl
{
    private readonly Label _title;
    private readonly PictureBox _picture;
    private System.Drawing.Size _sourceSize;

    public event EventHandler<CvPoint>? ImagePixelClicked;

    public ImageDisplay(string title)
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(242, 244, 248);
        Padding = new Padding(8);

        _title = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 34,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 39, 58),
            TextAlign = ContentAlignment.MiddleLeft
        };
        _picture = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(34, 39, 48),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
            Cursor = Cursors.Cross
        };
        _picture.MouseClick += PictureOnMouseClick;
        Controls.Add(_picture);
        Controls.Add(_title);
    }

    public void SetTitle(string title) => _title.Text = title;

    public void SetMat(CvMat image)
    {
        Image? old = _picture.Image;
        _picture.Image = ImageHelpers.ToBitmap(image);
        _sourceSize = new System.Drawing.Size(image.Width, image.Height);
        old?.Dispose();
    }

    private void PictureOnMouseClick(object? sender, MouseEventArgs e)
    {
        if (_picture.Image is null || _sourceSize.Width <= 0 || _sourceSize.Height <= 0)
            return;

        double imageRatio = _sourceSize.Width / (double)_sourceSize.Height;
        double boxRatio = _picture.ClientSize.Width / (double)Math.Max(1, _picture.ClientSize.Height);
        double shownWidth;
        double shownHeight;
        double offsetX;
        double offsetY;

        if (imageRatio > boxRatio)
        {
            shownWidth = _picture.ClientSize.Width;
            shownHeight = shownWidth / imageRatio;
            offsetX = 0;
            offsetY = (_picture.ClientSize.Height - shownHeight) / 2.0;
        }
        else
        {
            shownHeight = _picture.ClientSize.Height;
            shownWidth = shownHeight * imageRatio;
            offsetX = (_picture.ClientSize.Width - shownWidth) / 2.0;
            offsetY = 0;
        }

        if (e.X < offsetX || e.X > offsetX + shownWidth || e.Y < offsetY || e.Y > offsetY + shownHeight)
            return;

        int sourceX = (int)Math.Round((e.X - offsetX) * _sourceSize.Width / shownWidth);
        int sourceY = (int)Math.Round((e.Y - offsetY) * _sourceSize.Height / shownHeight);
        sourceX = Math.Clamp(sourceX, 0, _sourceSize.Width - 1);
        sourceY = Math.Clamp(sourceY, 0, _sourceSize.Height - 1);
        ImagePixelClicked?.Invoke(this, new CvPoint(sourceX, sourceY));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _picture.Image?.Dispose();
        base.Dispose(disposing);
    }
}
