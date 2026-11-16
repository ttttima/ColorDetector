public static class AppLog
{
    public static event Action<string>? MessageWritten;

    public static void Write(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        MessageWritten?.Invoke(line);
    }
}
