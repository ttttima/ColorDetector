using System.Globalization;

public static class ColorStorage
{
    private const double DefaultTolerancePercent = 5.0;
    private static readonly string FilePath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "colors.txt");
    private static readonly HashSet<int> ExactColors = [];
    private static readonly bool[] ExactMatches = new bool[1 << 24];
    private static readonly bool[] DefaultMatches = new bool[1 << 24];
    private static int[] sourceColors = [];

    public static void LoadColors()
    {
        if (!File.Exists(FilePath))
        {
            AppLog.Write("colors.txt not found.");
            return;
        }

        ExactColors.Clear();
        Array.Clear(ExactMatches);
        Array.Clear(DefaultMatches);
        var loadedColors = new List<int>();

        foreach (string line in File.ReadLines(FilePath))
        {
            string clean = line.Trim();
            if (clean.Length != 7
                || clean[0] != '#'
                || !int.TryParse(
                    clean.AsSpan(1),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out int packed))
            {
                continue;
            }

            if (ExactColors.Add(packed))
            {
                loadedColors.Add(packed);
                ExactMatches[packed] = true;
            }
        }

        sourceColors = loadedColors.ToArray();
        int tolerance = GetChannelTolerance(DefaultTolerancePercent);
        foreach (int packed in sourceColors)
        {
            int sourceR = (packed >> 16) & 0xFF;
            int sourceG = (packed >> 8) & 0xFF;
            int sourceB = packed & 0xFF;

            for (int r = Math.Max(0, sourceR - tolerance);
                 r <= Math.Min(255, sourceR + tolerance);
                 r++)
            for (int g = Math.Max(0, sourceG - tolerance);
                 g <= Math.Min(255, sourceG + tolerance);
                 g++)
            for (int b = Math.Max(0, sourceB - tolerance);
                 b <= Math.Min(255, sourceB + tolerance);
                 b++)
            {
                DefaultMatches[Pack(r, g, b)] = true;
            }
        }

        AppLog.Write($"Loaded {sourceColors.Length} colors.");
    }

    public static bool ContainsExact(byte r, byte g, byte b)
    {
        return ExactMatches[Pack(r, g, b)];
    }

    public static bool Contains(
        byte r,
        byte g,
        byte b,
        double tolerancePercent = DefaultTolerancePercent)
    {
        int color = Pack(r, g, b);
        if (tolerancePercent == DefaultTolerancePercent)
        {
            return DefaultMatches[color];
        }

        if (ExactColors.Contains(color))
        {
            return true;
        }

        if (tolerancePercent <= 0)
        {
            return false;
        }

        int tolerance = GetChannelTolerance(tolerancePercent);

        foreach (int candidate in sourceColors)
        {
            if (Math.Abs(r - ((candidate >> 16) & 0xFF)) <= tolerance
                && Math.Abs(g - ((candidate >> 8) & 0xFF)) <= tolerance
                && Math.Abs(b - (candidate & 0xFF)) <= tolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static int Pack(int r, int g, int b)
    {
        return (r << 16) | (g << 8) | b;
    }

    private static int GetChannelTolerance(double tolerancePercent)
    {
        return (int)Math.Ceiling(
            byte.MaxValue * Math.Min(tolerancePercent, 100.0) / 100.0);
    }
}
