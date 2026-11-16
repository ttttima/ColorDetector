using System.ComponentModel;
using System.IO.Ports;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal static class Program
{
static async Task Main()
{
string devicePath = FindCardputerMouse()
    ?? throw new InvalidOperationException(
        "Cardputer HID path was not found.");

Console.WriteLine($"HID: {devicePath}");

using SafeFileHandle handle = CreateFile(
    devicePath,
    GenericRead,
    FileShareRead | FileShareWrite,
    IntPtr.Zero,
    OpenExisting,
    FileFlagOverlapped,
    IntPtr.Zero);

if (handle.IsInvalid)
{
    throw new Win32Exception(Marshal.GetLastWin32Error());
}

using var hid = new FileStream(handle, FileAccess.Read, 64, true);
using var serial = new SerialPort("COM11", 115200)
{
    DtrEnable = true,
    RtsEnable = true,
    NewLine = "\n",
    ReadTimeout = 5000,
    WriteTimeout = 1000
};

var report = new byte[64];
Task<int> readTask = hid.ReadAsync(report).AsTask();

serial.Open();
serial.DiscardInBuffer();
serial.Write("CLICK\n");
Console.WriteLine($"Serial: {serial.ReadLine().Trim()}");

Task completed = await Task.WhenAny(
    readTask,
    Task.Delay(TimeSpan.FromSeconds(5)));

if (completed != readTask)
{
    Console.WriteLine("HID: no input report within 5 seconds.");
    return;
}

int count = await readTask;
Console.WriteLine(
    $"HID bytes ({count}): {Convert.ToHexString(report, 0, count)}");
}

static string? FindCardputerMouse()
{
    HidD_GetHidGuid(out Guid hidGuid);

    IntPtr infoSet = SetupDiGetClassDevs(
        ref hidGuid,
        null,
        IntPtr.Zero,
        DigcfPresent | DigcfDeviceInterface);

    if (infoSet == new IntPtr(-1))
    {
        throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    try
    {
        for (uint index = 0; ; index++)
        {
            var interfaceData = new DeviceInterfaceData
            {
                Size = Marshal.SizeOf<DeviceInterfaceData>()
            };

            if (!SetupDiEnumDeviceInterfaces(
                    infoSet,
                    IntPtr.Zero,
                    ref hidGuid,
                    index,
                    ref interfaceData))
            {
                if (Marshal.GetLastWin32Error() == ErrorNoMoreItems)
                {
                    return null;
                }

                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            uint requiredSize = 0;
            SetupDiGetDeviceInterfaceDetail(
                infoSet,
                ref interfaceData,
                IntPtr.Zero,
                0,
                ref requiredSize,
                IntPtr.Zero);

            IntPtr detail = Marshal.AllocHGlobal((int)requiredSize);
            try
            {
                Marshal.WriteInt32(
                    detail,
                    IntPtr.Size == 8 ? 8 : 6);

                if (!SetupDiGetDeviceInterfaceDetail(
                        infoSet,
                        ref interfaceData,
                        detail,
                        requiredSize,
                        ref requiredSize,
                        IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                string? path = Marshal.PtrToStringUni(
                    detail + 4);

                if (path?.Contains(
                        "vid_303a&pid_4031",
                        StringComparison.OrdinalIgnoreCase) == true)
                {
                    return path;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(detail);
            }
        }
    }
    finally
    {
        SetupDiDestroyDeviceInfoList(infoSet);
    }
}

const uint GenericRead = 0x80000000;
const uint FileShareRead = 0x00000001;
const uint FileShareWrite = 0x00000002;
const uint OpenExisting = 3;
const uint FileFlagOverlapped = 0x40000000;
const uint DigcfPresent = 0x00000002;
const uint DigcfDeviceInterface = 0x00000010;
const int ErrorNoMoreItems = 259;

[StructLayout(LayoutKind.Sequential)]
struct DeviceInterfaceData
{
    public int Size;
    public Guid InterfaceClassGuid;
    public int Flags;
    public IntPtr Reserved;
}

[DllImport("hid.dll")]
static extern void HidD_GetHidGuid(out Guid hidGuid);

[DllImport(
    "setupapi.dll",
    CharSet = CharSet.Unicode,
    SetLastError = true)]
static extern IntPtr SetupDiGetClassDevs(
    ref Guid classGuid,
    string? enumerator,
    IntPtr parent,
    uint flags);

[DllImport("setupapi.dll", SetLastError = true)]
static extern bool SetupDiEnumDeviceInterfaces(
    IntPtr deviceInfoSet,
    IntPtr deviceInfoData,
    ref Guid interfaceClassGuid,
    uint memberIndex,
    ref DeviceInterfaceData deviceInterfaceData);

[DllImport(
    "setupapi.dll",
    CharSet = CharSet.Unicode,
    SetLastError = true)]
static extern bool SetupDiGetDeviceInterfaceDetail(
    IntPtr deviceInfoSet,
    ref DeviceInterfaceData deviceInterfaceData,
    IntPtr detailData,
    uint detailDataSize,
    ref uint requiredSize,
    IntPtr deviceInfoData);

[DllImport("setupapi.dll")]
static extern bool SetupDiDestroyDeviceInfoList(
    IntPtr deviceInfoSet);

[DllImport(
    "kernel32.dll",
    CharSet = CharSet.Unicode,
    SetLastError = true)]
static extern SafeFileHandle CreateFile(
    string fileName,
    uint desiredAccess,
    uint shareMode,
    IntPtr securityAttributes,
    uint creationDisposition,
    uint flagsAndAttributes,
    IntPtr templateFile);
}
