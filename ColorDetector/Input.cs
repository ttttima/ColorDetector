public static class Input
{
    private static readonly CardputerClient Cardputer = new();

    public static void Initialize()
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                Cardputer.Connect();
                AppLog.Write("Cardputer connected.");
            }
            catch (Exception ex)
            {
                AppLog.Write($"Cardputer connection pending: {ex.Message}");
            }
        });
    }

    public static void LeftClick()
    {
        try
        {
            if (Cardputer.Click())
            {
                AppLog.Write("CLICK confirmed by Cardputer.");
            }
            else
            {
                AppLog.Write("Unexpected Cardputer response.");
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"Cardputer send failed: {ex.Message}");
        }
    }

    public static bool MoveRelative(int x, int y)
    {
        try
        {
            return Cardputer.Move(x, y);
        }
        catch (Exception ex)
        {
            AppLog.Write($"Cardputer move failed: {ex.Message}");
            return false;
        }
    }

    public static bool MoveStraight(
        int x,
        int y,
        int stepDelayMs,
        int maximumStepCounts)
    {
        try
        {
            return Cardputer.MoveLine(
                x,
                y,
                stepDelayMs,
                maximumStepCounts);
        }
        catch (Exception ex)
        {
            AppLog.Write($"Cardputer straight move failed: {ex.Message}");
            return false;
        }
    }
}
