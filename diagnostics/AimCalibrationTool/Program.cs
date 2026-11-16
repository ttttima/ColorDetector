using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

internal static class Program
{
    private const int ProbeCounts = 40;
    private const int SettleMilliseconds = 100;
    private const int RequiredConnectedPixels = 11;
    private const int VkF8 = 0x77;
    private const int VkQ = 0x51;
    private const int VkEscape = 0x1B;
    private static readonly string CalibrationPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "aim-calibration.json");
    private static readonly bool[] SavedColors = new bool[1 << 24];
    private static readonly CardputerClient Cardputer = new();
    private static Calibration? calibration;

    [STAThread]
    private static void Main(string[] args)
    {
        Console.Title = "Cardputer Aim Calibration Tool";
        LoadColors();
        calibration = LoadCalibration();

        Console.WriteLine("Cardputer Aim Calibration Tool");
        Console.WriteLine("Close ColorDetector before using this tool.");
        Console.WriteLine("F8 = calibrate, Q = one straight aim, Esc = exit");
        if (calibration is not null)
        {
            Console.WriteLine("Loaded saved 2x2 calibration matrix.");
        }

        try
        {
            Cardputer.Connect();
            Console.WriteLine("Cardputer connected.");
            if (args.Contains(
                    "--calibrate",
                    StringComparer.OrdinalIgnoreCase))
            {
                Calibrate();
                return;
            }
            if (args.Contains(
                    "--aim",
                    StringComparer.OrdinalIgnoreCase))
            {
                AimOnce();
                return;
            }
            if (args.Contains(
                    "--calibrate-visual",
                    StringComparer.OrdinalIgnoreCase))
            {
                CalibrateVisualPipeline();
                return;
            }
            if (args.Contains(
                    "--aim-visual",
                    StringComparer.OrdinalIgnoreCase))
            {
                AimVisualOnce();
                return;
            }
            PollKeys();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal: {ex.Message}");
        }
        finally
        {
            Cardputer.Dispose();
        }
    }

    private static void PollKeys()
    {
        bool previousF8 = false;
        bool previousQ = false;

        while ((GetAsyncKeyState(VkEscape) & 0x8000) == 0)
        {
            bool f8 = (GetAsyncKeyState(VkF8) & 0x8000) != 0;
            bool q = (GetAsyncKeyState(VkQ) & 0x8000) != 0;

            if (f8 && !previousF8)
            {
                Calibrate();
            }
            if (q && !previousQ)
            {
                AimOnce();
            }

            previousF8 = f8;
            previousQ = q;
            Thread.Sleep(1);
        }
    }

    private static void Calibrate()
    {
        Console.WriteLine("Calibration started...");
        Point baseline = GetCursorPosition();

        MoveStraight(ProbeCounts, 0);
        Thread.Sleep(SettleMilliseconds);
        Point afterX = GetCursorPosition();
        MoveStraight(-ProbeCounts, 0);
        Thread.Sleep(SettleMilliseconds);

        Point baselineY = GetCursorPosition();
        MoveStraight(0, ProbeCounts);
        Thread.Sleep(SettleMilliseconds);
        Point afterY = GetCursorPosition();
        MoveStraight(0, -ProbeCounts);
        Thread.Sleep(SettleMilliseconds);

        Console.WriteLine(
            $"Cursor probes: X {baseline.X},{baseline.Y} -> "
            + $"{afterX.X},{afterX.Y}; Y {baselineY.X},{baselineY.Y} -> "
            + $"{afterY.X},{afterY.Y}");

        calibration = new Calibration
        {
            ScreenXPerHidX = (afterX.X - baseline.X) / (double)ProbeCounts,
            ScreenYPerHidX = (afterX.Y - baseline.Y) / (double)ProbeCounts,
            ScreenXPerHidY = (afterY.X - baselineY.X) / (double)ProbeCounts,
            ScreenYPerHidY = (afterY.Y - baselineY.Y) / (double)ProbeCounts
        };

        if (Math.Abs(calibration.Determinant) < 0.0001)
        {
            calibration = null;
            Console.WriteLine("Calibration failed: measured matrix is singular.");
            return;
        }

        File.WriteAllText(
            CalibrationPath,
            JsonSerializer.Serialize(
                calibration,
                new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Calibration saved:");
        Console.WriteLine(calibration);
    }

    private static void CalibrateVisualPipeline()
    {
        Console.WriteLine("Visual camera calibration started...");
        Point baseline = CaptureTargetOrThrow();

        MoveStraight(ProbeCounts, 0);
        Thread.Sleep(SettleMilliseconds);
        Point afterX = CaptureTargetOrThrow();
        MoveStraight(-ProbeCounts, 0);
        Thread.Sleep(SettleMilliseconds);

        Point baselineY = CaptureTargetOrThrow();
        MoveStraight(0, ProbeCounts);
        Thread.Sleep(SettleMilliseconds);
        Point afterY = CaptureTargetOrThrow();
        MoveStraight(0, -ProbeCounts);
        Thread.Sleep(SettleMilliseconds);

        calibration = new Calibration
        {
            ScreenXPerHidX = (afterX.X - baseline.X) / (double)ProbeCounts,
            ScreenYPerHidX = (afterX.Y - baseline.Y) / (double)ProbeCounts,
            ScreenXPerHidY = (afterY.X - baselineY.X) / (double)ProbeCounts,
            ScreenYPerHidY = (afterY.Y - baselineY.Y) / (double)ProbeCounts
        };

        Console.WriteLine(
            $"Visual probes: X {baseline.X},{baseline.Y} -> "
            + $"{afterX.X},{afterX.Y}; Y {baselineY.X},{baselineY.Y} -> "
            + $"{afterY.X},{afterY.Y}");
        if (Math.Abs(calibration.Determinant) < 0.0001)
        {
            calibration = null;
            Console.WriteLine(
                "Visual calibration failed: target response is singular.");
            return;
        }

        File.WriteAllText(
            CalibrationPath,
            JsonSerializer.Serialize(
                calibration,
                new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Visual calibration saved: {calibration}");
    }

    private static void AimOnce()
    {
        if (calibration is null)
        {
            Console.WriteLine("Press F8 to calibrate first.");
            return;
        }

        Point target = CaptureTargetOrThrow();
        Rectangle screen = Screen.PrimaryScreen?.Bounds
            ?? throw new InvalidOperationException("No primary screen found.");
        Point center = new(
            screen.Left + screen.Width / 2,
            screen.Top + screen.Height / 2);
        MoveCursorTo(center);
        var desiredScreenMovement = new Point(
            target.X - screen.Width / 2,
            target.Y - screen.Height / 2);

        (double hidX, double hidY) = calibration.Solve(desiredScreenMovement);
        int commandX = (int)Math.Round(hidX);
        int commandY = (int)Math.Round(hidY);
        Console.WriteLine(
            $"Target {target.X},{target.Y}; screen delta "
            + $"{desiredScreenMovement.X},{desiredScreenMovement.Y}; "
            + $"HID {commandX},{commandY}");

        Point cursorBefore = GetCursorPosition();
        MoveStraight(commandX, commandY);
        Thread.Sleep(SettleMilliseconds);
        Point cursorAfter = GetCursorPosition();
        var actualMovement = new Point(
            cursorAfter.X - cursorBefore.X,
            cursorAfter.Y - cursorBefore.Y);
        Console.WriteLine(
            $"Actual cursor delta {actualMovement.X},{actualMovement.Y}; "
            + $"residual {desiredScreenMovement.X - actualMovement.X},"
            + $"{desiredScreenMovement.Y - actualMovement.Y}");
        LearnFromAim(commandX, commandY, actualMovement);
    }

    private static void AimVisualOnce()
    {
        if (calibration is null)
        {
            Console.WriteLine("Run --calibrate-visual first.");
            return;
        }

        Point target = CaptureTargetOrThrow();
        Rectangle screen = Screen.PrimaryScreen?.Bounds
            ?? throw new InvalidOperationException("No primary screen found.");
        var desiredTargetMovement = new Point(
            screen.Width / 2 - target.X,
            screen.Height / 2 - target.Y);
        (double hidX, double hidY) = calibration.Solve(
            desiredTargetMovement);
        int commandX = (int)Math.Round(hidX);
        int commandY = (int)Math.Round(hidY);

        Console.WriteLine(
            $"Visual target {target.X},{target.Y}; desired target delta "
            + $"{desiredTargetMovement.X},{desiredTargetMovement.Y}; "
            + $"HID {commandX},{commandY}");
        MoveStraight(commandX, commandY);
        Thread.Sleep(SettleMilliseconds);
        Point result = CaptureTargetOrThrow();
        Console.WriteLine(
            $"Visual result {result.X},{result.Y}; residual "
            + $"{screen.Width / 2 - result.X},"
            + $"{screen.Height / 2 - result.Y}");
        LearnFromVisualAim(target, result, commandX, commandY);
    }

    private static void LearnFromVisualAim(
        Point before,
        Point after,
        int commandX,
        int commandY)
    {
        if (calibration is null)
        {
            return;
        }

        if (Math.Abs(commandX) >= 200 && after.X != before.X)
        {
            calibration.ScreenXPerHidX =
                (after.X - before.X) / (double)commandX;
        }
        if (Math.Abs(commandY) >= 200 && after.Y != before.Y)
        {
            calibration.ScreenYPerHidY =
                (after.Y - before.Y) / (double)commandY;
        }
        calibration.ScreenXPerHidY = 0;
        calibration.ScreenYPerHidX = 0;

        File.WriteAllText(
            CalibrationPath,
            JsonSerializer.Serialize(
                calibration,
                new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Learned visual matrix: {calibration}");
    }

    private static void LearnFromAim(
        int commandX,
        int commandY,
        Point actualMovement)
    {
        if (calibration is null)
        {
            return;
        }

        if (commandX != 0 && Math.Abs(actualMovement.X) > 1)
        {
            calibration.ScreenXPerHidX =
                actualMovement.X / (double)commandX;
        }
        if (commandY != 0 && Math.Abs(actualMovement.Y) > 1)
        {
            calibration.ScreenYPerHidY =
                actualMovement.Y / (double)commandY;
        }

        File.WriteAllText(
            CalibrationPath,
            JsonSerializer.Serialize(
                calibration,
                new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Learned full-distance matrix: {calibration}");
    }

    private static void MoveCursorTo(Point target)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            Point current = GetCursorPosition();
            var error = new Point(
                target.X - current.X,
                target.Y - current.Y);
            if (error.X == 0 && error.Y == 0)
            {
                return;
            }

            (double x, double y) = calibration!.Solve(error);
            MoveStraight((int)Math.Round(x), (int)Math.Round(y));
            Thread.Sleep(SettleMilliseconds);
        }

        Point result = GetCursorPosition();
        Console.WriteLine(
            $"Centering residual {target.X - result.X},"
            + $"{target.Y - result.Y}");
    }

    private static void MoveStraight(int totalX, int totalY)
    {
        int steps = Math.Max(1, (int)Math.Ceiling(
            Math.Max(Math.Abs(totalX), Math.Abs(totalY)) / 8.0));
        int sentX = 0;
        int sentY = 0;

        for (int step = 1; step <= steps; step++)
        {
            int targetX = (int)Math.Round(totalX * step / (double)steps);
            int targetY = (int)Math.Round(totalY * step / (double)steps);
            int deltaX = targetX - sentX;
            int deltaY = targetY - sentY;
            if (!Cardputer.Move(deltaX, deltaY))
            {
                throw new InvalidOperationException("Cardputer MOVE failed.");
            }
            sentX = targetX;
            sentY = targetY;
        }
    }

    private static Point CaptureTargetOrThrow()
    {
        Rectangle bounds = Screen.PrimaryScreen?.Bounds
            ?? throw new InvalidOperationException("No primary screen found.");
        using var bitmap = new Bitmap(
            bounds.Width,
            bounds.Height,
            PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        }

        return FindTopmostTarget(bitmap)
            ?? throw new InvalidOperationException(
                "No qualifying connected yellow target found.");
    }

    private static unsafe Point? FindTopmostTarget(Bitmap bitmap)
    {
        BitmapData data = bitmap.LockBits(
            new Rectangle(Point.Empty, bitmap.Size),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < data.Height; y++)
            {
                byte* row = (byte*)data.Scan0 + y * data.Stride;
                for (int x = 0; x < data.Width; x++)
                {
                    byte* pixel = row + x * 4;
                    if (IsSaved(pixel)
                        && HasConnectedSupport(data, x, y))
                    {
                        return new Point(x, y);
                    }
                }
            }
            return null;
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
            if (value.Length == 7
                && value[0] == '#'
                && int.TryParse(
                    value.AsSpan(1),
                    System.Globalization.NumberStyles.HexNumber,
                    null,
                    out int packed))
            {
                SavedColors[packed] = true;
            }
        }
    }

    private static Calibration? LoadCalibration()
    {
        try
        {
            return File.Exists(CalibrationPath)
                ? JsonSerializer.Deserialize<Calibration>(
                    File.ReadAllText(CalibrationPath))
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static Point GetCursorPosition()
    {
        return GetCursorPos(out Point point)
            ? point
            : throw new InvalidOperationException(
                "Could not read the Windows cursor position.");
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);
}

internal sealed class Calibration
{
    public double ScreenXPerHidX { get; set; }
    public double ScreenYPerHidX { get; set; }
    public double ScreenXPerHidY { get; set; }
    public double ScreenYPerHidY { get; set; }
    public double Determinant =>
        ScreenXPerHidX * ScreenYPerHidY
        - ScreenXPerHidY * ScreenYPerHidX;

    public (double x, double y) Solve(Point desired)
    {
        double determinant = Determinant;
        return (
            (desired.X * ScreenYPerHidY
                - ScreenXPerHidY * desired.Y) / determinant,
            (ScreenXPerHidX * desired.Y
                - desired.X * ScreenYPerHidX) / determinant);
    }

    public override string ToString()
    {
        return $"[{ScreenXPerHidX:0.0000}, {ScreenXPerHidY:0.0000}; "
            + $"{ScreenYPerHidX:0.0000}, {ScreenYPerHidY:0.0000}]";
    }
}
