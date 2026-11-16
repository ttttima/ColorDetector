using System.Diagnostics;
using System.Threading;

public sealed class DetectionSettings
{
    private const int DisplayNoiseThreshold = 16;
    private static readonly long DisplaySettleTicks =
        Stopwatch.Frequency / 10;

    private int reactionTimeMs;
    private int detectionFps = 1000;
    private int overlayCircleSize = 50;
    private int overlayCircleVisible = 1;
    private int snapBackDelayHalfMilliseconds = 4;
    private int snapBackTimeMultiplierPercent = 100;
    private int toggleActivationMode;
    private int detectorEnabled = 1;
    private int linearAimMode;
    private int previewCircleSize = 50;
    private int previewCircleVisibleOnScreen;
    private int finalDownPixels = 2;
    private int firstMovementDelayMs;
    private int secondMovementDelayMs;
    private int thirdMovementDelayMs;
    private int stickEnabled;
    private int stickSpeedPercent = 100;
    private int stickSmoothnessPercent = 75;
    private int stickPredictionEnabled = 1;
    private int stickStabilizationMethod = 1;
    private int stickVerticalDeadzonePixels = 5;
    private int aimPauseOnLeftClickEnabled;
    private int aimPauseOnLeftClickMilliseconds = 250;
    private int stickActivationVirtualKey = 0xA5;
    private int stickToggleActivationMode = 1;
    private int observedColor;
    private int candidateColor = -1;
    private long candidateSince;

    public int ReactionTimeMs
    {
        get => Volatile.Read(ref reactionTimeMs);
        set => Volatile.Write(ref reactionTimeMs, value);
    }

    public int DetectionFps
    {
        get => Volatile.Read(ref detectionFps);
        set => Volatile.Write(ref detectionFps, Math.Clamp(value, 30, 1000));
    }

    public int OverlayCircleSize
    {
        get => Volatile.Read(ref overlayCircleSize);
        set => Volatile.Write(ref overlayCircleSize, Math.Clamp(value, 1, 250));
    }

    public bool OverlayCircleVisible
    {
        get => Volatile.Read(ref overlayCircleVisible) != 0;
        set => Volatile.Write(ref overlayCircleVisible, value ? 1 : 0);
    }

    public int SnapBackDelayHalfMilliseconds
    {
        get => Volatile.Read(ref snapBackDelayHalfMilliseconds);
        set => Volatile.Write(
            ref snapBackDelayHalfMilliseconds,
            Math.Clamp(value, 1, 2000));
    }

    public int SnapBackTimeMultiplierPercent
    {
        get => Volatile.Read(ref snapBackTimeMultiplierPercent);
        set => Volatile.Write(
            ref snapBackTimeMultiplierPercent,
            Math.Clamp(value, 25, 400));
    }

    public bool ToggleActivationMode
    {
        get => Volatile.Read(ref toggleActivationMode) != 0;
        set => Volatile.Write(ref toggleActivationMode, value ? 1 : 0);
    }

    public bool DetectorEnabled
    {
        get => Volatile.Read(ref detectorEnabled) != 0;
        set => Volatile.Write(ref detectorEnabled, value ? 1 : 0);
    }

    public bool LinearAimMode
    {
        get => Volatile.Read(ref linearAimMode) != 0;
        set => Volatile.Write(ref linearAimMode, value ? 1 : 0);
    }

    public int PreviewCircleSize
    {
        get => Volatile.Read(ref previewCircleSize);
        set => Volatile.Write(
            ref previewCircleSize,
            Math.Clamp(value, 1, 1000));
    }

    public bool PreviewCircleVisibleOnScreen
    {
        get => Volatile.Read(ref previewCircleVisibleOnScreen) != 0;
        set => Volatile.Write(
            ref previewCircleVisibleOnScreen,
            value ? 1 : 0);
    }

    public int FinalDownPixels
    {
        get => Volatile.Read(ref finalDownPixels);
        set => Volatile.Write(
            ref finalDownPixels,
            Math.Clamp(value, 0, 50));
    }

    public int FirstMovementDelayMs
    {
        get => Volatile.Read(ref firstMovementDelayMs);
        set => Volatile.Write(
            ref firstMovementDelayMs,
            Math.Clamp(value, 0, 20));
    }

