using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

internal static class Program
{
    private const int VkQ = 0x51;
    private const int VkEscape = 0x1B;
    private const int RequiredConnectedPixels = 11;
    private const int RecordingMilliseconds = 2000;
    private const int RequestedSampleIntervalMilliseconds = 5;
    private const int TrackingRadiusPixels = 320;
    private static readonly bool[] SavedColors = new bool[1 << 24];
    private static readonly string LogDirectory = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "logs");
    private static int sessionNumber;
    private static readonly object LatestTargetSync = new();
    private static Point? latestTarget;
    private static long latestTargetTimestamp;
    private static volatile bool recording;
    private static volatile bool running = true;

    [STAThread]
    private static void Main()
    {
        Console.Title = "Aim Telemetry Logger";
        LoadColors();
        Directory.CreateDirectory(LogDirectory);

        Console.WriteLine("Standalone Aim Telemetry Logger");
        Console.WriteLine("This program does not connect to the Cardputer.");
        Console.WriteLine("Focus the simulator, then press Q normally.");
        Console.WriteLine("Esc exits. Logs are written to:");
        Console.WriteLine(LogDirectory);

        var cacheThread = new Thread(CacheTargets)
        {
            IsBackground = true,
            Name = "AimTelemetryPreQCache",
            Priority = ThreadPriority.AboveNormal
        };
        cacheThread.Start();

        bool previousQ = false;
        while ((GetAsyncKeyState(VkEscape) & 0x8000) == 0)
        {
            bool q = (GetAsyncKeyState(VkQ) & 0x8000) != 0;
            if (q && !previousQ)
            {
                RecordFirstAttempt();
            }
            previousQ = q;
            Thread.Sleep(1);
        }
        running = false;
        cacheThread.Join(1000);
    }

    private static void CacheTargets()
    {
        Rectangle bounds = Screen.PrimaryScreen?.Bounds ?? Rectangle.Empty;
        Point center = new(bounds.Width / 2, bounds.Height / 2);
        while (running)
        {
            if (recording)
            {
                Thread.Sleep(1);
                continue;
            }

            try
            {
                Point? target = CaptureClosestTarget(bounds, center);
                if (target is Point point)
                {
                    lock (LatestTargetSync)
                    {
                        latestTarget = point;
                        latestTargetTimestamp = Stopwatch.GetTimestamp();
                    }
                }
            }
            catch
            {
                Thread.Sleep(10);
            }
        }
    }

    private static void RecordFirstAttempt()
    {
        Rectangle bounds = Screen.PrimaryScreen?.Bounds
            ?? throw new InvalidOperationException("No primary screen found.");
        Point expected = new(bounds.Width / 2, bounds.Height / 2);
        recording = true;
        Point? cachedTarget;
        long cachedTimestamp;
        lock (LatestTargetSync)
        {
            cachedTarget = latestTarget;
            cachedTimestamp = latestTargetTimestamp;
        }
        cachedTarget ??= CaptureClosestTarget(bounds, expected);
        int session = Interlocked.Increment(ref sessionNumber);
        string path = Path.Combine(
            LogDirectory,
            $"aim-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{session}.csv");
        var output = new StringBuilder(32 * 1024);
        output.AppendLine(
            "session,sample,elapsed_ms,target_x,target_y,expected_x,"
            + "expected_y,error_x,error_y,distance_px,correct_horizontal,"
            + "correct_vertical,detected");

        var timer = Stopwatch.StartNew();
        int sample = 0;
        Point? first = cachedTarget;
        Point? last = null;
        Point? tracked = cachedTarget;
        var settledPoints = new Queue<Point>();

        if (cachedTarget is Point initial)
        {
            AppendSample(
                output,
                session,
                sample++,
                0,
                initial,
                expected);
        }

        while (timer.ElapsedMilliseconds < RecordingMilliseconds)
        {
            long started = timer.ElapsedMilliseconds;
            Point? target = tracked is Point previous
                ? CaptureTrackedTarget(bounds, previous)
                : CaptureClosestTarget(bounds, expected);
            if (target is Point point)
            {
                first ??= point;
                last = point;
                tracked = point;
                AppendSample(
                    output,
                    session,
                    sample,
                    timer.Elapsed.TotalMilliseconds,
                    point,
                    expected);
                settledPoints.Enqueue(point);
                while (settledPoints.Count > 15)
                {
                    settledPoints.Dequeue();
                }
            }
            else
            {
                output.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{session},{sample},{timer.Elapsed.TotalMilliseconds:0.000},"
                    + $",,{expected.X},{expected.Y},,,,,,0"));
            }

            sample++;
            int delay = RequestedSampleIntervalMilliseconds
                - (int)(timer.ElapsedMilliseconds - started);
            if (delay > 0)
            {
                Thread.Sleep(delay);
            }
        }

        File.WriteAllText(path, output.ToString());
        recording = false;
        if (first is Point start && last is Point finish)
        {
            Point stable = MedianPoint(settledPoints);
            double preQAgeMs = cachedTimestamp == 0
                ? double.NaN
                : (Stopwatch.GetTimestamp() - cachedTimestamp)
                    * 1000.0 / Stopwatch.Frequency
                    - timer.Elapsed.TotalMilliseconds;
            Console.WriteLine(
                $"Session {session}: start {start.X},{start.Y}; "
                + $"stable landing {stable.X},{stable.Y}; residual "
                + $"{stable.X - expected.X},{stable.Y - expected.Y}; "
                + $"correct {DescribeHorizontal(stable.X - expected.X)} "
                + $"{DescribeVertical(stable.Y - expected.Y)}; "
                + $"samples {sample}; pre-Q cache age "
                + $"{preQAgeMs:0.0} ms");
            File.WriteAllText(
                Path.ChangeExtension(path, ".summary.txt"),
                $"Session: {session}{Environment.NewLine}"
                + $"Expected crosshair: {expected.X}, {expected.Y}"
                + Environment.NewLine
                + $"Target before Q: {start.X}, {start.Y}"
                + Environment.NewLine
                + $"Stable final target: {stable.X}, {stable.Y}"
                + Environment.NewLine
                + $"Residual: {stable.X - expected.X}, "
                + $"{stable.Y - expected.Y}{Environment.NewLine}"
                + $"Required correction: "
                + $"{DescribeHorizontal(stable.X - expected.X)}, "
                + $"{DescribeVertical(stable.Y - expected.Y)}"
                + Environment.NewLine);
        }
        else
        {
            Console.WriteLine(
                $"Session {session}: target was not consistently detected.");
        }
        Console.WriteLine(path);
    }

    private static void AppendSample(
        StringBuilder output,
        int session,
        int sample,
        double elapsedMilliseconds,
        Point point,
        Point expected)
    {
        int errorX = point.X - expected.X;
        int errorY = point.Y - expected.Y;
        double distance = Math.Sqrt(
            errorX * (double)errorX + errorY * (double)errorY);
        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{session},{sample},{elapsedMilliseconds:0.000},"
            + $"{point.X},{point.Y},{expected.X},{expected.Y},"
            + $"{errorX},{errorY},{distance:0.000},"
            + $"{DescribeHorizontal(errorX)},"
            + $"{DescribeVertical(errorY)},1"));
    }

    private static string DescribeHorizontal(int errorX)
    {
        return errorX switch
        {
            > 0 => $"RIGHT {errorX}px",
            < 0 => $"LEFT {-errorX}px",
            _ => "CENTERED"
        };
    }

    private static string DescribeVertical(int errorY)
    {
        return errorY switch
        {
            > 0 => $"DOWN {errorY}px",
            < 0 => $"UP {-errorY}px",
            _ => "CENTERED"
        };
    }

    private static Point MedianPoint(IEnumerable<Point> points)
    {
        Point[] values = points.ToArray();
        if (values.Length == 0) return Point.Empty;
        int[] xs = values.Select(point => point.X).Order().ToArray();
        int[] ys = values.Select(point => point.Y).Order().ToArray();
        return new Point(xs[xs.Length / 2], ys[ys.Length / 2]);
    }

    private static Point? CaptureTrackedTarget(
        Rectangle screenBounds,
        Point previous)
    {
        Rectangle region = Rectangle.Intersect(
            new Rectangle(
                previous.X - TrackingRadiusPixels,
                previous.Y - TrackingRadiusPixels,
                TrackingRadiusPixels * 2 + 1,
                TrackingRadiusPixels * 2 + 1),
            new Rectangle(Point.Empty, screenBounds.Size));
        using var bitmap = new Bitmap(
            region.Width,
            region.Height,
            PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                screenBounds.Left + region.Left,
                screenBounds.Top + region.Top,
                0,
                0,
                region.Size);
        }

        Point localPrevious = new(
            previous.X - region.Left,
            previous.Y - region.Top);
        Point? localTarget = FindClosestTarget(bitmap, localPrevious);
        return localTarget is Point target
            ? new Point(target.X + region.Left, target.Y + region.Top)
            : null;
    }

    private static Point? CaptureClosestTarget(
        Rectangle bounds,
        Point center)
    {
        using var bitmap = new Bitmap(
            bounds.Width,
            bounds.Height,
            PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        }
        return FindClosestTarget(bitmap, center);
    }

    private static unsafe Point? FindClosestTarget(
        Bitmap bitmap,
        Point center)
    {
        BitmapData data = bitmap.LockBits(
            new Rectangle(Point.Empty, bitmap.Size),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            Point? closest = null;
            long closestDistance = long.MaxValue;
            for (int y = 0; y < data.Height; y++)
            {
                byte* row = (byte*)data.Scan0 + y * data.Stride;
                for (int x = 0; x < data.Width; x++)
                {
                    byte* pixel = row + x * 4;
                    if (!IsSaved(pixel)) continue;
                    long dx = x - center.X;
                    long dy = y - center.Y;
                    long distance = dx * dx + dy * dy;
                    if (distance >= closestDistance
                        || !HasConnectedSupport(data, x, y)) continue;
                    closest = new Point(x, y);
                    closestDistance = distance;
                }
            }
            return closest;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static unsafe bool HasConnectedSupport(
        BitmapData data,
        int startX,
        int startY)
    {
        Span<Point> points = stackalloc Point[RequiredConnectedPixels];
        points[0] = new Point(startX, startY);
        int count = 1;
        int next = 0;
        while (next < count)
        {
            Point current = points[next++];
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int x = current.X + dx;
                int y = current.Y + dy;
                if (x < 0 || x >= data.Width || y < 0 || y >= data.Height
                    || !IsSavedAt(data, x, y)) continue;
                bool known = false;
                for (int index = 0; index < count; index++)
                {
                    if (points[index].X == x && points[index].Y == y)
                    {
                        known = true;
                        break;
                    }
                }
                if (known) continue;
                points[count++] = new Point(x, y);
                if (count >= RequiredConnectedPixels) return true;
            }
        }
        return false;
    }

    private static unsafe bool IsSavedAt(BitmapData data, int x, int y)
    {
        byte* pixel = (byte*)data.Scan0 + y * data.Stride + x * 4;
        return IsSaved(pixel);
    }

    private static unsafe bool IsSaved(byte* pixel)
    {
        return SavedColors[(pixel[2] << 16) | (pixel[1] << 8) | pixel[0]];
    }

    private static void LoadColors()
    {
        string path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "colors.txt");
        foreach (string line in File.ReadLines(path))
        {
            string value = line.Trim();
            if (value.Length == 7 && value[0] == '#'
                && int.TryParse(
                    value.AsSpan(1),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out int packed))
            {
                SavedColors[packed] = true;
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
