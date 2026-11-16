using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public sealed class CrosshairOverlay : Form
{
    private readonly DetectionSettings settings;
    private readonly System.Windows.Forms.Timer refreshTimer = new()
    {
        Interval = 33
    };
    private int displayedSize;
    private bool displayedVisible;
    private int displayedPreviewSize;
    private bool displayedPreviewVisible;

    public CrosshairOverlay(DetectionSettings settings)
    {
        this.settings = settings;
        displayedSize = settings.OverlayCircleSize;
        displayedVisible = settings.OverlayCircleVisible;
        displayedPreviewSize = settings.PreviewCircleSize;
        displayedPreviewVisible = settings.PreviewCircleVisibleOnScreen;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.PrimaryScreen?.Bounds
            ?? throw new InvalidOperationException("No primary screen found.");
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        DoubleBuffered = true;

        refreshTimer.Tick += (_, _) =>
        {
            int nextSize = settings.OverlayCircleSize;
            bool nextVisible = settings.OverlayCircleVisible;
            int nextPreviewSize = settings.PreviewCircleSize;
            bool nextPreviewVisible = settings.PreviewCircleVisibleOnScreen;
            if (nextSize != displayedSize
                || nextVisible != displayedVisible
                || nextPreviewSize != displayedPreviewSize
                || nextPreviewVisible != displayedPreviewVisible)
            {
                displayedSize = nextSize;
                displayedVisible = nextVisible;
                displayedPreviewSize = nextPreviewSize;
                displayedPreviewVisible = nextPreviewVisible;
                Invalidate();
            }
        };
        Shown += (_, _) => refreshTimer.Start();
        FormClosed += (_, _) => refreshTimer.Dispose();
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        SetWindowDisplayAffinity(Handle, 0x00000011);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WsExNoActivate = 0x08000000;
            const int WsExTransparent = 0x00000020;
            const int WsExToolWindow = 0x00000080;

            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |=
                WsExNoActivate | WsExTransparent | WsExToolWindow;
            return parameters;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!displayedVisible && !displayedPreviewVisible)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.None;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.None;

        if (displayedVisible)
        {
            DrawCenteredCircle(e.Graphics, displayedSize, Color.White);
        }

        if (displayedPreviewVisible)
        {
            DrawCenteredCircle(
                e.Graphics,
                displayedPreviewSize,
                Color.FromArgb(210, 255, 255, 255));
        }
    }

    private void DrawCenteredCircle(
        Graphics graphics,
        int diameter,
        Color color)
    {
        int x = ClientSize.Width / 2 - diameter / 2;
        int y = ClientSize.Height / 2 - diameter / 2;
        var circle = new Rectangle(x, y, diameter, diameter);

        using var circlePen = new Pen(color, 1);
        graphics.DrawEllipse(circlePen, circle);
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(
        IntPtr window,
        uint affinity);
}
