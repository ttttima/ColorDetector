using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class UiTheme
{
    public static readonly Color Canvas = Color.FromArgb(245, 245, 245);
    public static readonly Color Paper = Color.White;
    public static readonly Color SurfaceAlt = Color.FromArgb(250, 250, 250);
    public static readonly Color InputFill = Color.FromArgb(245, 245, 245);
    public static readonly Color Ink = Color.FromArgb(10, 10, 10);
    public static readonly Color InkSoft = Color.FromArgb(23, 23, 23);
    public static readonly Color MidGray = Color.FromArgb(115, 115, 115);
    public static readonly Color Hairline = Color.FromArgb(229, 229, 229);

    public static Font BodyFont(float size = 9f, FontStyle style = FontStyle.Regular)
    {
        return new Font("Segoe UI Variable Text", size, style);
    }

    public static Font HeadingFont(float size = 18f)
    {
        return new Font("Segoe UI Variable Display", size, FontStyle.Bold);
    }
}

public sealed class ControlForm : Form
{
    private static readonly Color WindowColor = UiTheme.Canvas;
    private static readonly Color CardColor = UiTheme.Paper;
    private static readonly Color MutedColor = UiTheme.MidGray;
    private static readonly Color AccentColor = UiTheme.InkSoft;
    private static readonly Color SuccessColor = UiTheme.InkSoft;
    private static readonly Color PurpleAccent = UiTheme.InkSoft;
    private static readonly Color PinkAccent = UiTheme.InkSoft;
    private static readonly Color OrangeAccent = UiTheme.InkSoft;

    private readonly ValueBadge reactionValue = new();
    private readonly ValueBadge detectionFpsValue = new();
    private readonly ValueBadge overlaySizeValue = new();
    private readonly ValueBadge snapBackValue = new();
    private readonly Label snapBackMultiplierValue = new();
    private readonly RoundedPanel colorSwatch = new();
    private readonly Label watchedHex = new();
    private readonly Label watchedRgb = new();
    private readonly ValueBadge watchedMatch = new();
    private readonly RichTextBox log = new();
    private bool showingCardputerPosition;
    private readonly System.Windows.Forms.Timer colorTimer = new()
    {
        Interval = 50
    };