    public int SecondMovementDelayMs
    {
        get => Volatile.Read(ref secondMovementDelayMs);
        set => Volatile.Write(
            ref secondMovementDelayMs,
            Math.Clamp(value, 0, 20));
    }

    public int ThirdMovementDelayMs
    {
        get => Volatile.Read(ref thirdMovementDelayMs);
        set => Volatile.Write(
            ref thirdMovementDelayMs,
            Math.Clamp(value, 0, 20));
    }

    public bool StickEnabled
    {
        get => Volatile.Read(ref stickEnabled) != 0;
        set => Volatile.Write(ref stickEnabled, value ? 1 : 0);
    }

    public int StickSpeedPercent
    {
        get => Volatile.Read(ref stickSpeedPercent);
        set => Volatile.Write(
            ref stickSpeedPercent,
            Math.Clamp(value, 0, 100));
    }

    public int StickSmoothnessPercent
    {
        get => Volatile.Read(ref stickSmoothnessPercent);
        set => Volatile.Write(
            ref stickSmoothnessPercent,
            Math.Clamp(value, 0, 1000));
    }

    public bool StickPredictionEnabled
    {
        get => Volatile.Read(ref stickPredictionEnabled) != 0;
        set => Volatile.Write(
            ref stickPredictionEnabled,
            value ? 1 : 0);
    }

    public int StickStabilizationMethod
    {
        get => Volatile.Read(ref stickStabilizationMethod);
        set => Volatile.Write(
            ref stickStabilizationMethod,
            Math.Clamp(value, 1, 5));
    }

    public int StickVerticalDeadzonePixels
    {
        get => Volatile.Read(ref stickVerticalDeadzonePixels);
        set => Volatile.Write(
            ref stickVerticalDeadzonePixels,
            Math.Clamp(value, 0, 25));
    }

    public bool AimPauseOnLeftClickEnabled
    {
        get => Volatile.Read(ref aimPauseOnLeftClickEnabled) != 0;
        set => Volatile.Write(
            ref aimPauseOnLeftClickEnabled,
            value ? 1 : 0);
    }

    public int AimPauseOnLeftClickMilliseconds
    {
        get => Volatile.Read(ref aimPauseOnLeftClickMilliseconds);
        set => Volatile.Write(
            ref aimPauseOnLeftClickMilliseconds,
            Math.Clamp(value, 50, 2000));
    }

    public int StickActivationVirtualKey
    {
        get => Volatile.Read(ref stickActivationVirtualKey);
        set => Volatile.Write(
            ref stickActivationVirtualKey,
            Math.Clamp(value, 1, 255));
    }

    public bool StickToggleActivationMode
    {
        get => Volatile.Read(ref stickToggleActivationMode) != 0;
        set => Volatile.Write(
            ref stickToggleActivationMode,
            value ? 1 : 0);
    }

    public void SetObservedColor(byte r, byte g, byte b)
    {
        int packed = (r << 16) | (g << 8) | b;
        long now = Stopwatch.GetTimestamp();

        if (!IsNear(packed, candidateColor))
        {
            candidateColor = packed;
            candidateSince = now;
            return;
        }

        if (now - candidateSince >= DisplaySettleTicks
            && !IsNear(packed, Volatile.Read(ref observedColor)))
        {
            Volatile.Write(ref observedColor, candidateColor);
        }
    }

    public (byte r, byte g, byte b) GetObservedColor()
    {
        int packed = Volatile.Read(ref observedColor);
        return (
            (byte)((packed >> 16) & 0xFF),
            (byte)((packed >> 8) & 0xFF),
            (byte)(packed & 0xFF));
    }

    private static bool IsNear(int first, int second)
    {
        if (second < 0)
        {
            return false;
        }

        return Math.Abs(((first >> 16) & 0xFF) - ((second >> 16) & 0xFF))
                < DisplayNoiseThreshold
            && Math.Abs(((first >> 8) & 0xFF) - ((second >> 8) & 0xFF))
                < DisplayNoiseThreshold
            && Math.Abs((first & 0xFF) - (second & 0xFF))
                < DisplayNoiseThreshold;
    }
}
