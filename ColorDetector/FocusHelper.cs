using System;
using System.Runtime.InteropServices;

public static class FocusHelper
{
    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    public static IntPtr GetActiveWindow()
    {
        return GetForegroundWindow();
    }
}