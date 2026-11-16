using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

ApplicationConfiguration.Initialize();
Application.Run(new SnakeTestForm());

internal sealed class SnakeTestForm : Form
{
    private const int TargetY = 720;
    private const int SnakeWidth = 180;
    private const int SnakeHeight = 36;

    private readonly System.Windows.Forms.Timer timer = new()
    {
        Interval = 16
    };
    private readonly Button clickTarget = new();
    private readonly Label status = new();
    private readonly Label rawStatus = new();
    private readonly Color snakeColor = Color.FromArgb(255, 255, 53);

    private int clickCount;
    private int rawLeftDownCount;
    private int rawLeftUpCount;
    private bool cursorPositioned;

    public SnakeTestForm()
    {
        Text = "Disposable ColorDetector Snake Test";
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        BackColor = Color.FromArgb(35, 35, 40);
        TopMost = false;
        DoubleBuffered = true;
        KeyPreview = true;

        clickTarget.Text = "HARDWARE CLICK TARGET";
        clickTarget.Font = new Font("Segoe UI", 15, FontStyle.Bold);
        clickTarget.Size = new Size(320, 90);
        clickTarget.Location = new Point(80, 80);
        clickTarget.BackColor = Color.FromArgb(230, 230, 230);
        clickTarget.FlatStyle = FlatStyle.Flat;
        clickTarget.Click += (_, _) =>
        {
            clickCount++;
            clickTarget.Text = $"CLICK RECEIVED: {clickCount}";
            status.Text = $"PASS: Windows delivered {clickCount} click(s).";
            status.ForeColor = Color.LimeGreen;
        };

        status.AutoSize = true;
        status.Font = new Font("Consolas", 14, FontStyle.Bold);
        status.ForeColor = Color.White;
        status.Location = new Point(80, 190);
        status.Text =
            "Cursor will stay on the button. Snake #FFFF35 crosses (1280,720).";

        rawStatus.AutoSize = true;
        rawStatus.Font = new Font("Consolas", 12, FontStyle.Bold);
        rawStatus.ForeColor = Color.Cyan;
        rawStatus.Location = new Point(80, 225);
        rawStatus.Text = "Raw mouse: waiting for button packets.";

        Controls.Add(clickTarget);
        Controls.Add(status);
        Controls.Add(rawStatus);

        timer.Tick += (_, _) =>
        {
            if (!cursorPositioned && IsHandleCreated)
            {
                var target = clickTarget.PointToScreen(
                    new Point(clickTarget.Width / 2, clickTarget.Height / 2));
                Cursor.Position = target;
                cursorPositioned = true;
            }

            Invalidate();
        };

        Shown += (_, _) =>
        {
            Activate();
            timer.Start();
        };

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        var devices = new[]
        {
            new RawInputDevice
            {
                UsagePage = 0x01,
                Usage = 0x02,
                Flags = 0x00000100,
                Target = Handle
            }
        };

        if (!RegisterRawInputDevices(
                devices,
                (uint)devices.Length,
                (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            rawStatus.Text = $"Raw Input registration failed: {Marshal.GetLastWin32Error()}";
        }
    }

    protected override void WndProc(ref Message m)
    {
        const int WmInput = 0x00FF;
        const uint RidInput = 0x10000003;
        const ushort LeftDown = 0x0001;
        const ushort LeftUp = 0x0002;

        if (m.Msg == WmInput)
        {
            uint size = 0;
            var headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
            GetRawInputData(m.LParam, RidInput, IntPtr.Zero, ref size, headerSize);

            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(m.LParam, RidInput, buffer, ref size, headerSize) == size)
                {
                    var input = Marshal.PtrToStructure<RawInput>(buffer);
                    if (input.Header.Type == 0)
                    {
                        var flags = (ushort)(input.Mouse.Buttons & 0xFFFF);
                        if ((flags & LeftDown) != 0)
                        {
                            rawLeftDownCount++;
                        }

                        if ((flags & LeftUp) != 0)
                        {
                            rawLeftUpCount++;
                        }

                        if (flags != 0)
                        {
                            string deviceName = GetRawDeviceName(input.Header.Device);
                            rawStatus.Text =
                                $"Raw mouse: down={rawLeftDownCount}, up={rawLeftUpCount}, " +
                                $"flags=0x{flags:X4}, device={deviceName}";
                        }
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        base.WndProc(ref m);
    }

    private static string GetRawDeviceName(IntPtr device)
    {
        const uint RidiDeviceName = 0x20000007;
        uint characterCount = 0;
        GetRawInputDeviceInfo(
            device,
            RidiDeviceName,
            IntPtr.Zero,
            ref characterCount);

        if (characterCount == 0)
        {
            return $"0x{device.ToInt64():X}";
        }

        IntPtr buffer = Marshal.AllocHGlobal((int)characterCount * 2);
        try
        {
            if (GetRawInputDeviceInfo(
                    device,
                    RidiDeviceName,
                    buffer,
                    ref characterCount) == uint.MaxValue)
            {
                return $"0x{device.ToInt64():X}";
            }

            return Marshal.PtrToStringUni(buffer)
                ?? $"0x{device.ToInt64():X}";
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using var centerPen = new Pen(Color.White, 1);
        e.Graphics.DrawLine(centerPen, 1270, TargetY, 1290, TargetY);
        e.Graphics.DrawLine(centerPen, 1280, TargetY - 10, 1280, TargetY + 10);

        using var brush = new SolidBrush(snakeColor);
        int snakeX = 1280 - SnakeWidth / 2;
        e.Graphics.FillRectangle(
            brush,
            snakeX,
            TargetY - SnakeHeight / 2,
            SnakeWidth,
            SnakeHeight);

    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawMouse
    {
        public ushort Flags;
        public uint Buttons;
        public uint RawButtons;
        public int LastX;
        public int LastY;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInput
    {
        public RawInputHeader Header;
        public RawMouse Mouse;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(
        RawInputDevice[] devices,
        uint numberOfDevices,
        uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr rawInput,
        uint command,
        IntPtr data,
        ref uint size,
        uint headerSize);

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode,
        EntryPoint = "GetRawInputDeviceInfoW",
        SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr device,
        uint command,
        IntPtr data,
        ref uint size);
}
