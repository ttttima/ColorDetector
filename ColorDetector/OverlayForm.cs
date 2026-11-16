using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

public sealed class OverlayForm : Form
{
    private const int VkLeftControl = 0xA2;

    private readonly int targetX;
    private readonly int targetY;
    private readonly DetectionSettings settings;
    private long lastTriggerTimestamp = -Stopwatch.Frequency;
    private volatile bool running = true;

    public OverlayForm(DetectionSettings settings)
    {
        this.settings = settings;
        FormBorderStyle = FormBorderStyle.None;
        Size = new System.Drawing.Size(1, 1);
        StartPosition = FormStartPosition.Manual;
        Location = new System.Drawing.Point(-32000, -32000);
        ShowInTaskbar = false;
        Opacity = 0;
        Enabled = false;

        targetX = ScreenCapture.GetScreenWidth() / 2;
        targetY = ScreenCapture.GetScreenHeight() / 2;

        AppLog.Write($"Detection point: {targetX}, {targetY}.");
        ColorStorage.LoadColors();
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        var detectorThread = new Thread(DetectionLoop)
        {
            IsBackground = true,
            Name = "ColorDetector",
            Priority = ThreadPriority.AboveNormal
        };
        detectorThread.Start();
    }

    private void DetectionLoop()
    {
        long nextSampleTimestamp = Stopwatch.GetTimestamp();
        bool previousControlHeld = false;
        bool toggleActive = false;
        bool previousToggleMode = settings.ToggleActivationMode;

        while (running)
        {
            if (!settings.DetectorEnabled)
            {
                toggleActive = false;
                previousControlHeld = IsLeftControlHeld();
                Thread.Sleep(10);
                nextSampleTimestamp = Stopwatch.GetTimestamp();
                continue;
            }

            bool controlHeld = IsLeftControlHeld();
            bool toggleMode = settings.ToggleActivationMode;

            if (toggleMode != previousToggleMode)
            {
                toggleActive = false;
                previousControlHeld = controlHeld;
                previousToggleMode = toggleMode;
            }
            else if (toggleMode && controlHeld && !previousControlHeld)
            {
                toggleActive = !toggleActive;
                AppLog.Write(
                    toggleActive
                        ? "Detector toggled ON."
                        : "Detector toggled OFF.");
            }

            previousControlHeld = controlHeld;
            bool detectorActive = toggleMode ? toggleActive : controlHeld;
            if (!detectorActive)
            {
                Thread.Sleep(1);
                nextSampleTimestamp = Stopwatch.GetTimestamp();
                continue;
            }

            try
            {
                var sample = ScreenCapture.SampleCircle(
                    targetX,
                    targetY,
                    settings.OverlayCircleSize);
                settings.SetObservedColor(sample.r, sample.g, sample.b);

                long now = Stopwatch.GetTimestamp();
                if (sample.matched
                    && now - lastTriggerTimestamp >= Stopwatch.Frequency)
                {
                    AppLog.Write("MATCH inside watched circle");
                    var snapBackTarget = SnapBack.GetCursorPosition();

                    int reactionTimeMs = settings.ReactionTimeMs;
                    if (reactionTimeMs > 0)
                    {
                        Thread.Sleep(reactionTimeMs);
                    }

                    bool stillActive = settings.DetectorEnabled
                        && (settings.ToggleActivationMode
                            ? toggleActive
                            : IsLeftControlHeld());
                    if (stillActive)
                    {
                        Input.LeftClick();
                        SnapBack.Start(
                            snapBackTarget,
                            settings.SnapBackDelayHalfMilliseconds,
                            settings.SnapBackTimeMultiplierPercent);
                        lastTriggerTimestamp = Stopwatch.GetTimestamp();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write($"Detection loop failed: {ex.Message}");
            }

            int fps = settings.DetectionFps;
            long interval = Math.Max(1, Stopwatch.Frequency / fps);
            nextSampleTimestamp += interval;

            long currentTimestamp = Stopwatch.GetTimestamp();
            if (nextSampleTimestamp <= currentTimestamp)
            {
                nextSampleTimestamp = currentTimestamp;
                continue;
            }

            int waitMs = (int)(
                (nextSampleTimestamp - currentTimestamp)
                * 1000
                / Stopwatch.Frequency);
            Thread.Sleep(Math.Max(1, waitMs));
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        running = false;
        base.OnFormClosing(e);
    }

    private static bool IsLeftControlHeld()
    {
        return (GetAsyncKeyState(VkLeftControl) & 0x8000) != 0;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