    public ControlForm(DetectionSettings settings)
    {
        Text = "ColorDetector";
        ClientSize = new Size(1280, 920);
        MinimumSize = new Size(580, 880);
        StartPosition = FormStartPosition.Manual;
        Location = new Point(24, 120);
        TopMost = false;
        BackColor = WindowColor;
        ForeColor = UiTheme.Ink;
        Font = UiTheme.BodyFont();
        FormBorderStyle = FormBorderStyle.Sizable;
        DoubleBuffered = true;

        var header = BuildHeader(settings);
        var reactionCard = BuildReactionCard(settings);
        var watchedColorCard = BuildWatchedColorCard();
        var consoleCard = BuildConsoleCard();
        var monitorPreviewCard = BuildMonitorPreviewCard(settings);
        var pageSwitch = new PillButton
        {
            FlatStyle = FlatStyle.Flat,
            Size = new Size(190, 34),
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        var sectionDivider = new Panel
        {
            BackColor = UiTheme.Hairline,
            Location = new Point(640, 0),
            Size = new Size(1, ClientSize.Height),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
        };
        var cardputerPositionTitle = new Label
        {
            AutoSize = true,
            Font = UiTheme.HeadingFont(),
            ForeColor = UiTheme.Ink,
            Location = new Point(668, 23),
            Text = "CardputerPosition"
        };

        void UpdatePageSwitch()
        {
            pageSwitch.Text = showingCardputerPosition
                ? "COLOR DETECTOR"
                : "CARDPUTER POSITION";
            pageSwitch.ForeColor = UiTheme.Paper;
            pageSwitch.BackColor = UiTheme.InkSoft;
        }

        pageSwitch.Click += (_, _) =>
        {
            showingCardputerPosition = !showingCardputerPosition;
            UpdatePageSwitch();
            LayoutCards(
                pageSwitch,
                header,
                reactionCard,
                watchedColorCard,
                consoleCard,
                sectionDivider,
                cardputerPositionTitle,
                monitorPreviewCard);
        };

        Controls.Add(pageSwitch);
        Controls.Add(header);
        Controls.Add(reactionCard);
        Controls.Add(watchedColorCard);
        Controls.Add(consoleCard);
        Controls.Add(monitorPreviewCard);
        Controls.Add(sectionDivider);
        Controls.Add(cardputerPositionTitle);

        Resize += (_, _) =>
            LayoutCards(
                pageSwitch,
                header,
                reactionCard,
                watchedColorCard,
                consoleCard,
                sectionDivider,
                cardputerPositionTitle,
                monitorPreviewCard);
        Shown += (_, _) =>
        {
            UpdatePageSwitch();
            LayoutCards(
                pageSwitch,
                header,
                reactionCard,
                watchedColorCard,
                consoleCard,
                sectionDivider,
                cardputerPositionTitle,
                monitorPreviewCard);
            Activate();
            BringToFront();
            AppLog.Write("Application ready.");
            colorTimer.Start();
        };

        colorTimer.Tick += (_, _) => UpdateWatchedColor(settings);
        FormClosed += (_, _) =>
        {
            colorTimer.Stop();
            colorTimer.Dispose();
            AppLog.MessageWritten -= AppendLog;
        };
        AppLog.MessageWritten += AppendLog;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyModernBackdrop();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var background = new SolidBrush(WindowColor);
        e.Graphics.FillRectangle(background, ClientRectangle);
    }

    private void ApplyModernBackdrop()
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            int corner = 2;
            DwmSetWindowAttribute(Handle, 33, ref corner, sizeof(int));
        }
    }

    private Control BuildHeader(DetectionSettings settings)
    {
        var header = new Panel
        {
            BackColor = Color.Transparent,
            Size = new Size(572, 56)
        };

        var icon = new GradientBadge
        {
            Text = "C",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = UiTheme.Ink,
            StartColor = CardColor,
            EndColor = CardColor,
            Size = new Size(44, 44),
            Location = new Point(0, 5)
        };

        var title = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI Variable Display", 18, FontStyle.Bold),
            ForeColor = UiTheme.Ink,
            Location = new Point(60, 1),
            Text = "ColorDetector"
        };

        var subtitle = new Label
        {
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(61, 36),
            Text = "Hardware-assisted color reaction"
        };

        var statusDot = new Label
        {
            Text = "●",
            AutoSize = true,
            Font = new Font("Segoe UI", 11),
            ForeColor = SuccessColor,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(454, 14),
            Cursor = Cursors.Hand
        };

        var status = new Label
        {
            Text = "RUNNING",
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = AccentColor,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(476, 16),
            Cursor = Cursors.Hand
        };

        void UpdateDetectorStatus()
        {
            bool enabled = settings.DetectorEnabled;
            status.Text = enabled ? "RUNNING" : "OFF";
            statusDot.ForeColor = enabled ? SuccessColor : MutedColor;
            status.ForeColor = enabled
                ? AccentColor
                : MutedColor;
        }

        void ToggleDetector(object? sender, EventArgs eventArgs)
        {
            settings.DetectorEnabled = !settings.DetectorEnabled;
            if (!settings.DetectorEnabled)
            {
                SnapBack.Cancel();
            }

            UpdateDetectorStatus();
            SettingsStorage.Save(settings);
            AppLog.Write(settings.DetectorEnabled
                ? "Detector enabled."
                : "Detector disabled.");
        }

        statusDot.Click += ToggleDetector;
        status.Click += ToggleDetector;

        var activationMode = new PillButton
        {
            FlatStyle = FlatStyle.Flat,
            Size = new Size(92, 34),
            Location = new Point(350, 10),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        activationMode.FlatAppearance.BorderSize = 0;

        void UpdateActivationMode()
        {
            bool toggle = settings.ToggleActivationMode;
            activationMode.Text = toggle ? "TOGGLE" : "HOLD";
            activationMode.ForeColor = toggle
                ? UiTheme.Paper
                : UiTheme.Ink;
            activationMode.BackColor = toggle
                ? UiTheme.InkSoft
                : UiTheme.SurfaceAlt;
        }

        activationMode.Click += (_, _) =>
        {
            settings.ToggleActivationMode =
                !settings.ToggleActivationMode;
            UpdateActivationMode();
            SettingsStorage.Save(settings);
        };

        header.Controls.Add(icon);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(activationMode);
        header.Controls.Add(statusDot);
        header.Controls.Add(status);
        UpdateActivationMode();
        UpdateDetectorStatus();
        return header;
    }

    private Control BuildReactionCard(DetectionSettings settings)
    {
        var card = new RoundedPanel
        {
            BackColor = CardColor,
            CornerRadius = 22,
            Size = new Size(572, 465)
        };

        var eyebrow = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = MutedColor,
            Location = new Point(22, 18),
            Text = "PERFORMANCE"
        };

        var title = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = UiTheme.Ink,
            Location = new Point(20, 40),
            Text = "Reaction delay"
        };

        var help = new Label
        {
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(22, 67),
            Text = "Delay between color detection and the CLICK command."
        };

        reactionValue.AutoSize = false;
        reactionValue.Size = new Size(94, 38);
        reactionValue.TextAlign = ContentAlignment.MiddleCenter;
        reactionValue.Font = new Font("Segoe UI", 13, FontStyle.Bold);
        reactionValue.ForeColor = UiTheme.Ink;
        reactionValue.BackColor = UiTheme.SurfaceAlt;
        reactionValue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        reactionValue.Location = new Point(462, 24);

        var slider = new ModernSlider
        {
            Location = new Point(22, 101),
            Size = new Size(534, 34),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Minimum = 0,
            Maximum = 500,
            Value = settings.ReactionTimeMs,
            AccentColor = PinkAccent,
            EndColor = PurpleAccent
        };

        var fastest = new Label
        {
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(22, 138),
            Text = "0 ms  FASTEST"
        };

        var controlled = new Label
        {
            AutoSize = true,
            ForeColor = MutedColor,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(461, 138),
            Text = "500 ms"
        };

        var fpsTitle = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = UiTheme.Ink,
            Location = new Point(20, 172),
            Text = "Detection rate"
        };

        detectionFpsValue.AutoSize = false;
        detectionFpsValue.Size = new Size(94, 38);
        detectionFpsValue.TextAlign = ContentAlignment.MiddleCenter;
        detectionFpsValue.Font = new Font("Segoe UI", 13, FontStyle.Bold);
        detectionFpsValue.ForeColor = UiTheme.Ink;
        detectionFpsValue.BackColor = UiTheme.SurfaceAlt;
        detectionFpsValue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        detectionFpsValue.Location = new Point(462, 164);

        var fpsSlider = new ModernSlider
        {
            Location = new Point(22, 210),
            Size = new Size(534, 34),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Minimum = 30,
            Maximum = 1000,
            Value = settings.DetectionFps,
            AccentColor = AccentColor,
            EndColor = OrangeAccent
        };

        var fpsMinimum = new Label
        {
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(22, 246),
            Text = "30 FPS"
        };

        var fpsMaximum = new Label
        {
            AutoSize = true,
            ForeColor = MutedColor,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(475, 246),
            Text = "1000 FPS  FASTEST"
        };

        var overlayTitle = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = UiTheme.Ink,
            Location = new Point(20, 277),
            Text = "Watched pixel circle"
        };

        overlaySizeValue.AutoSize = false;
        overlaySizeValue.Size = new Size(94, 38);
        overlaySizeValue.TextAlign = ContentAlignment.MiddleCenter;
        overlaySizeValue.Font = new Font("Segoe UI", 13, FontStyle.Bold);
        overlaySizeValue.ForeColor = UiTheme.Ink;
        overlaySizeValue.BackColor = UiTheme.SurfaceAlt;
        overlaySizeValue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        overlaySizeValue.Location = new Point(462, 269);

        var overlaySizeSlider = new ModernSlider
        {
            Location = new Point(22, 315),
            Size = new Size(534, 34),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Minimum = 1,
            Maximum = 250,
            Value = settings.OverlayCircleSize,
            AccentColor = AccentColor,
            EndColor = PurpleAccent
        };

        var overlayMinimum = new Label
        {
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(22, 345),
            Text = "1 px"
        };

        var overlayMaximum = new Label
        {
            AutoSize = true,
            ForeColor = MutedColor,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(512, 345),
            Text = "250 px"
        };

        var overlayVisibility = new PillButton
        {
            FlatStyle = FlatStyle.Flat,
            Size = new Size(94, 38),
            Location = new Point(356, 269),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        overlayVisibility.FlatAppearance.BorderSize = 0;

        var snapBackTitle = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = UiTheme.Ink,
            Location = new Point(20, 377),
            Text = "Snap-back"
        };

        var snapBackHelp = new Label
        {
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(22, 404),
            Text = "Delay before smooth hardware return to the saved position."
        };

        snapBackValue.AutoSize = false;
        snapBackValue.Size = new Size(94, 38);
        snapBackValue.TextAlign = ContentAlignment.MiddleCenter;
        snapBackValue.Font = new Font("Segoe UI", 13, FontStyle.Bold);
        snapBackValue.ForeColor = UiTheme.Ink;
        snapBackValue.BackColor = UiTheme.SurfaceAlt;
        snapBackValue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        snapBackValue.Location = new Point(462, 369);

        var snapBackSlider = new ModernSlider
        {
            Location = new Point(22, 423),
            Size = new Size(346, 34),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Minimum = 1,
            Maximum = 2000,
            Value = settings.SnapBackDelayHalfMilliseconds,
            AccentColor = OrangeAccent,
            EndColor = PinkAccent
        };

        snapBackMultiplierValue.AutoSize = true;
        snapBackMultiplierValue.Font =
            new Font("Segoe UI", 8, FontStyle.Bold);
        snapBackMultiplierValue.ForeColor = MutedColor;
        snapBackMultiplierValue.Location = new Point(390, 404);

        var snapBackMultiplierSlider = new ModernSlider
        {
            Location = new Point(382, 423),
            Size = new Size(174, 34),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Minimum = 25,
            Maximum = 400,
            Value = settings.SnapBackTimeMultiplierPercent,
            AccentColor = AccentColor,
            EndColor = PurpleAccent
        };

        void UpdateOverlayVisibilityButton()
        {
            bool visible = settings.OverlayCircleVisible;
            overlayVisibility.Text = visible ? "VISIBLE" : "HIDDEN";
            overlayVisibility.ForeColor = visible
                ? UiTheme.Paper
                : MutedColor;
            overlayVisibility.BackColor = visible
                ? UiTheme.InkSoft
                : UiTheme.SurfaceAlt;
        }

        overlayVisibility.Click += (_, _) =>
        {
            settings.OverlayCircleVisible = !settings.OverlayCircleVisible;
            UpdateOverlayVisibilityButton();
            SettingsStorage.Save(settings);
        };

        void UpdateReactionTime()
        {
            settings.ReactionTimeMs = slider.Value;
            reactionValue.Text = $"{slider.Value} ms";
            AppLog.Write($"Reaction delay set to {slider.Value} ms.");
            SettingsStorage.Save(settings);
        }

        slider.ValueChanged += (_, _) => UpdateReactionTime();

        void UpdateDetectionFps()
        {
            settings.DetectionFps = fpsSlider.Value;
            detectionFpsValue.Text = $"{fpsSlider.Value} FPS";
            AppLog.Write($"Detection rate set to {fpsSlider.Value} FPS.");
            SettingsStorage.Save(settings);
        }

        fpsSlider.ValueChanged += (_, _) => UpdateDetectionFps();

        void UpdateOverlaySize()
        {
            settings.OverlayCircleSize = overlaySizeSlider.Value;
            overlaySizeValue.Text = $"{overlaySizeSlider.Value} px";
            SettingsStorage.Save(settings);
        }

        overlaySizeSlider.ValueChanged += (_, _) => UpdateOverlaySize();

        void UpdateSnapBackDelay()
        {
            settings.SnapBackDelayHalfMilliseconds = snapBackSlider.Value;
            double milliseconds = snapBackSlider.Value * 0.5;
            snapBackValue.Text = milliseconds < 1000
                ? $"{milliseconds:0.0} ms"
                : "1.0 s";
            SettingsStorage.Save(settings);
        }

        snapBackSlider.ValueChanged += (_, _) => UpdateSnapBackDelay();

        void UpdateSnapBackMultiplier()
        {
            settings.SnapBackTimeMultiplierPercent =
                snapBackMultiplierSlider.Value;
            snapBackMultiplierValue.Text =
                $"RETURN TIME  {snapBackMultiplierSlider.Value / 100.0:0.00}x";
            SettingsStorage.Save(settings);
        }

        snapBackMultiplierSlider.ValueChanged +=
            (_, _) => UpdateSnapBackMultiplier();

        card.Controls.Add(eyebrow);
        card.Controls.Add(title);
        card.Controls.Add(help);
        card.Controls.Add(reactionValue);
        card.Controls.Add(slider);
        card.Controls.Add(fastest);
        card.Controls.Add(controlled);
        card.Controls.Add(fpsTitle);
        card.Controls.Add(detectionFpsValue);
        card.Controls.Add(fpsSlider);
        card.Controls.Add(fpsMinimum);
        card.Controls.Add(fpsMaximum);
        card.Controls.Add(overlayTitle);
        card.Controls.Add(overlaySizeValue);
        card.Controls.Add(overlaySizeSlider);
        card.Controls.Add(overlayMinimum);
        card.Controls.Add(overlayMaximum);
        card.Controls.Add(overlayVisibility);
        card.Controls.Add(snapBackTitle);
        card.Controls.Add(snapBackHelp);
        card.Controls.Add(snapBackValue);
        card.Controls.Add(snapBackSlider);
        card.Controls.Add(snapBackMultiplierValue);
        card.Controls.Add(snapBackMultiplierSlider);
        UpdateReactionTime();
        UpdateDetectionFps();
        UpdateOverlaySize();
        UpdateOverlayVisibilityButton();
        UpdateSnapBackDelay();
        UpdateSnapBackMultiplier();
        return card;
    }

    private Control BuildWatchedColorCard()
    {
        var card = new RoundedPanel
        {
            BackColor = CardColor,
            CornerRadius = 22,
            Size = new Size(572, 96)
        };

        colorSwatch.CornerRadius = 14;
        colorSwatch.BackColor = Color.Black;
        colorSwatch.Location = new Point(20, 18);
        colorSwatch.Size = new Size(60, 60);

        var title = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = MutedColor,
            Location = new Point(98, 16),
            Text = "WATCHED PIXEL"
        };

        watchedHex.AutoSize = true;
        watchedHex.Font = new Font("Cascadia Mono", 15, FontStyle.Bold);
        watchedHex.ForeColor = UiTheme.Ink;
        watchedHex.Location = new Point(96, 35);
        watchedHex.Text = "#000000";

        watchedRgb.AutoSize = true;
        watchedRgb.Font = new Font("Cascadia Mono", 9);
        watchedRgb.ForeColor = MutedColor;
        watchedRgb.Location = new Point(99, 66);
        watchedRgb.Text = "RGB  0, 0, 0";

        watchedMatch.AutoSize = false;
        watchedMatch.Size = new Size(112, 30);
        watchedMatch.TextAlign = ContentAlignment.MiddleCenter;
        watchedMatch.Font = new Font("Segoe UI", 8, FontStyle.Bold);
        watchedMatch.ForeColor = MutedColor;
        watchedMatch.BackColor = UiTheme.SurfaceAlt;
        watchedMatch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        watchedMatch.Location = new Point(444, 33);
        watchedMatch.Text = "NO MATCH";

        card.Controls.Add(colorSwatch);
        card.Controls.Add(title);
        card.Controls.Add(watchedHex);
        card.Controls.Add(watchedRgb);
        card.Controls.Add(watchedMatch);
        return card;
    }

    private Control BuildConsoleCard()
    {
        var card = new RoundedPanel
        {
            BackColor = CardColor,
            CornerRadius = 22,
            Size = new Size(572, 182)
        };

        var title = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = UiTheme.Ink,
            Location = new Point(20, 16),
            Text = "Activity"
        };

        var live = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = SuccessColor,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(514, 20),
            Text = "● LIVE"
        };

        log.Anchor =
            AnchorStyles.Top | AnchorStyles.Bottom |
            AnchorStyles.Left | AnchorStyles.Right;
        log.Location = new Point(20, 52);
        log.Size = new Size(532, 142);
        log.BackColor = UiTheme.SurfaceAlt;
        log.ForeColor = UiTheme.InkSoft;
        log.Font = new Font("Cascadia Mono", 9);
        log.ReadOnly = true;
        log.BorderStyle = BorderStyle.None;
        log.DetectUrls = false;
        log.Margin = Padding.Empty;

        card.Controls.Add(title);
        card.Controls.Add(live);
        card.Controls.Add(log);
        return card;
    }

    private static Control BuildMonitorPreviewCard(DetectionSettings settings)
    {
        MonitorPreviewControl? preview = null;
        var card = new RoundedPanel
        {
            BackColor = CardColor,
            CornerRadius = 22,
            Size = new Size(584, 796)
        };

        var title = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = UiTheme.Ink,
            Location = new Point(20, 18),
            Text = "MONITOR PREVIEW"
        };
        var scanMode = new PillButton
        {
            FlatStyle = FlatStyle.Flat,
            Size = new Size(112, 30),
            Location = new Point(452, 14),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        scanMode.FlatAppearance.BorderSize = 0;

        void UpdateScanMode()
        {
            scanMode.Text = settings.LinearAimMode
                ? "LINEAR"
                : "HORIZONTAL";
            scanMode.ForeColor = settings.LinearAimMode
                ? UiTheme.Paper
                : UiTheme.Ink;
            scanMode.BackColor = settings.LinearAimMode
                ? UiTheme.InkSoft
                : UiTheme.SurfaceAlt;
        }

        scanMode.Click += (_, _) =>
        {
            settings.LinearAimMode = !settings.LinearAimMode;
            UpdateScanMode();
            SettingsStorage.Save(settings);
        };
        UpdateScanMode();
        var stick = new CheckBox
        {
            AutoSize = true,
            Location = new Point(20, 59),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Text = "STICK",
            Checked = settings.StickEnabled,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = MutedColor,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        bool synchronizingStick = false;
        stick.CheckedChanged += (_, _) =>
        {
            stick.ForeColor = stick.Checked ? AccentColor : MutedColor;
            if (synchronizingStick)
            {
                return;
            }

            settings.StickEnabled = stick.Checked;
            SettingsStorage.Save(settings);
            AppLog.Write(stick.Checked
                ? "Stick targeting enabled."
                : "Stick targeting disabled.");
        };
        stick.ForeColor = stick.Checked ? AccentColor : MutedColor;
        var stickBind = new PillButton
        {
            Size = new Size(110, 30),
            Location = new Point(94, 50),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = UiTheme.Ink,
            BackColor = UiTheme.SurfaceAlt,
            Cursor = Cursors.Hand,
            TabStop = false,
            Text = MonitorPreviewControl.GetBindingDisplayName(
                settings.StickActivationVirtualKey)
        };
        stickBind.Click += (_, _) =>
        {
            stickBind.Text = "PRESS KEY";
            preview?.BeginStickBindingCapture();
        };
        var stickMode = new PillButton
        {
            Size = new Size(100, 30),
            Location = new Point(212, 50),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        void UpdateStickMode()
        {
            bool toggle = settings.StickToggleActivationMode;
            stickMode.Text = toggle ? "TOGGLE" : "HOLD";
            stickMode.ForeColor = toggle ? UiTheme.Paper : MutedColor;
            stickMode.BackColor = toggle
                ? UiTheme.InkSoft
                : UiTheme.SurfaceAlt;
            stick.Enabled = toggle;
        }
        stickMode.Click += (_, _) =>
        {
            settings.StickToggleActivationMode =
                !settings.StickToggleActivationMode;
            if (!settings.StickToggleActivationMode)
            {
                settings.StickEnabled = false;
                synchronizingStick = true;
                stick.Checked = false;
                stick.ForeColor = MutedColor;
                synchronizingStick = false;
            }
            SettingsStorage.Save(settings);
            UpdateStickMode();
        };
        UpdateStickMode();
        var previewCircleLabel = new Label
        {
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(20, 96),
            Text = $"PREVIEW CIRCLE  {settings.PreviewCircleSize} px"
        };
        var previewCircleOnScreen = new CheckBox
        {
            AutoSize = true,
            Location = new Point(336, 58),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Text = "SHOW ON SCREEN",
            Checked = settings.PreviewCircleVisibleOnScreen,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = settings.PreviewCircleVisibleOnScreen
                ? AccentColor
                : MutedColor,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        previewCircleOnScreen.CheckedChanged += (_, _) =>
        {
            settings.PreviewCircleVisibleOnScreen =
                previewCircleOnScreen.Checked;
            previewCircleOnScreen.ForeColor = previewCircleOnScreen.Checked
                ? AccentColor
                : MutedColor;
            SettingsStorage.Save(settings);
        };
        var stickPrediction = new CheckBox
        {
            AutoSize = true,
            Location = new Point(336, 78),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Text = "PREDICT",
            Checked = settings.StickPredictionEnabled,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = settings.StickPredictionEnabled
                ? AccentColor
                : MutedColor,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        stickPrediction.CheckedChanged += (_, _) =>
        {
            settings.StickPredictionEnabled = stickPrediction.Checked;
            stickPrediction.ForeColor = stickPrediction.Checked
                ? AccentColor
                : MutedColor;
            SettingsStorage.Save(settings);
        };
        var stickMethod = new PillButton
        {
            Size = new Size(110, 30),
            Location = new Point(454, 50),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        static string StickMethodText(int method)
        {
            return method switch
            {
                1 => "METHOD 1",
                2 => "METHOD 2",
                3 => "METHOD 3",
                4 => "METHOD 4",
                5 => "METHOD 5",
                _ => "METHOD 1"
            };
        }

        void UpdateStickMethod()
        {
            stickMethod.Text = StickMethodText(
                settings.StickStabilizationMethod);
            stickMethod.ForeColor = UiTheme.Paper;
            stickMethod.BackColor = UiTheme.InkSoft;
        }

        stickMethod.Click += (_, _) =>
        {
            settings.StickStabilizationMethod =
                settings.StickStabilizationMethod >= 5
                    ? 1
                    : settings.StickStabilizationMethod + 1;
            UpdateStickMethod();
            SettingsStorage.Save(settings);
        };
        UpdateStickMethod();
        var previewCircleSlider = new ModernSlider
        {
            Location = new Point(18, 116),
            Size = new Size(546, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Left
                | AnchorStyles.Right,
            Minimum = 1,
            Maximum = 1000,
            Value = settings.PreviewCircleSize,
            AccentColor = AccentColor,
            EndColor = PurpleAccent
        };
        previewCircleSlider.ValueChanged += (_, _) =>
        {
            settings.PreviewCircleSize = previewCircleSlider.Value;
            previewCircleLabel.Text =
                $"PREVIEW CIRCLE  {previewCircleSlider.Value} px";
            preview?.Invalidate();
            SettingsStorage.Save(settings);
        };
        var finalDownLabel = new Label
        {
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(20, 152),
            Text = $"FINAL DOWN  {settings.FinalDownPixels} px"
        };
        var finalDownSlider = new ModernSlider
        {
            Location = new Point(18, 172),
            Size = new Size(546, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Left
                | AnchorStyles.Right,
            Minimum = 0,
            Maximum = 50,
            Value = settings.FinalDownPixels,
            AccentColor = OrangeAccent,
            EndColor = PinkAccent
        };
        finalDownSlider.ValueChanged += (_, _) =>
        {
            settings.FinalDownPixels = finalDownSlider.Value;
            finalDownLabel.Text =
                $"FINAL DOWN  {finalDownSlider.Value} px";
            SettingsStorage.Save(settings);
        };
        var firstDelayLabel = new Label
        {
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(20, 208),
            Text = $"FIRST DELAY  {settings.FirstMovementDelayMs} ms"
        };
        var firstDelaySlider = new ModernSlider
        {
            Location = new Point(18, 228),
            Size = new Size(546, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Left
                | AnchorStyles.Right,
            Minimum = 0,
            Maximum = 20,
            Value = settings.FirstMovementDelayMs,
            AccentColor = PinkAccent,
            EndColor = OrangeAccent
        };
        firstDelaySlider.ValueChanged += (_, _) =>
        {
            settings.FirstMovementDelayMs = firstDelaySlider.Value;
            firstDelayLabel.Text =
                $"FIRST DELAY  {firstDelaySlider.Value} ms";
            SettingsStorage.Save(settings);
        };
        var secondDelayLabel = new Label
        {
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(20, 264),
            Text = $"SECOND DELAY  {settings.SecondMovementDelayMs} ms"
        };
        var secondDelaySlider = new ModernSlider
        {
            Location = new Point(18, 284),
            Size = new Size(546, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Left
                | AnchorStyles.Right,
            Minimum = 0,
            Maximum = 20,
            Value = settings.SecondMovementDelayMs,
            AccentColor = AccentColor,
            EndColor = PurpleAccent
        };
        secondDelaySlider.ValueChanged += (_, _) =>
        {
            settings.SecondMovementDelayMs = secondDelaySlider.Value;
            secondDelayLabel.Text =
                $"SECOND DELAY  {secondDelaySlider.Value} ms";
            SettingsStorage.Save(settings);
        };
        var thirdDelayLabel = new Label
        {
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(20, 320),
            Text = $"THIRD DELAY  {settings.ThirdMovementDelayMs} ms"
        };
        var thirdDelaySlider = new ModernSlider
        {
            Location = new Point(18, 340),
            Size = new Size(546, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Left
                | AnchorStyles.Right,
            Minimum = 0,
            Maximum = 20,
            Value = settings.ThirdMovementDelayMs,
            AccentColor = OrangeAccent,
            EndColor = PinkAccent
        };
        thirdDelaySlider.ValueChanged += (_, _) =>
        {
            settings.ThirdMovementDelayMs = thirdDelaySlider.Value;
            thirdDelayLabel.Text =
                $"THIRD DELAY  {thirdDelaySlider.Value} ms";
            SettingsStorage.Save(settings);
        };
        var stickSpeedLabel = new Label
        {
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(20, 376),
            Text = $"STICK SPEED  {settings.StickSpeedPercent}%"
        };
        var stickSpeedSlider = new ModernSlider
        {
            Location = new Point(18, 396),
            Size = new Size(546, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Left
                | AnchorStyles.Right,
            Minimum = 0,
            Maximum = 100,
            Value = settings.StickSpeedPercent,
            AccentColor = AccentColor,
            EndColor = AccentColor
        };
        stickSpeedSlider.ValueChanged += (_, _) =>
        {
            settings.StickSpeedPercent = stickSpeedSlider.Value;
            stickSpeedLabel.Text =
                $"STICK SPEED  {stickSpeedSlider.Value}%";
            SettingsStorage.Save(settings);
        };
        var stickSmoothnessLabel = new Label
        {
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(20, 432),
            Text = $"STICK SMOOTHNESS  {settings.StickSmoothnessPercent}%"
        };
        var stickSmoothnessSlider = new ModernSlider
        {
            Location = new Point(18, 452),
            Size = new Size(546, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Left
                | AnchorStyles.Right,
            Minimum = 0,
            Maximum = 1000,
            Value = settings.StickSmoothnessPercent,
            AccentColor = AccentColor,
            EndColor = AccentColor
        };
        stickSmoothnessSlider.ValueChanged += (_, _) =>
        {
            settings.StickSmoothnessPercent = stickSmoothnessSlider.Value;
            stickSmoothnessLabel.Text =
                $"STICK SMOOTHNESS  {stickSmoothnessSlider.Value}%";
            SettingsStorage.Save(settings);
        };
        var stickDeadzoneLabel = new Label
        {
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(20, 488),
            Text = $"STICK Y DEADZONE  "
                + $"{settings.StickVerticalDeadzonePixels} px"
        };
        var stickDeadzoneSlider = new ModernSlider
        {
            Location = new Point(18, 508),
            Size = new Size(546, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Left
                | AnchorStyles.Right,
            Minimum = 0,
            Maximum = 25,
            Value = settings.StickVerticalDeadzonePixels,
            AccentColor = AccentColor,
            EndColor = AccentColor
        };
        stickDeadzoneSlider.ValueChanged += (_, _) =>
        {
            settings.StickVerticalDeadzonePixels = stickDeadzoneSlider.Value;
            stickDeadzoneLabel.Text =
                $"STICK Y DEADZONE  {stickDeadzoneSlider.Value} px";
            preview?.Invalidate();
            SettingsStorage.Save(settings);
        };
        var listenLabel = new Label
        {
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(20, 544),
            Text = $"LISTEN PAUSE  "
                + $"{settings.AimPauseOnLeftClickMilliseconds / 1000.0:0.00}s"
        };
        var listen = new CheckBox
        {
            AutoSize = true,
            Location = new Point(454, 544),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Text = "LISTEN",
            Checked = settings.AimPauseOnLeftClickEnabled,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = settings.AimPauseOnLeftClickEnabled
                ? AccentColor
                : MutedColor,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        listen.CheckedChanged += (_, _) =>
        {
            settings.AimPauseOnLeftClickEnabled = listen.Checked;
            listen.ForeColor = listen.Checked
                ? AccentColor
                : MutedColor;
            SettingsStorage.Save(settings);
        };
        var listenPauseSlider = new ModernSlider
        {
            Location = new Point(18, 564),
            Size = new Size(546, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Left
                | AnchorStyles.Right,
            Minimum = 50,
            Maximum = 2000,
            Value = settings.AimPauseOnLeftClickMilliseconds,
            AccentColor = AccentColor,
            EndColor = AccentColor
        };
        listenPauseSlider.ValueChanged += (_, _) =>
        {
            settings.AimPauseOnLeftClickMilliseconds =
                listenPauseSlider.Value;
            listenLabel.Text =
                $"LISTEN PAUSE  {listenPauseSlider.Value / 1000.0:0.00}s";
            SettingsStorage.Save(settings);
        };
        preview = new MonitorPreviewControl(settings)
        {
            Location = new Point(20, 604),
            Size = new Size(544, 172),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                | AnchorStyles.Left | AnchorStyles.Right
        };
        preview.StickEnabledChanged += (_, _) =>
        {
            if (stick.IsDisposed || !stick.IsHandleCreated)
            {
                return;
            }

            stick.BeginInvoke(() =>
            {
                synchronizingStick = true;
                stick.Checked = settings.StickEnabled;
                stick.ForeColor = stick.Checked
                    ? AccentColor
                    : MutedColor;
                synchronizingStick = false;
            });
        };
        preview.StickBindingChanged += (_, _) =>
        {
            if (stickBind.IsDisposed || !stickBind.IsHandleCreated)
            {
                return;
            }

            stickBind.BeginInvoke(() =>
            {
                stickBind.Text = MonitorPreviewControl.GetBindingDisplayName(
                    settings.StickActivationVirtualKey);
            });
        };

        card.Controls.Add(title);
        card.Controls.Add(stick);
        card.Controls.Add(stickBind);
        card.Controls.Add(stickMode);
        card.Controls.Add(scanMode);
        card.Controls.Add(previewCircleLabel);
        card.Controls.Add(previewCircleOnScreen);
        card.Controls.Add(stickPrediction);
        card.Controls.Add(stickMethod);
        card.Controls.Add(previewCircleSlider);
        card.Controls.Add(finalDownLabel);
        card.Controls.Add(finalDownSlider);
        card.Controls.Add(firstDelayLabel);
        card.Controls.Add(firstDelaySlider);
        card.Controls.Add(secondDelayLabel);
        card.Controls.Add(secondDelaySlider);
        card.Controls.Add(thirdDelayLabel);
        card.Controls.Add(thirdDelaySlider);
        card.Controls.Add(stickSpeedLabel);
        card.Controls.Add(stickSpeedSlider);
        card.Controls.Add(stickSmoothnessLabel);
        card.Controls.Add(stickSmoothnessSlider);
        card.Controls.Add(stickDeadzoneLabel);
        card.Controls.Add(stickDeadzoneSlider);
        card.Controls.Add(listenLabel);
        card.Controls.Add(listen);
        card.Controls.Add(listenPauseSlider);
        card.Controls.Add(preview);
        return card;
    }

    private void LayoutCards(
        Control pageSwitch,
        Control header,
        Control reactionCard,
        Control watchedColorCard,
        Control consoleCard,
        Control sectionDivider,
        Control cardputerPositionTitle,
        Control monitorPreviewCard)
    {
        const int margin = 28;
        int contentWidth = Math.Max(1, ClientSize.Width - margin * 2);
        pageSwitch.SetBounds(
            Math.Max(margin, (ClientSize.Width - pageSwitch.Width) / 2),
            22,
            pageSwitch.Width,
            pageSwitch.Height);
        pageSwitch.BringToFront();

        header.Visible = !showingCardputerPosition;
        reactionCard.Visible = !showingCardputerPosition;
        watchedColorCard.Visible = !showingCardputerPosition;
        consoleCard.Visible = !showingCardputerPosition;
        sectionDivider.Visible = false;
        cardputerPositionTitle.Visible = showingCardputerPosition;
        monitorPreviewCard.Visible = showingCardputerPosition;

        if (showingCardputerPosition)
        {
            cardputerPositionTitle.SetBounds(margin, 24, contentWidth, 42);
            monitorPreviewCard.SetBounds(
                margin,
                96,
                contentWidth,
                Math.Max(150, ClientSize.Height - 124));
            return;
        }

        header.SetBounds(margin, 22, contentWidth, 56);
        reactionCard.SetBounds(margin, 96, contentWidth, 465);
        watchedColorCard.SetBounds(margin, 579, contentWidth, 96);
        consoleCard.SetBounds(
            margin,
            693,
            contentWidth,
            Math.Max(150, ClientSize.Height - 721));
    }

    private void UpdateWatchedColor(DetectionSettings settings)
    {
        var (r, g, b) = settings.GetObservedColor();
        var color = Color.FromArgb(r, g, b);
        string hex = $"#{r:X2}{g:X2}{b:X2}";
        bool isMatch = ColorStorage.Contains(r, g, b);

        colorSwatch.BackColor = color;
        watchedHex.Text = hex;
        watchedRgb.Text = $"RGB  {r}, {g}, {b}";
        watchedMatch.Text = isMatch ? "MATCH" : "NO MATCH";
        watchedMatch.ForeColor = isMatch
            ? UiTheme.Paper
            : MutedColor;
        watchedMatch.BackColor = isMatch
            ? UiTheme.InkSoft
            : UiTheme.SurfaceAlt;
    }

    private void AppendLog(string line)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(line));
            return;
        }

        log.AppendText(line + Environment.NewLine);
        log.SelectionStart = log.TextLength;
        log.ScrollToCaret();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr window,
        int attribute,
        ref int value,
        int valueSize);
}

internal sealed class RoundedPanel : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 24;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = UiTheme.Hairline;

    public RoundedPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint
            | ControlStyles.SupportsTransparentBackColor,
            true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ConfigureGraphics(e.Graphics);
        var surfaceBounds = Rectangle.Inflate(ClientRectangle, -1, -1);
        using var path = CreatePath(surfaceBounds, Math.Max(1, CornerRadius - 1));
        using var brush = new SolidBrush(BackColor);
        e.Graphics.FillPath(brush, path);
        using var shadowPen = new Pen(Color.FromArgb(13, 23, 23, 23), 1);
        e.Graphics.DrawPath(shadowPen, path);
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        using var borderPen = new Pen(BorderColor, 1);
        var borderBounds = Rectangle.Inflate(ClientRectangle, -1, -1);
        using var borderPath = CreatePath(
            borderBounds,
            Math.Max(1, CornerRadius - 1));
        e.Graphics.DrawPath(borderPen, borderPath);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent is RoundedPanel
            ? UiTheme.Paper
            : UiTheme.Canvas);
    }

    private static void ConfigureGraphics(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
    }

    private static GraphicsPath CreatePath(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var path = new GraphicsPath();
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class PillButton : Button
{
    private bool hovered;

    public PillButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint
            | ControlStyles.SupportsTransparentBackColor,
            true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
        var bounds = Rectangle.Inflate(ClientRectangle, -1, -1);
        int radius = Math.Max(8, bounds.Height / 2);
        using var path = CreatePath(bounds, radius);
        using var fill = new SolidBrush(
            hovered ? ControlPaint.Dark(BackColor, 0.03f) : BackColor);
        e.Graphics.FillPath(fill, path);

        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        using var border = new Pen(
            hovered
                ? UiTheme.InkSoft
                : UiTheme.Hairline,
            1);
        e.Graphics.DrawPath(border, path);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            ClientRectangle,
            ForeColor,
            TextFormatFlags.HorizontalCenter
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.SingleLine);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent is RoundedPanel
            ? UiTheme.Paper
            : UiTheme.Canvas);
    }

    private static GraphicsPath CreatePath(Rectangle bounds, int radius)
    {
        int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class ValueBadge : Label
{
    public ValueBadge()
    {
        AutoSize = false;
        TextAlign = ContentAlignment.MiddleCenter;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint
            | ControlStyles.SupportsTransparentBackColor,
            true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
        using var path = CreateRoundedPath(Rectangle.Inflate(ClientRectangle, -1, -1), 9);
        using var brush = new SolidBrush(UiTheme.InputFill);
        e.Graphics.FillPath(brush, path);
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        using var border = new Pen(UiTheme.Hairline, 1);
        using var borderPath = CreateRoundedPath(Rectangle.Inflate(ClientRectangle, -1, -1), 9);
        e.Graphics.DrawPath(border, borderPath);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            ClientRectangle,
            ForeColor,
            TextFormatFlags.HorizontalCenter
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.SingleLine);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(UiTheme.Paper);
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(
            bounds.Right - diameter,
            bounds.Y,
            diameter,
            diameter,
            270,
            90);
        path.AddArc(
            bounds.Right - diameter,
            bounds.Bottom - diameter,
            diameter,
            diameter,
            0,
            90);
        path.AddArc(
            bounds.X,
            bounds.Bottom - diameter,
            diameter,
            diameter,
            90,
            90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class GradientBadge : Label
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color StartColor { get; set; } = UiTheme.Paper;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color EndColor { get; set; } = UiTheme.Paper;

    public GradientBadge()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint
            | ControlStyles.SupportsTransparentBackColor,
            true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
        using var path = CreatePath(Rectangle.Inflate(ClientRectangle, -1, -1), 11);
        using var brush = new SolidBrush(StartColor);
        e.Graphics.FillPath(brush, path);
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        using var highlight = new Pen(UiTheme.Hairline, 1);
        using var highlightPath = CreatePath(Rectangle.Inflate(ClientRectangle, -1, -1), 11);
        e.Graphics.DrawPath(highlight, highlightPath);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            ClientRectangle,
            ForeColor,
            TextFormatFlags.HorizontalCenter
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.SingleLine);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(UiTheme.Canvas);
    }

    private static GraphicsPath CreatePath(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(
            bounds.Right - diameter,
            bounds.Y,
            diameter,
            diameter,
            270,
            90);
        path.AddArc(
            bounds.Right - diameter,
            bounds.Bottom - diameter,
            diameter,
            diameter,
            0,
            90);
        path.AddArc(
            bounds.X,
            bounds.Bottom - diameter,
            diameter,
            diameter,
            90,
            90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class ModernSlider : Control
{
    private int value;

    public event EventHandler? ValueChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minimum { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum { get; set; } = 100;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor { get; set; } = Color.DodgerBlue;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color EndColor { get; set; } = Color.MediumPurple;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => value;
        set
        {
            int next = Math.Clamp(value, Minimum, Maximum);
            if (this.value == next)
            {
                return;
            }

            this.value = next;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ModernSlider()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.CompositingQuality = CompositingQuality.HighQuality;

        int centerY = Height / 2;
        var track = new Rectangle(8, centerY - 1, Width - 16, 2);
        float ratio = Maximum == Minimum
            ? 0
            : (float)(Value - Minimum) / (Maximum - Minimum);
        int thumbX = track.Left + (int)(track.Width * ratio);

        using var inactiveBrush = new SolidBrush(UiTheme.Hairline);
        using var thumbBrush = new SolidBrush(UiTheme.Paper);

        e.Graphics.FillRoundedRectangle(inactiveBrush, track, 3);
        if (thumbX > track.Left)
        {
            var activeBounds = new Rectangle(
                track.Left,
                track.Top,
                Math.Max(1, thumbX - track.Left),
                track.Height);
            using var activeBrush = new SolidBrush(AccentColor);
            e.Graphics.FillRoundedRectangle(
                activeBrush,
                activeBounds,
                3);
        }

        e.Graphics.FillEllipse(
            thumbBrush,
            thumbX - 5,
            centerY - 5,
            10,
            10);
        using var thumbBorder = new Pen(UiTheme.Hairline, 1);
        e.Graphics.DrawEllipse(thumbBorder, thumbX - 5, centerY - 5, 10, 10);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Capture = true;
        SetValueFromX(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Capture)
        {
            SetValueFromX(e.X);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        Capture = false;
    }

    private void SetValueFromX(int x)
    {
        int usableWidth = Math.Max(1, Width - 16);
        float ratio = Math.Clamp((float)(x - 8) / usableWidth, 0, 1);
        Value = Minimum + (int)Math.Round((Maximum - Minimum) * ratio);
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(
        this Graphics graphics,
        Brush brush,
        Rectangle bounds,
        int radius)
    {
        using var path = new GraphicsPath();
        int diameter = radius * 2;

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(
            bounds.Right - diameter,
            bounds.Y,
            diameter,
            diameter,
            270,
            90);
        path.AddArc(
            bounds.Right - diameter,
            bounds.Bottom - diameter,
            diameter,
            diameter,
            0,
            90);
        path.AddArc(
            bounds.X,
            bounds.Bottom - diameter,
            diameter,
            diameter,
            90,
            90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}
