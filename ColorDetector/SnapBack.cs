using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

public static class SnapBack
{
    private const int MaximumReportCorrection = 24;
    private const int MinimumPathSteps = 12;
    private const int MaximumPathSteps = 100;
    private static int returnGeneration;

    public static void Cancel()
    {
        Interlocked.Increment(ref returnGeneration);
    }

    public static Point GetCursorPosition()
    {
        return GetCursorPos(out Point point) ? point : Point.Empty;
    }

    public static void Start(
        Point target,
        int delayHalfMilliseconds,
        int timeMultiplierPercent)
    {
        int generation = Interlocked.Increment(ref returnGeneration);

        _ = Task.Run(() =>
        {
            WaitPrecisely(delayHalfMilliseconds * 0.5);
            Point delayedPosition = GetCursorPosition();
            FollowSmoothPath(
                delayedPosition,
                target,
                generation,
                timeMultiplierPercent / 100.0);
        });
    }

    private static void FollowSmoothPath(
        Point start,
        Point target,
        int generation,
        double timeMultiplier)
    {
        double distance = Math.Sqrt(
            Math.Pow(target.X - start.X, 2)
            + Math.Pow(target.Y - start.Y, 2));
        int baseSteps = Math.Clamp(
            (int)Math.Ceiling(distance / 8.0),
            MinimumPathSteps,
            MaximumPathSteps);
        int steps = Math.Clamp(
            (int)Math.Round(baseSteps * timeMultiplier),
            3,
            MaximumPathSteps * 4);

        for (int step = 1; step <= steps; step++)
        {
            if (generation != Volatile.Read(ref returnGeneration))
            {
                return;
            }

            double progress = step / (double)steps;
            double eased = progress * progress * (3.0 - 2.0 * progress);
            var pathPoint = new Point(
                (int)Math.Round(start.X + (target.X - start.X) * eased),
                (int)Math.Round(start.Y + (target.Y - start.Y) * eased));

            if (!MoveToward(pathPoint))
            {
                return;
            }

            Thread.Sleep(1);
        }

        LockToExactPosition(target, generation);
    }

    private static void LockToExactPosition(Point target, int generation)
    {
        long deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 5;
        int exactReadCount = 0;

        while (generation == Volatile.Read(ref returnGeneration)
            && Stopwatch.GetTimestamp() < deadline)
        {
            Point current = GetCursorPosition();
            int x = target.X - current.X;
            int y = target.Y - current.Y;

            if (x == 0 && y == 0)
            {
                exactReadCount++;
                if (exactReadCount >= 3)
                {
                    AppLog.Write(
                        $"Snap-back restored {target.X}, {target.Y}.");
                    return;
                }

                Thread.Sleep(1);
                continue;
            }

            exactReadCount = 0;
            if (!MoveToward(target))
            {
                return;
            }

            Thread.Sleep(2);
        }

        if (generation == Volatile.Read(ref returnGeneration))
        {
            Point current = GetCursorPosition();
            AppLog.Write(
                $"Snap-back timed out at {current.X}, {current.Y}; "
                + $"target was {target.X}, {target.Y}.");
        }
    }

    private static bool MoveToward(Point destination)
    {
        Point current = GetCursorPosition();
        int x = Math.Clamp(
            destination.X - current.X,
            -MaximumReportCorrection,
            MaximumReportCorrection);
        int y = Math.Clamp(
            destination.Y - current.Y,
            -MaximumReportCorrection,
            MaximumReportCorrection);

        return (x == 0 && y == 0) || Input.MoveRelative(x, y);
    }

    private static void WaitPrecisely(double milliseconds)
    {
        long target = Stopwatch.GetTimestamp()
            + (long)(milliseconds * Stopwatch.Frequency / 1000.0);

        while (true)
        {
            long remaining = target - Stopwatch.GetTimestamp();
            if (remaining <= 0)
            {
                return;
            }

            double remainingMilliseconds =
                remaining * 1000.0 / Stopwatch.Frequency;
            if (remainingMilliseconds > 1.5)
            {
                Thread.Sleep(Math.Max(
                    1,
                    (int)remainingMilliseconds - 1));
            }
            else
            {
                Thread.SpinWait(32);
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);
}
