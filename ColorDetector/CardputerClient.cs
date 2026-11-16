using System.IO.Ports;
using Microsoft.Win32;

public sealed class CardputerClient : IDisposable
{
    private const int BaudRate = 115200;
    private readonly object sync = new();
    private SerialPort? serialPort;

    public void Connect()
    {
        lock (sync)
        {
            EnsureOpen();
        }
    }

    public bool Click()
    {
        lock (sync)
        {
            try
            {
                EnsureOpen();
                serialPort!.Write("CLICK\n");
                return serialPort.ReadLine().Trim() == "OK CLICK";
            }
            catch
            {
                ClosePort();
                throw;
            }
        }
    }

    public bool Move(int x, int y)
    {
        if (x == 0 && y == 0)
        {
            return true;
        }

        lock (sync)
        {
            try
            {
                EnsureOpen();
                serialPort!.Write($"MOVE {x} {y}\n");
                return serialPort.ReadLine().Trim() == "OK MOVE";
            }
            catch
            {
                ClosePort();
                throw;
            }
        }
    }

    public bool MoveLine(
        int x,
        int y,
        int stepDelayMs,
        int maximumStepCounts)
    {
        if (x == 0 && y == 0)
        {
            return true;
        }

        lock (sync)
        {
            try
            {
                EnsureOpen();
                serialPort!.Write(
                    $"LINE {x} {y} {Math.Clamp(stepDelayMs, 0, 20)} "
                    + $"{Math.Clamp(maximumStepCounts, 1, 127)}\n");
                return serialPort.ReadLine().Trim() == "OK LINE";
            }
            catch
            {
                ClosePort();
                throw;
            }
        }
    }

    private void EnsureOpen()
    {
        if (serialPort?.IsOpen == true)
        {
            return;
        }

        string portName = FindCardputerPort()
            ?? throw new InvalidOperationException(
                "Cardputer USB serial port was not found.");

        serialPort = new SerialPort(portName, BaudRate)
        {
            NewLine = "\n",
            ReadTimeout = 1000,
            WriteTimeout = 500,
            DtrEnable = true,
            RtsEnable = true
        };
        serialPort.Open();
        Thread.Sleep(100);
        serialPort.DiscardInBuffer();
    }

    private static string? FindCardputerPort()
    {
        const string usbDevicesPath =
            @"SYSTEM\CurrentControlSet\Enum\USB";

        using var usbDevices =
            Registry.LocalMachine.OpenSubKey(usbDevicesPath);
        if (usbDevices is null)
        {
            return null;
        }

        foreach (string deviceName in usbDevices.GetSubKeyNames())
        {
            if (!deviceName.StartsWith(
                    "VID_303A&",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var device = usbDevices.OpenSubKey(deviceName);
            if (device is null)
            {
                continue;
            }

            foreach (string instanceName in device.GetSubKeyNames())
            {
                using var parameters = device.OpenSubKey(
                    $@"{instanceName}\Device Parameters");
                if (parameters?.GetValue("PortName") is string portName
                    && SerialPort.GetPortNames().Contains(
                        portName,
                        StringComparer.OrdinalIgnoreCase))
                {
                    return portName;
                }
            }
        }

        return null;
    }

    private void ClosePort()
    {
        serialPort?.Dispose();
        serialPort = null;
    }

    public void Dispose()
    {
        lock (sync)
        {
            ClosePort();
        }
    }
}
