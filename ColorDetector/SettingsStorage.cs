using System.Text.Json;

public static class SettingsStorage
{
    private static readonly string FilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "settings.json");

    public static DetectionSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new DetectionSettings();
            }

            string json = File.ReadAllText(FilePath);
            PersistedSettings? saved =
                JsonSerializer.Deserialize<PersistedSettings>(json);
            if (saved is null)
            {
                return new DetectionSettings();
            }

            return new DetectionSettings
            {
                ReactionTimeMs = saved.ReactionTimeMs,
                DetectionFps = saved.DetectionFps,
                OverlayCircleSize = saved.OverlayCircleSize,
                OverlayCircleVisible = saved.OverlayCircleVisible,
                SnapBackDelayHalfMilliseconds =
                    saved.SnapBackDelayHalfMilliseconds,
                SnapBackTimeMultiplierPercent =
                    saved.SnapBackTimeMultiplierPercent,
                ToggleActivationMode = saved.ToggleActivationMode,
                DetectorEnabled = saved.DetectorEnabled,
                LinearAimMode = saved.LinearAimMode,
                PreviewCircleSize = saved.PreviewCircleSize,
                PreviewCircleVisibleOnScreen =
                    saved.PreviewCircleVisibleOnScreen,
                FinalDownPixels = saved.FinalDownPixels,
                FirstMovementDelayMs = saved.FirstMovementDelayMs,
                SecondMovementDelayMs = saved.SecondMovementDelayMs,
                ThirdMovementDelayMs = saved.ThirdMovementDelayMs,
                StickEnabled = saved.StickEnabled,
                StickSpeedPercent = saved.StickSpeedPercent,
                StickSmoothnessPercent = saved.StickSmoothnessPercent,
                StickPredictionEnabled = saved.StickPredictionEnabled,
                StickStabilizationMethod = saved.StickStabilizationMethod,
                StickVerticalDeadzonePixels =
                    saved.StickVerticalDeadzonePixels,
                AimPauseOnLeftClickEnabled =
                    saved.AimPauseOnLeftClickEnabled,
                AimPauseOnLeftClickMilliseconds =
                    saved.AimPauseOnLeftClickMilliseconds,
                StickActivationVirtualKey = saved.StickActivationVirtualKey,
                StickToggleActivationMode = saved.StickToggleActivationMode
            };
        }
        catch (Exception ex)
        {
            AppLog.Write($"Could not load settings: {ex.Message}");
            return new DetectionSettings();
        }
    }

    public static void Save(DetectionSettings settings)
    {
        try
        {
            var saved = new PersistedSettings
            {
                ReactionTimeMs = settings.ReactionTimeMs,
                DetectionFps = settings.DetectionFps,
                OverlayCircleSize = settings.OverlayCircleSize,
                OverlayCircleVisible = settings.OverlayCircleVisible,
                SnapBackDelayHalfMilliseconds =
                    settings.SnapBackDelayHalfMilliseconds,
                SnapBackTimeMultiplierPercent =
                    settings.SnapBackTimeMultiplierPercent,
                ToggleActivationMode = settings.ToggleActivationMode,
                DetectorEnabled = settings.DetectorEnabled,
                LinearAimMode = settings.LinearAimMode,
                PreviewCircleSize = settings.PreviewCircleSize,
                PreviewCircleVisibleOnScreen =
                    settings.PreviewCircleVisibleOnScreen,
                FinalDownPixels = settings.FinalDownPixels,
                FirstMovementDelayMs = settings.FirstMovementDelayMs,
                SecondMovementDelayMs = settings.SecondMovementDelayMs,
                ThirdMovementDelayMs = settings.ThirdMovementDelayMs,
                StickEnabled = settings.StickEnabled,
                StickSpeedPercent = settings.StickSpeedPercent,
                StickSmoothnessPercent = settings.StickSmoothnessPercent,
                StickPredictionEnabled = settings.StickPredictionEnabled,
                StickStabilizationMethod = settings.StickStabilizationMethod,
                StickVerticalDeadzonePixels =
                    settings.StickVerticalDeadzonePixels,
                AimPauseOnLeftClickEnabled =
                    settings.AimPauseOnLeftClickEnabled,
                AimPauseOnLeftClickMilliseconds =
                    settings.AimPauseOnLeftClickMilliseconds,
                StickActivationVirtualKey = settings.StickActivationVirtualKey,
                StickToggleActivationMode = settings.StickToggleActivationMode
            };

            string json = JsonSerializer.Serialize(
                saved,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex)
        {
            AppLog.Write($"Could not save settings: {ex.Message}");
        }
    }

    private sealed class PersistedSettings
    {
        public int ReactionTimeMs { get; set; }
        public int DetectionFps { get; set; } = 1000;
        public int OverlayCircleSize { get; set; } = 50;
        public bool OverlayCircleVisible { get; set; } = true;
        public int SnapBackDelayHalfMilliseconds { get; set; } = 4;
        public int SnapBackTimeMultiplierPercent { get; set; } = 100;
        public bool ToggleActivationMode { get; set; }
        public bool DetectorEnabled { get; set; } = true;
        public bool LinearAimMode { get; set; }
        public int PreviewCircleSize { get; set; } = 50;
        public bool PreviewCircleVisibleOnScreen { get; set; }
        public int FinalDownPixels { get; set; } = 2;
        public int FirstMovementDelayMs { get; set; }
        public int SecondMovementDelayMs { get; set; }
        public int ThirdMovementDelayMs { get; set; }
        public bool StickEnabled { get; set; }
        public int StickSpeedPercent { get; set; } = 100;
        public int StickSmoothnessPercent { get; set; } = 75;
        public bool StickPredictionEnabled { get; set; } = true;
        public int StickStabilizationMethod { get; set; } = 1;
        public int StickVerticalDeadzonePixels { get; set; } = 5;
        public bool AimPauseOnLeftClickEnabled { get; set; }
        public int AimPauseOnLeftClickMilliseconds { get; set; } = 250;
        public int StickActivationVirtualKey { get; set; } = 0xA5;
        public bool StickToggleActivationMode { get; set; } = true;
    }
}
