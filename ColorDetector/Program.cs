using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        timeBeginPeriod(1);
        ApplicationConfiguration.Initialize();

        try
        {
            DetectionSettings settings = SettingsStorage.Load();
            var controls = new ControlForm(settings);
            var overlay = new OverlayForm(settings);
            var crosshair = new CrosshairOverlay(settings);

            Input.Initialize();
            overlay.Show();
            crosshair.Show();
            controls.FormClosed += (_, _) =>
            {
                overlay.Close();
                crosshair.Close();
            };
            Application.Run(controls);
        }
        finally
        {
            timeEndPeriod(1);
        }
    }

    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint period);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint period);
}
