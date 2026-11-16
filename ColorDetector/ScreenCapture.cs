using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class ScreenCapture
{
    private const int Srccopy = 0x00CC0020;
    private const uint DibRgbColors = 0;

    private sealed class CircularCapture : IDisposable
    {
        private readonly IntPtr screenDc;
        private readonly IntPtr memoryDc;
        private IntPtr bitmap;
        private IntPtr previousBitmap;
        private IntPtr pixels;
        private int size;

        public CircularCapture()
        {
            screenDc = GetDC(IntPtr.Zero);
            memoryDc = CreateCompatibleDC(screenDc);
            if (screenDc == IntPtr.Zero || memoryDc == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "Could not initialize screen capture.");
            }
        }

        public unsafe (bool matched, byte r, byte g, byte b) Sample(
            int centerX,
            int centerY,
            int diameter)
        {
            EnsureBitmap(diameter);

            int left = centerX - diameter / 2;
            int top = centerY - diameter / 2;
            if (!BitBlt(
                    memoryDc,
                    0,
                    0,
                    diameter,
                    diameter,
                    screenDc,
                    left,
                    top,
                    Srccopy))
            {
                throw new InvalidOperationException("Screen capture failed.");
            }

            byte* data = (byte*)pixels;
            int centerIndex = diameter / 2;
            byte* centerPixel =
                data + (centerIndex * diameter + centerIndex) * 4;
            byte centerR = centerPixel[2];
            byte centerG = centerPixel[1];
            byte centerB = centerPixel[0];
            double radius = diameter / 2.0;
            double center = (diameter - 1) / 2.0;
            double radiusSquared = radius * radius;

            for (int y = 0; y < diameter; y++)
            {
                double dy = y - center;
                double horizontalRadiusSquared =
                    radiusSquared - dy * dy;
                if (horizontalRadiusSquared < 0)
                {
                    continue;
                }

                int halfWidth = (int)Math.Floor(
                    Math.Sqrt(horizontalRadiusSquared));
                int startX = Math.Max(0, (int)Math.Ceiling(center - halfWidth));
                int endX = Math.Min(
                    diameter - 1,
                    (int)Math.Floor(center + halfWidth));
                byte* row = data + y * diameter * 4;

                for (int x = startX; x <= endX; x++)
                {
                    byte* pixel = row + x * 4;
                    if (ColorStorage.Contains(pixel[2], pixel[1], pixel[0]))
                    {
                        return (true, pixel[2], pixel[1], pixel[0]);
                    }
                }
            }

            return (false, centerR, centerG, centerB);
        }

        private void EnsureBitmap(int requestedSize)
        {
            if (size == requestedSize)
            {
                return;
            }

            ReleaseBitmap();

            var info = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = requestedSize,
                    Height = -requestedSize,
                    Planes = 1,
                    BitCount = 32,
                    Compression = 0
                }
            };

            bitmap = CreateDIBSection(
                memoryDc,
                ref info,
                DibRgbColors,
                out pixels,
                IntPtr.Zero,
                0);
            if (bitmap == IntPtr.Zero || pixels == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "Could not allocate screen capture buffer.");
            }

            previousBitmap = SelectObject(memoryDc, bitmap);
            size = requestedSize;
        }

        private void ReleaseBitmap()
        {
            if (bitmap == IntPtr.Zero)
            {
                return;
            }

            SelectObject(memoryDc, previousBitmap);
            DeleteObject(bitmap);
            bitmap = IntPtr.Zero;
            previousBitmap = IntPtr.Zero;
            pixels = IntPtr.Zero;
            size = 0;
        }

        public void Dispose()
        {
            ReleaseBitmap();
            DeleteDC(memoryDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static readonly ThreadLocal<CircularCapture> Captures =
        new(() => new CircularCapture());

    public static int GetScreenWidth()
    {
        return Screen.PrimaryScreen?.Bounds.Width
            ?? throw new InvalidOperationException("No primary screen found.");
    }

    public static int GetScreenHeight()
    {
        return Screen.PrimaryScreen?.Bounds.Height
            ?? throw new InvalidOperationException("No primary screen found.");
    }

    public static (bool matched, byte r, byte g, byte b) SampleCircle(
        int centerX,
        int centerY,
        int diameter)
    {
        return Captures.Value?.Sample(centerX, centerY, diameter)
            ?? throw new InvalidOperationException(
                "Screen capture context was not initialized.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr deviceContext);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(
        IntPtr deviceContext,
        ref BitmapInfo bitmapInfo,
        uint usage,
        out IntPtr bits,
        IntPtr section,
        uint offset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(
        IntPtr deviceContext,
        IntPtr objectHandle);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr objectHandle);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(
        IntPtr destination,
        int destinationX,
        int destinationY,
        int width,
        int height,
        IntPtr source,
        int sourceX,
        int sourceY,
        int rasterOperation);
}
