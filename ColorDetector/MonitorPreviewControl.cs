using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public sealed class MonitorPreviewControl : Control
{
    private const int TargetFps = 500;
    private const int RequiredConnectedYellowPixels = 11;
    private const int PrecisionStepCounts = 64;
    private const int FastStepCounts = 127;
    private const double CalibratedHidCountsPerPixelX =
        1.0 / 0.3266162888329135;
    private const double CalibratedHidCountsPerPixelY =
        1.0 / 0.32684824902723736;
    private const int VkOem3 = 0xC0;
    private const int VkQ = 0x51;
    private const int VkLeftButton = 0x01;
    private readonly object frameSync = new();
    private static readonly object AimTelemetrySync = new();
    private readonly DetectionSettings settings;
    private Bitmap? frontFrame;
    private Bitmap? backFrame;
    private Bitmap? stickFrame;
    private int frontHitY = -1;
    private int backHitY = -1;
    private Point? frontContactPoint;
    private Point? backContactPoint;
    private CancellationTokenSource? captureCancellation;
    private Thread? captureThread;
    private int repaintQueued;
    private bool previousScanKeyHeld;
    private bool previousAimKeyHeld;
    private bool previousStickKeyHeld;
    private bool previousLeftButtonHeld;
    private bool scanActive;
    private int scanLineY = -1;
    private int lockedScanLineY = -1;
    private Point? scanContactPoint;
    private Point? currentAimTarget;
    private bool aimAfterScanRequested;
    private int aimGeneration;
    private int stickGeneration;
    private long aimPauseUntilTimestamp;
    private int captureSequence;
    private bool observedStickEnabled;
    private bool observedStickPredictionEnabled;
    private int observedStickStabilizationMethod;
    private bool hasSmoothedStickError;
    private double smoothedStickErrorX;
    private double smoothedStickErrorY;
    private bool hasPreviousStickFinalError;
    private bool hasLastStickFinalError;
    private Point previousStickFinalError;
    private Point lastStickFinalError;
    private bool hasLastSentStickFinalError;
    private Point lastSentStickFinalError;
    private bool hasOneEuroStickFinal;
    private double oneEuroX;
    private double oneEuroY;
    private double oneEuroDerivativeX;
    private double oneEuroDerivativeY;
    private long oneEuroLastTimestamp;
    private bool hasDoubleExpStickFinal;
    private double doubleExpLevelX;
    private double doubleExpLevelY;
    private double doubleExpTrendX;
    private double doubleExpTrendY;
    private readonly SemaphoreSlim movementSequence = new(1, 1);
    private int stickBindingCaptureRequested;
    private long stickBindingCaptureNotBefore;

    public event EventHandler? StickEnabledChanged;
    public event EventHandler? StickBindingChanged;

    public MonitorPreviewControl(DetectionSettings settings)
    {
        this.settings = settings;
        DoubleBuffered = true;
        BackColor = Color.Black;
        HandleCreated += (_, _) => StartCapture();
        HandleDestroyed += (_, _) => StopCapture();
    }

    private void StartCapture()
    {
        StopCapture();
        captureCancellation = new CancellationTokenSource();
        captureThread = new Thread(
            () => CaptureLoop(captureCancellation.Token))
        {
            IsBackground = true,
            Name = "MonitorPreview500Hz",
            Priority = ThreadPriority.AboveNormal
        };
        captureThread.Start();
    }

    private void StopCapture()
    {
        captureCancellation?.Cancel();
        if (captureThread?.IsAlive == true
            && Thread.CurrentThread != captureThread)
        {
            captureThread.Join(1000);
        }

        captureThread = null;
        captureCancellation?.Dispose();
        captureCancellation = null;
    }

    private void CaptureLoop(CancellationToken cancellationToken)
    {
        long frameInterval = Stopwatch.Frequency / TargetFps;
        long nextFrame = Stopwatch.GetTimestamp();

        while (!cancellationToken.IsCancellationRequested)
        {
            bool leftButtonHeld =
                (GetAsyncKeyState(VkLeftButton) & 0x8000) != 0;
            if (settings.AimPauseOnLeftClickEnabled
                && leftButtonHeld
                && !previousLeftButtonHeld)
            {
                PauseAimingForLeftClick();
            }

            previousLeftButtonHeld = leftButtonHeld;

            bool scanKeyHeld =
                (GetAsyncKeyState(VkOem3) & 0x8000) != 0;
            if (scanKeyHeld && !previousScanKeyHeld)
            {
                StartInstantScan();
            }

            previousScanKeyHeld = scanKeyHeld;
            bool aimKeyHeld = (GetAsyncKeyState(VkQ) & 0x8000) != 0;
            if (!IsAimingPaused()
                && aimKeyHeld
                && !previousAimKeyHeld)
            {
                aimAfterScanRequested = true;
                StartInstantScan();
                if (currentAimTarget is Point contact)
                {
                    aimAfterScanRequested = false;
                    StartStraightAdjustment(contact);
                }
            }

            previousAimKeyHeld = aimKeyHeld;
            if (Volatile.Read(ref stickBindingCaptureRequested) != 0
                && Stopwatch.GetTimestamp()
                    >= Volatile.Read(ref stickBindingCaptureNotBefore))
            {
                TryCaptureStickBinding();
            }

            bool stickKeyHeld = (GetAsyncKeyState(
                settings.StickActivationVirtualKey) & 0x8000) != 0;
            bool stickChanged = false;
            if (settings.StickToggleActivationMode)
            {
                if (stickKeyHeld && !previousStickKeyHeld)
                {
                    settings.StickEnabled = !settings.StickEnabled;
                    SettingsStorage.Save(settings);
                    stickChanged = true;
                }
            }
            else if (settings.StickEnabled != stickKeyHeld)
            {
                settings.StickEnabled = stickKeyHeld;
                stickChanged = true;
            }

            if (stickChanged)
            {
                AppLog.Write(settings.StickEnabled
                    ? "Stick targeting enabled."
                    : "Stick targeting disabled.");
                StickEnabledChanged?.Invoke(this, EventArgs.Empty);
            }
            previousStickKeyHeld = stickKeyHeld;
            bool predictionEnabled = settings.StickPredictionEnabled;
            int stabilizationMethod = settings.StickStabilizationMethod;
            if (predictionEnabled != observedStickPredictionEnabled
                || stabilizationMethod != observedStickStabilizationMethod)
            {
                observedStickPredictionEnabled = predictionEnabled;
                observedStickStabilizationMethod = stabilizationMethod;
                ResetStickPrediction();
            }

            bool stickEnabled = settings.StickEnabled && !IsAimingPaused();
            if (stickEnabled != observedStickEnabled)
            {
                observedStickEnabled = stickEnabled;
                if (stickEnabled)
                {
                    StartStickTracking();
                }
                else
                {
                    Interlocked.Increment(ref stickGeneration);
                    hasSmoothedStickError = false;
                    ResetStickPrediction();
                }
            }

            long now = Stopwatch.GetTimestamp();
            if (now < nextFrame)
            {
                WaitUntil(nextFrame, cancellationToken);
                continue;
            }

            CaptureMonitor();
            nextFrame += frameInterval;
            if (nextFrame <= Stopwatch.GetTimestamp())
            {
                nextFrame = Stopwatch.GetTimestamp() + frameInterval;
            }
        }
    }

    public void BeginStickBindingCapture()
    {
        Volatile.Write(
            ref stickBindingCaptureNotBefore,
            Stopwatch.GetTimestamp() + Stopwatch.Frequency / 5);
        Volatile.Write(ref stickBindingCaptureRequested, 1);
    }

    public static string GetBindingDisplayName(int virtualKey)
    {
        return virtualKey switch
        {
            0x01 => "LEFT MOUSE",
            0x02 => "RIGHT MOUSE",
            0x04 => "MIDDLE MOUSE",
            0x05 => "MOUSE 4",
            0x06 => "MOUSE 5",
            0xA0 => "LEFT SHIFT",
            0xA1 => "RIGHT SHIFT",
            0xA2 => "LEFT CTRL",
            0xA3 => "RIGHT CTRL",
            0xA4 => "LEFT ALT",
            0xA5 => "RIGHT ALT",
            _ => ((Keys)virtualKey).ToString().ToUpperInvariant()
        };
    }

    private void PauseAimingForLeftClick()
    {
        long pauseTicks = Math.Max(
            1,
            (long)(Stopwatch.Frequency
                * settings.AimPauseOnLeftClickMilliseconds
                / 1000.0));
        long until = Stopwatch.GetTimestamp() + pauseTicks;
        Volatile.Write(ref aimPauseUntilTimestamp, until);
        aimAfterScanRequested = false;
        scanActive = false;
        Interlocked.Increment(ref aimGeneration);
        Interlocked.Increment(ref stickGeneration);
        ResetStickPrediction();
    }

    private bool IsAimingPaused()
    {
        return Stopwatch.GetTimestamp()
            < Volatile.Read(ref aimPauseUntilTimestamp);
    }

    private void TryCaptureStickBinding()
    {
        ReadOnlySpan<int> mouseButtons = [0x01, 0x02, 0x04, 0x05, 0x06];
        foreach (int virtualKey in mouseButtons)
        {
            if ((GetAsyncKeyState(virtualKey) & 0x8000) != 0)
            {
                CompleteStickBindingCapture(virtualKey);
                return;
            }
        }

        ReadOnlySpan<int> modifiers = [0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5];
        foreach (int virtualKey in modifiers)
        {
            if ((GetAsyncKeyState(virtualKey) & 0x8000) != 0)
            {
                CompleteStickBindingCapture(virtualKey);
                return;
            }
        }

        for (int virtualKey = 8; virtualKey <= 255; virtualKey++)
        {
            if (virtualKey is 0x10 or 0x11 or 0x12
                || virtualKey == VkQ
                || virtualKey == VkOem3)
            {
                continue;
            }

            if ((GetAsyncKeyState(virtualKey) & 0x8000) != 0)
            {
                CompleteStickBindingCapture(virtualKey);
                return;
            }
        }
    }

    private void CompleteStickBindingCapture(int virtualKey)
    {
        settings.StickActivationVirtualKey = virtualKey;
        previousStickKeyHeld = true;
        Volatile.Write(ref stickBindingCaptureRequested, 0);
        SettingsStorage.Save(settings);
        AppLog.Write(
            $"Stick bind set to {GetBindingDisplayName(virtualKey)}.");
        StickBindingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void StartInstantScan()
    {
        Volatile.Write(ref lockedScanLineY, -1);
        scanContactPoint = null;
        currentAimTarget = null;

        lock (frameSync)
        {
            if (frontFrame is not null)
            {
                Point center = new(
                    frontFrame.Width / 2,
                    frontFrame.Height / 2);
                Point? contact = settings.LinearAimMode
                    ? FindClosestQualifiedTarget(frontFrame, center)
                    : ScanRowsIndividually(
                        frontFrame,
                        0,
                        frontFrame.Height);
                if (contact is Point hit)
                {
                    scanLineY = hit.Y;
                    Volatile.Write(
                        ref lockedScanLineY,
                        settings.LinearAimMode ? -1 : hit.Y);
                    scanContactPoint = hit;
                    currentAimTarget = hit;
                    frontHitY = settings.LinearAimMode ? -1 : hit.Y;
                    frontContactPoint = hit;
                }
                else
                {
                    scanLineY = -1;
                    frontHitY = -1;
                    frontContactPoint = null;
                }

                scanActive = false;
                return;
            }
        }

        scanLineY = 0;
        scanActive = true;
    }

    private void CaptureMonitor()
    {
        Rectangle bounds = Screen.PrimaryScreen?.Bounds ?? Rectangle.Empty;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        if (backFrame?.Size != bounds.Size)
        {
            backFrame?.Dispose();
            backFrame = new Bitmap(
                bounds.Width,
                bounds.Height,
                PixelFormat.Format32bppArgb);
        }

        using (Graphics graphics = Graphics.FromImage(backFrame))
        {
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        }

        KeepOnlySavedColors(backFrame);
        int rowsToScan = scanActive
            ? GetRowsToScan(backFrame.Height)
            : 0;
        Point? scanHit = rowsToScan > 0
            ? ScanRowsIndividually(backFrame, scanLineY, rowsToScan)
            : null;
        backHitY = scanActive
            ? scanLineY
            : Volatile.Read(ref lockedScanLineY);
        backContactPoint = scanContactPoint;
        if (scanActive)
        {
            if (scanHit is Point contact)
            {
                backHitY = contact.Y;
                scanLineY = contact.Y;
                Volatile.Write(ref lockedScanLineY, contact.Y);
                scanContactPoint = contact;
                currentAimTarget = contact;
                backContactPoint = contact;
                scanActive = false;
                if (aimAfterScanRequested)
                {
                    aimAfterScanRequested = false;
                    StartStraightAdjustment(contact);
                }
            }
            else if ((scanLineY += rowsToScan) >= backFrame.Height)
            {
                scanLineY = -1;
                scanActive = false;
            }
        }

        lock (frameSync)
        {
            (frontFrame, backFrame) = (backFrame, frontFrame);
            (frontHitY, backHitY) = (backHitY, frontHitY);
            (frontContactPoint, backContactPoint) =
                (backContactPoint, frontContactPoint);
        }
        Interlocked.Increment(ref captureSequence);

        QueueRepaint();
    }

    private int GetRowsToScan(int screenHeight)
    {
        return Math.Max(0, screenHeight - scanLineY);
    }

    private void QueueRepaint()
    {
        if (Interlocked.Exchange(ref repaintQueued, 1) != 0
            || !IsHandleCreated
            || IsDisposed)
        {
            return;
        }

        try
        {
            BeginInvoke(() =>
            {
                Interlocked.Exchange(ref repaintQueued, 0);
                Invalidate();
            });
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref repaintQueued, 0);
        }
    }

    private void StartStraightAdjustment(Point contact)
    {
        Rectangle bounds = Screen.PrimaryScreen?.Bounds ?? Rectangle.Empty;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        int centerX = bounds.Width / 2;
        int centerY = bounds.Height / 2;
        IssueVisualMove(new Point(
            contact.X - centerX,
            contact.Y - centerY));
    }

    private void IssueVisualMove(Point error)
    {
        int generation = Interlocked.Increment(ref aimGeneration);
        int moveX = (int)Math.Round(
            error.X * CalibratedHidCountsPerPixelX);
        int moveY = (int)Math.Round(
            error.Y * CalibratedHidCountsPerPixelY);
        if (moveX == 0 && error.X != 0)
        {
            moveX = Math.Sign(error.X);
        }

        if (moveY == 0 && error.Y != 0)
        {
            moveY = Math.Sign(error.Y);
        }

        AppLog.Write(
            $"Preview offset from center: {error.X}, {error.Y}; "
            + "Cardputer command: "
            + $"{moveX}, {moveY}.");
        WriteAimCommandTelemetry(error, moveX, moveY);
        _ = Task.Run(() =>
        {
            movementSequence.Wait();
            try
            {
                if (!MoveStraight(
                        moveX,
                        moveY,
                        generation,
                        FastStepCounts,
                        settings.FirstMovementDelayMs))
                {
                    return;
                }

                RunRingAndDown(generation, false);
            }
            finally
            {
                movementSequence.Release();
            }
        });
    }

    private void StartStickTracking()
    {
        int generation = Interlocked.Increment(ref stickGeneration);
        _ = Task.Run(() =>
        {
            while (settings.StickEnabled
                && generation == Volatile.Read(ref stickGeneration))
            {
                movementSequence.Wait();
                try
                {
                    if (!settings.StickEnabled
                        || generation != Volatile.Read(ref stickGeneration))
                    {
                        return;
                    }

                    RunRingAndDown(generation, true);
                }
                finally
                {
                    movementSequence.Release();
                }
            }
        });
    }

    private bool RunRingAndDown(int generation, bool stickMode)
    {
        Point? highest;
        if (stickMode)
        {
            highest = CaptureHighestMatchInsidePreviewCircle();
        }
        else
        {
            int sequence = Volatile.Read(ref captureSequence);
            WaitForFreshCaptures(sequence, 2, generation);
            highest = GetHighestMatchInsidePreviewCircle();
        }

        bool active = stickMode
            ? settings.StickEnabled
                && !IsAimingPaused()
                && generation == Volatile.Read(ref stickGeneration)
            : !IsAimingPaused()
                && generation == Volatile.Read(ref aimGeneration);
        if (highest is not Point target || !active)
        {
            if (!stickMode)
            {
                AppLog.Write(
                    "No saved-color pixel found inside preview ring.");
            }
            return false;
        }

        Rectangle bounds = Screen.PrimaryScreen?.Bounds ?? Rectangle.Empty;
        var secondError = new Point(
            target.X - bounds.Width / 2,
            target.Y - bounds.Height / 2);
        if (stickMode && !settings.StickPredictionEnabled)
        {
            secondError = SmoothStickError(secondError);
        }

        int downPixels = settings.FinalDownPixels;
        var measuredFinalError = new Point(
            secondError.X,
            secondError.Y + downPixels);
        if (stickMode && settings.StickPredictionEnabled)
        {
            return RunStabilizedStickFinalMove(
                measuredFinalError,
                generation);
        }

        Point finalError = measuredFinalError;
        if (stickMode)
        {
            finalError = PredictStickFinalError(finalError);
            secondError = new Point(
                finalError.X,
                finalError.Y - downPixels);
        }

        int finalVerticalError = secondError.Y + downPixels;
        bool suppressStickY = stickMode
            && Math.Abs(finalVerticalError)
                <= settings.StickVerticalDeadzonePixels;
        if (suppressStickY)
        {
            int horizontalOnly = ToHidCount(
                secondError.X,
                CalibratedHidCountsPerPixelX);
            bool horizontalMoved = horizontalOnly == 0
                || MoveStickStraight(horizontalOnly, 0, generation);
            if (horizontalMoved && stickMode)
            {
                RecordStickFinalError(measuredFinalError);
            }
            return horizontalMoved;
        }

        int secondX = ToHidCount(secondError.X, CalibratedHidCountsPerPixelX);
        int secondY = ToHidCount(secondError.Y, CalibratedHidCountsPerPixelY);
        if (!stickMode)
        {
            AppLog.Write(
                $"Ring top match {target.X}, {target.Y}; second command "
                + $"{secondX}, {secondY}.");
        }
        bool moved = stickMode
            ? MoveStickStraight(
                secondX,
                secondY,
                generation)
            : MoveStraight(
                secondX,
                secondY,
                generation,
                PrecisionStepCounts,
                settings.SecondMovementDelayMs);
        if (!moved)
        {
            return false;
        }

        if (downPixels <= 0)
        {
            if (stickMode)
            {
                RecordStickFinalError(measuredFinalError);
            }
            return true;
        }

        int downCommand = Math.Max(
            1,
            (int)Math.Round(downPixels * CalibratedHidCountsPerPixelY));
        if (!stickMode)
        {
            AppLog.Write(
                $"Final down {downPixels} px; command 0, {downCommand}.");
        }
        bool downMoved = stickMode
            ? MoveStickStraight(
                0,
                downCommand,
                generation)
            : MoveStraight(
                0,
                downCommand,
                generation,
                PrecisionStepCounts,
                settings.ThirdMovementDelayMs);
        if (downMoved && stickMode)
        {
            RecordStickFinalError(measuredFinalError);
        }
        return downMoved;
    }

    private bool RunStabilizedStickFinalMove(
        Point measuredFinalError,
        int generation)
    {
        Point finalError = settings.StickStabilizationMethod switch
        {
            1 => FilterOneEuroStickFinal(measuredFinalError),
            2 => measuredFinalError,
            3 => ApplyStickDeadband(measuredFinalError),
            4 => measuredFinalError,
            5 => FilterDoubleExponentialStickFinal(measuredFinalError),
            _ => measuredFinalError
        };

        int moveX = ToHidCount(finalError.X, CalibratedHidCountsPerPixelX);
        int moveY = Math.Abs(finalError.Y)
            <= settings.StickVerticalDeadzonePixels
                ? 0
                : ToHidCount(finalError.Y, CalibratedHidCountsPerPixelY);

        bool moved = settings.StickStabilizationMethod == 4
            ? MoveStickMinimumJerk(moveX, moveY, generation)
            : MoveStickStraight(moveX, moveY, generation);
        if (moved)
        {
            RecordStickFinalError(measuredFinalError);
            lastSentStickFinalError = finalError;
            hasLastSentStickFinalError = true;
        }

        return moved;
    }

    private Point ApplyStickDeadband(Point measuredFinalError)
    {
        if (!hasLastSentStickFinalError)
        {
            return measuredFinalError;
        }

        int deltaX = measuredFinalError.X - lastSentStickFinalError.X;
        int deltaY = measuredFinalError.Y - lastSentStickFinalError.Y;
        int threshold = Math.Max(
            2,
            Math.Min(5, settings.PreviewCircleSize / 18));
        return deltaX * deltaX + deltaY * deltaY <= threshold * threshold
            ? Point.Empty
            : measuredFinalError;
    }

    private Point FilterOneEuroStickFinal(Point measuredFinalError)
    {
        long now = Stopwatch.GetTimestamp();
        if (!hasOneEuroStickFinal)
        {
            oneEuroX = measuredFinalError.X;
            oneEuroY = measuredFinalError.Y;
            oneEuroDerivativeX = 0;
            oneEuroDerivativeY = 0;
            oneEuroLastTimestamp = now;
            hasOneEuroStickFinal = true;
            return measuredFinalError;
        }

        double elapsed = Math.Max(
            0.001,
            (now - oneEuroLastTimestamp) / (double)Stopwatch.Frequency);
        oneEuroLastTimestamp = now;

        double rawDerivativeX = (measuredFinalError.X - oneEuroX) / elapsed;
        double rawDerivativeY = (measuredFinalError.Y - oneEuroY) / elapsed;
        const double DerivativeCutoff = 1.0;
        double derivativeAlpha = LowPassAlpha(elapsed, DerivativeCutoff);
        oneEuroDerivativeX = LowPass(
            oneEuroDerivativeX,
            rawDerivativeX,
            derivativeAlpha);
        oneEuroDerivativeY = LowPass(
            oneEuroDerivativeY,
            rawDerivativeY,
            derivativeAlpha);

        double speed = Math.Sqrt(
            oneEuroDerivativeX * oneEuroDerivativeX
            + oneEuroDerivativeY * oneEuroDerivativeY);
        double smoothness = settings.StickSmoothnessPercent / 1000.0;
        double minCutoff = 0.35 + (1.0 - smoothness) * 2.2;
        double beta = 0.002 + (1.0 - smoothness) * 0.018;
        double cutoff = minCutoff + beta * speed;
        double alpha = LowPassAlpha(elapsed, cutoff);
        oneEuroX = LowPass(oneEuroX, measuredFinalError.X, alpha);
        oneEuroY = LowPass(oneEuroY, measuredFinalError.Y, alpha);
        return new Point(
            (int)Math.Round(oneEuroX),
            (int)Math.Round(oneEuroY));
    }

    private Point FilterDoubleExponentialStickFinal(Point measuredFinalError)
    {
        double smoothness = settings.StickSmoothnessPercent / 1000.0;
        double alpha = 0.65 - smoothness * 0.5;
        double gamma = 0.35 - smoothness * 0.25;
        alpha = Math.Clamp(alpha, 0.12, 0.65);
        gamma = Math.Clamp(gamma, 0.05, 0.35);

        if (!hasDoubleExpStickFinal)
        {
            doubleExpLevelX = measuredFinalError.X;
            doubleExpLevelY = measuredFinalError.Y;
            doubleExpTrendX = 0;
            doubleExpTrendY = 0;
            hasDoubleExpStickFinal = true;
            return measuredFinalError;
        }

        double previousLevelX = doubleExpLevelX;
        double previousLevelY = doubleExpLevelY;
        doubleExpLevelX =
            alpha * measuredFinalError.X
            + (1.0 - alpha) * (doubleExpLevelX + doubleExpTrendX);
        doubleExpLevelY =
            alpha * measuredFinalError.Y
            + (1.0 - alpha) * (doubleExpLevelY + doubleExpTrendY);
        doubleExpTrendX =
            gamma * (doubleExpLevelX - previousLevelX)
            + (1.0 - gamma) * doubleExpTrendX;
        doubleExpTrendY =
            gamma * (doubleExpLevelY - previousLevelY)
            + (1.0 - gamma) * doubleExpTrendY;

        return new Point(
            (int)Math.Round(doubleExpLevelX + doubleExpTrendX),
            (int)Math.Round(doubleExpLevelY + doubleExpTrendY));
    }

    private static double LowPass(
        double previous,
        double current,
        double alpha)
    {
        return previous + (current - previous) * alpha;
    }

    private static double LowPassAlpha(double elapsedSeconds, double cutoff)
    {
        double tau = 1.0 / (2.0 * Math.PI * Math.Max(0.001, cutoff));
        return 1.0 / (1.0 + tau / elapsedSeconds);
    }

    private static int ToHidCount(int pixels, double countsPerPixel)
    {
        int result = (int)Math.Round(pixels * countsPerPixel);
        return result == 0 && pixels != 0 ? Math.Sign(pixels) : result;
    }

    private Point SmoothStickError(Point error)
    {
        int smoothness = settings.StickSmoothnessPercent;
        if (smoothness <= 0)
        {
            smoothedStickErrorX = error.X;
            smoothedStickErrorY = error.Y;
            hasSmoothedStickError = true;
            return error;
        }

        if (!hasSmoothedStickError)
        {
            smoothedStickErrorX = error.X;
            smoothedStickErrorY = error.Y;
            hasSmoothedStickError = true;
            return error;
        }

        double alpha = 1.0 / (1.0 + smoothness / 100.0);
        smoothedStickErrorX += (error.X - smoothedStickErrorX) * alpha;
        smoothedStickErrorY += (error.Y - smoothedStickErrorY) * alpha;

        if (Math.Abs(error.X - smoothedStickErrorX) < 1.0)
        {
            smoothedStickErrorX = error.X;
        }

        if (Math.Abs(error.Y - smoothedStickErrorY) < 1.0)
        {
            smoothedStickErrorY = error.Y;
        }

        return new Point(
            (int)Math.Round(smoothedStickErrorX),
            (int)Math.Round(smoothedStickErrorY));
    }

    private Point PredictStickFinalError(Point detectedFinalError)
    {
        if (!settings.StickPredictionEnabled
            || !hasPreviousStickFinalError
            || !hasLastStickFinalError)
        {
            return detectedFinalError;
        }

        int deltaX = lastStickFinalError.X - previousStickFinalError.X;
        int deltaY = lastStickFinalError.Y - previousStickFinalError.Y;
        int maximumVelocity = Math.Max(
            2,
            Math.Min(10, settings.PreviewCircleSize / 8));
        deltaX = Math.Clamp(deltaX, -maximumVelocity, maximumVelocity);
        deltaY = Math.Clamp(deltaY, -maximumVelocity, maximumVelocity);

        var predicted = new Point(
            lastStickFinalError.X + deltaX,
            lastStickFinalError.Y + deltaY);

        int differenceX = predicted.X - detectedFinalError.X;
        int differenceY = predicted.Y - detectedFinalError.Y;
        double distance = Math.Sqrt(
            differenceX * differenceX + differenceY * differenceY);
        double maximumTrustDistance = Math.Max(
            16,
            settings.PreviewCircleSize * 0.45);
        if (distance > maximumTrustDistance)
        {
            ResetStickPrediction();
            return detectedFinalError;
        }

        double offsetX = differenceX;
        double offsetY = differenceY;
        double offsetDistance = Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
        double maximumOffset = Math.Max(
            6,
            Math.Min(18, settings.PreviewCircleSize * 0.25));
        if (offsetDistance > maximumOffset)
        {
            double scale = maximumOffset / offsetDistance;
            offsetX *= scale;
            offsetY *= scale;
        }

        return new Point(
            (int)Math.Round(detectedFinalError.X + offsetX),
            (int)Math.Round(detectedFinalError.Y + offsetY));
    }

    private void RecordStickFinalError(Point finalError)
    {
        if (hasLastStickFinalError)
        {
            previousStickFinalError = lastStickFinalError;
            hasPreviousStickFinalError = true;
        }

        lastStickFinalError = finalError;
        hasLastStickFinalError = true;
    }

    private void ResetStickPrediction()
    {
        hasPreviousStickFinalError = false;
        hasLastStickFinalError = false;
        previousStickFinalError = Point.Empty;
        lastStickFinalError = Point.Empty;
        hasLastSentStickFinalError = false;
        lastSentStickFinalError = Point.Empty;
        hasOneEuroStickFinal = false;
        oneEuroX = 0;
        oneEuroY = 0;
        oneEuroDerivativeX = 0;
        oneEuroDerivativeY = 0;
        oneEuroLastTimestamp = 0;
        hasDoubleExpStickFinal = false;
        doubleExpLevelX = 0;
        doubleExpLevelY = 0;
        doubleExpTrendX = 0;
        doubleExpTrendY = 0;
    }

    private void WaitForFreshCaptures(
        int startingSequence,
        int count,
        int generation)
    {
        int targetSequence = startingSequence + count;
        long deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 2;
        while (generation == Volatile.Read(ref aimGeneration)
            && Volatile.Read(ref captureSequence) < targetSequence
            && Stopwatch.GetTimestamp() < deadline)
        {
            Thread.Sleep(1);
        }
    }

    private Point? CaptureHighestMatchInsidePreviewCircle()
    {
        Rectangle screen = Screen.PrimaryScreen?.Bounds ?? Rectangle.Empty;
        if (screen.Width <= 0 || screen.Height <= 0)
        {
            return null;
        }

        int diameter = Math.Clamp(
            settings.PreviewCircleSize,
            1,
            Math.Min(screen.Width, screen.Height));
        int left = screen.Left + (screen.Width - diameter) / 2;
        int top = screen.Top + (screen.Height - diameter) / 2;
        var captureBounds = new Rectangle(left, top, diameter, diameter);
        if (stickFrame?.Size != captureBounds.Size)
        {
            stickFrame?.Dispose();
            stickFrame = new Bitmap(
                captureBounds.Width,
                captureBounds.Height,
                PixelFormat.Format32bppArgb);
        }

        using (Graphics graphics = Graphics.FromImage(stickFrame))
        {
            graphics.CopyFromScreen(
                captureBounds.Location,
                Point.Empty,
                captureBounds.Size);
        }

        Point? local = FindHighestSavedColorInsideCircle(stickFrame);
        return local is Point match
            ? new Point(captureBounds.Left + match.X, captureBounds.Top + match.Y)
            : null;
    }

    private Point? GetHighestMatchInsidePreviewCircle()
    {
        lock (frameSync)
        {
            return frontFrame is null
                ? null
                : FindHighestMatchInsideCircle(
                    frontFrame,
                    settings.PreviewCircleSize);
        }
    }

    private void WriteAimCommandTelemetry(
        Point error,
        int commandX,
        int commandY)
    {
        try
        {
            Rectangle bounds = Screen.PrimaryScreen?.Bounds
                ?? Rectangle.Empty;
            int centerX = bounds.Width / 2;
            int centerY = bounds.Height / 2;
            string directory = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "ColorDetector",
                "Telemetry");
            string path = Path.Combine(directory, "aim-commands.csv");

            lock (AimTelemetrySync)
            {
                Directory.CreateDirectory(directory);
                if (!File.Exists(path))
                {
                    File.AppendAllText(
                        path,
                        "utc,mode,target_x,target_y,center_x,center_y,"
                        + "error_x,error_y,hid_x,hid_y\n");
                }

                File.AppendAllText(
                    path,
                    $"{DateTime.UtcNow:O},"
                    + $"{(settings.LinearAimMode ? "linear" : "horizontal")},"
                    + $"{centerX + error.X},{centerY + error.Y},"
                    + $"{centerX},{centerY},{error.X},{error.Y},"
                    + $"{commandX},{commandY}\n");
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"Aim telemetry failed: {ex.Message}");
        }
    }

    private bool MoveStraight(
        int totalX,
        int totalY,
        int generation,
        int maximumStepCounts,
        int stepDelayMs)
    {
        if (generation != Volatile.Read(ref aimGeneration))
        {
            return false;
        }

        if (IsAimingPaused())
        {
            return false;
        }

        if (!Input.MoveStraight(
                totalX,
                totalY,
                stepDelayMs,
                maximumStepCounts))
        {
            AppLog.Write("Cardputer straight adjustment failed.");
            return false;
        }

        AppLog.Write(
            $"Cardputer adjusted {totalX}, {totalY} using calibrated "
            + "raw-camera gains.");
        return true;
    }

    private bool MoveStickStraight(
        int totalX,
        int totalY,
        int generation)
    {
        if (!settings.StickEnabled
            || IsAimingPaused()
            || generation != Volatile.Read(ref stickGeneration))
        {
            return false;
        }

        int speed = settings.StickSpeedPercent;
        int stepDelayMs = (int)Math.Round((100 - speed) * 5.0 / 100.0);
        int actualSteps = 8 + (int)Math.Round(
            (100 - speed) * 56.0 / 100.0);
        int maximumDistance = Math.Max(Math.Abs(totalX), Math.Abs(totalY));
        int maximumStepCounts = Math.Max(
            1,
            (maximumDistance + actualSteps - 1) / actualSteps);
        if (!Input.MoveStraight(
                totalX,
                totalY,
                stepDelayMs,
                maximumStepCounts))
        {
            return false;
        }
        return true;
    }

    private bool MoveStickMinimumJerk(
        int totalX,
        int totalY,
        int generation)
    {
        if (!settings.StickEnabled
            || IsAimingPaused()
            || generation != Volatile.Read(ref stickGeneration))
        {
            return false;
        }

        if (totalX == 0 && totalY == 0)
        {
            return true;
        }

        int speed = settings.StickSpeedPercent;
        int segments = 8 + (int)Math.Round(
            settings.StickSmoothnessPercent * 16.0 / 1000.0);
        int stepDelayMs = (int)Math.Round((100 - speed) * 2.0 / 100.0);
        int previousX = 0;
        int previousY = 0;
        for (int segment = 1; segment <= segments; segment++)
        {
            if (!settings.StickEnabled
                || IsAimingPaused()
                || generation != Volatile.Read(ref stickGeneration))
            {
                return false;
            }

            double t = segment / (double)segments;
            double s = t * t * t * (10.0 + t * (-15.0 + 6.0 * t));
            int targetX = (int)Math.Round(totalX * s);
            int targetY = (int)Math.Round(totalY * s);
            int deltaX = targetX - previousX;
            int deltaY = targetY - previousY;
            previousX = targetX;
            previousY = targetY;

            if (deltaX == 0 && deltaY == 0)
            {
                continue;
            }

            if (!Input.MoveStraight(deltaX, deltaY, stepDelayMs, 127))
            {
                return false;
            }
        }

        return true;
    }

    private static unsafe void KeepOnlySavedColors(Bitmap bitmap)
    {
        var rectangle = new Rectangle(Point.Empty, bitmap.Size);
        BitmapData data = bitmap.LockBits(
            rectangle,
            ImageLockMode.ReadWrite,
            PixelFormat.Format32bppArgb);

        try
        {
            for (int y = 0; y < data.Height; y++)
            {
                byte* row = (byte*)data.Scan0 + y * data.Stride;
                for (int x = 0; x < data.Width; x++)
                {
                    byte* pixel = row + x * 4;
                    if (!ColorStorage.ContainsExact(
                            pixel[2],
                            pixel[1],
                            pixel[0]))
                    {
                        pixel[0] = 0;
                        pixel[1] = 0;
                        pixel[2] = 0;
                    }
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

    }

    private static unsafe Point? ScanRowsIndividually(
        Bitmap bitmap,
        int firstRow,
        int rowCount)
    {
        var rectangle = new Rectangle(Point.Empty, bitmap.Size);
        BitmapData data = bitmap.LockBits(
            rectangle,
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            int finalRow = Math.Min(
                data.Height,
                firstRow + rowCount);
            for (int y = firstRow; y < finalRow; y++)
            {
                byte* row = (byte*)data.Scan0 + y * data.Stride;
                for (int x = 0; x < data.Width; x++)
                {
                    byte* pixel = row + x * 4;
                    if ((pixel[0] | pixel[1] | pixel[2]) != 0
                        && HasRequiredYellowCluster(data, x, y))
                    {
                        return new Point(x, y);
                    }
                }
            }

            return null;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static unsafe Point? FindTopmostQualifiedTarget(Bitmap bitmap)
    {
        var rectangle = new Rectangle(Point.Empty, bitmap.Size);
        BitmapData data = bitmap.LockBits(
            rectangle,
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            for (int y = 0; y < data.Height; y++)
            {
                byte* row = (byte*)data.Scan0 + y * data.Stride;
                for (int x = 0; x < data.Width; x++)
                {
                    byte* pixel = row + x * 4;
                    if ((pixel[0] | pixel[1] | pixel[2]) == 0)
                    {
                        continue;
                    }

                    if (!HasRequiredYellowCluster(data, x, y))
                    {
                        continue;
                    }

                    return new Point(x, y);
                }
            }

            return null;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static unsafe Point? FindClosestQualifiedTarget(
        Bitmap bitmap,
        Point center)
    {
        var rectangle = new Rectangle(Point.Empty, bitmap.Size);
        BitmapData data = bitmap.LockBits(
            rectangle,
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            Point? closest = null;
            long closestDistanceSquared = long.MaxValue;
            for (int y = 0; y < data.Height; y++)
            {
                byte* row = (byte*)data.Scan0 + y * data.Stride;
                for (int x = 0; x < data.Width; x++)
                {
                    byte* pixel = row + x * 4;
                    if ((pixel[0] | pixel[1] | pixel[2]) == 0)
                    {
                        continue;
                    }

                    long deltaX = x - center.X;
                    long deltaY = y - center.Y;
                    long distanceSquared =
                        deltaX * deltaX + deltaY * deltaY;
                    if (distanceSquared >= closestDistanceSquared
                        || !HasRequiredYellowCluster(data, x, y))
                    {
                        continue;
                    }

                    closest = new Point(x, y);
                    closestDistanceSquared = distanceSquared;
                }
            }

            return closest;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static unsafe Point? FindHighestMatchInsideCircle(
        Bitmap bitmap,
        int diameter)
    {
        BitmapData data = bitmap.LockBits(
            new Rectangle(Point.Empty, bitmap.Size),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            double centerX = (data.Width - 1) / 2.0;
            double centerY = (data.Height - 1) / 2.0;
            double radius = diameter / 2.0;
            double radiusSquared = radius * radius;
            int firstY = Math.Max(0, (int)Math.Ceiling(centerY - radius));
            int finalY = Math.Min(
                data.Height - 1,
                (int)Math.Floor(centerY + radius));

            for (int y = firstY; y <= finalY; y++)
            {
                double dy = y - centerY;
                double horizontalSquared = radiusSquared - dy * dy;
                if (horizontalSquared < 0)
                {
                    continue;
                }

                int halfWidth = (int)Math.Floor(Math.Sqrt(horizontalSquared));
                int firstX = Math.Max(
                    0,
                    (int)Math.Ceiling(centerX - halfWidth));
                int finalX = Math.Min(
                    data.Width - 1,
                    (int)Math.Floor(centerX + halfWidth));
                byte* row = (byte*)data.Scan0 + y * data.Stride;
                int? closestX = null;
                double closestDistance = double.MaxValue;
                for (int x = firstX; x <= finalX; x++)
                {
                    byte* pixel = row + x * 4;
                    if ((pixel[0] | pixel[1] | pixel[2]) == 0)
                    {
                        continue;
                    }

                    double distance = Math.Abs(x - centerX);
                    if (distance < closestDistance)
                    {
                        closestX = x;
                        closestDistance = distance;
                    }
                }

                if (closestX is int matchX)
                {
                    return new Point(matchX, y);
                }
            }

            return null;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static unsafe Point? FindHighestSavedColorInsideCircle(
        Bitmap bitmap)
    {
        BitmapData data = bitmap.LockBits(
            new Rectangle(Point.Empty, bitmap.Size),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            double center = (data.Width - 1) / 2.0;
            double radius = data.Width / 2.0;
            double radiusSquared = radius * radius;
            for (int y = 0; y < data.Height; y++)
            {
                double dy = y - center;
                double horizontalSquared = radiusSquared - dy * dy;
                if (horizontalSquared < 0)
                {
                    continue;
                }

                int halfWidth = (int)Math.Floor(Math.Sqrt(horizontalSquared));
                int firstX = Math.Max(0, (int)Math.Ceiling(center - halfWidth));
                int finalX = Math.Min(
                    data.Width - 1,
                    (int)Math.Floor(center + halfWidth));
                byte* row = (byte*)data.Scan0 + y * data.Stride;
                int? closestX = null;
                double closestDistance = double.MaxValue;
                for (int x = firstX; x <= finalX; x++)
                {
                    byte* pixel = row + x * 4;
                    if (!ColorStorage.ContainsExact(
                            pixel[2],
                            pixel[1],
                            pixel[0]))
                    {
                        continue;
                    }

                    double distance = Math.Abs(x - center);
                    if (distance < closestDistance)
                    {
                        closestX = x;
                        closestDistance = distance;
                    }
                }

                if (closestX is int matchX)
                {
                    return new Point(matchX, y);
                }
            }

            return null;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static unsafe bool HasRequiredYellowCluster(
        BitmapData data,
        int startX,
        int startY)
    {
        Span<Point> connected =
            stackalloc Point[RequiredConnectedYellowPixels];
        connected[0] = new Point(startX, startY);
        int discovered = 1;
        int next = 0;

        while (next < discovered)
        {
            Point current = connected[next++];
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                int x = current.X + offsetX;
                int y = current.Y + offsetY;
                if (x < 0 || x >= data.Width || y < 0 || y >= data.Height
                    || !IsSavedYellow(data, x, y))
                {
                    continue;
                }

                bool alreadyDiscovered = false;
                for (int index = 0; index < discovered; index++)
                {
                    if (connected[index].X == x && connected[index].Y == y)
                    {
                        alreadyDiscovered = true;
                        break;
                    }
                }

                if (alreadyDiscovered)
                {
                    continue;
                }

                connected[discovered++] = new Point(x, y);
                if (discovered >= RequiredConnectedYellowPixels)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static unsafe bool IsSavedYellow(
        BitmapData data,
        int x,
        int y)
    {
        byte* pixel = (byte*)data.Scan0 + y * data.Stride + x * 4;
        return (pixel[0] | pixel[1] | pixel[2]) != 0;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        lock (frameSync)
        {
            if (frontFrame is null)
            {
                return;
            }

            Rectangle destination = Fit(frontFrame.Size, ClientRectangle);
            e.Graphics.InterpolationMode =
                InterpolationMode.HighQualityBilinear;
            e.Graphics.DrawImage(frontFrame, destination);

            int deadzonePixels = settings.StickVerticalDeadzonePixels;
            if (deadzonePixels > 0)
            {
                float scaleY = destination.Height
                    / (float)Math.Max(1, frontFrame.Height);
                float targetCenterY = destination.Top
                    + destination.Height / 2f
                    - settings.FinalDownPixels * scaleY;
                float bandHalfHeight = Math.Max(1f, deadzonePixels * scaleY);
                float bandTop = Math.Max(
                    destination.Top,
                    targetCenterY - bandHalfHeight);
                float bandBottom = Math.Min(
                    destination.Bottom - 1,
                    targetCenterY + bandHalfHeight);
                using var bandBrush = new SolidBrush(
                    Color.FromArgb(36, 120, 139, 151));
                e.Graphics.FillRectangle(
                    bandBrush,
                    destination.Left,
                    bandTop,
                    destination.Width,
                    Math.Max(1f, bandBottom - bandTop));
                using var deadzonePen = new Pen(
                    Color.FromArgb(180, 120, 139, 151),
                    1f);
                e.Graphics.DrawLine(
                    deadzonePen,
                    destination.Left,
                    bandTop,
                    destination.Right,
                    bandTop);
                e.Graphics.DrawLine(
                    deadzonePen,
                    destination.Left,
                    bandBottom,
                    destination.Right,
                    bandBottom);
            }

            using var centerBrush = new SolidBrush(Color.Red);
            if (frontHitY >= 0)
            {
                int scanLineY = destination.Top + (int)Math.Round(
                    frontHitY * (destination.Height - 1.0)
                    / Math.Max(1, frontFrame.Height - 1));
                e.Graphics.FillRectangle(
                    centerBrush,
                    destination.Left,
                    scanLineY,
                    destination.Width,
                    1);
            }

            if (settings.LinearAimMode
                && frontContactPoint is Point linearContact)
            {
                int contactX = destination.Left + (int)Math.Round(
                    linearContact.X * (destination.Width - 1.0)
                    / Math.Max(1, frontFrame.Width - 1));
                int contactY = destination.Top + (int)Math.Round(
                    linearContact.Y * (destination.Height - 1.0)
                    / Math.Max(1, frontFrame.Height - 1));
                using var linePen = new Pen(Color.Red, 1);
                e.Graphics.DrawLine(
                    linePen,
                    destination.Left + destination.Width / 2,
                    destination.Top + destination.Height / 2,
                    contactX,
                    contactY);

            }

            if (frontContactPoint is Point contact)
            {
                int contactX = destination.Left + (int)Math.Round(
                    contact.X * (destination.Width - 1.0)
                    / Math.Max(1, frontFrame.Width - 1));
                int contactY = destination.Top + (int)Math.Round(
                    contact.Y * (destination.Height - 1.0)
                    / Math.Max(1, frontFrame.Height - 1));
                using var contactBrush = new SolidBrush(Color.Lime);
                e.Graphics.FillRectangle(
                    contactBrush,
                    contactX,
                    contactY,
                    1,
                    1);
            }

            e.Graphics.FillRectangle(
                centerBrush,
                destination.Left + destination.Width / 2,
                destination.Top + destination.Height / 2,
                1,
                1);

            float circleDiameter = Math.Max(
                1f,
                settings.PreviewCircleSize
                * destination.Width
                / (float)Math.Max(1, frontFrame.Width));
            float circleX = destination.Left
                + (destination.Width - circleDiameter) / 2f;
            float circleY = destination.Top
                + (destination.Height - circleDiameter) / 2f;
            using var circlePen = new Pen(Color.White, 1f);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawEllipse(
                circlePen,
                circleX,
                circleY,
                circleDiameter,
                circleDiameter);
        }

        e.Graphics.SmoothingMode = SmoothingMode.None;
        using var previewBorder = new Pen(UiTheme.Hairline, 1);
        e.Graphics.DrawRectangle(
            previewBorder,
            0,
            0,
            Math.Max(0, ClientSize.Width - 1),
            Math.Max(0, ClientSize.Height - 1));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Interlocked.Increment(ref stickGeneration);
            StopCapture();
            movementSequence.Wait();
            stickFrame?.Dispose();
            stickFrame = null;
            movementSequence.Release();
            lock (frameSync)
            {
                frontFrame?.Dispose();
                backFrame?.Dispose();
                frontFrame = null;
                backFrame = null;
            }
        }

        base.Dispose(disposing);
    }

    private static void WaitUntil(
        long targetTimestamp,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            long remaining = targetTimestamp - Stopwatch.GetTimestamp();
            if (remaining <= 0)
            {
                return;
            }

            double milliseconds =
                remaining * 1000.0 / Stopwatch.Frequency;
            if (milliseconds > 1.5)
            {
                Thread.Sleep(Math.Max(1, (int)milliseconds - 1));
            }
            else
            {
                Thread.SpinWait(32);
            }
        }
    }

    private static Rectangle Fit(Size source, Rectangle destination)
    {
        double scale = Math.Min(
            destination.Width / (double)source.Width,
            destination.Height / (double)source.Height);
        int width = Math.Max(1, (int)Math.Round(source.Width * scale));
        int height = Math.Max(1, (int)Math.Round(source.Height * scale));
        return new Rectangle(
            destination.X + (destination.Width - width) / 2,
            destination.Y + (destination.Height - height) / 2,
            width,
            height);
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
