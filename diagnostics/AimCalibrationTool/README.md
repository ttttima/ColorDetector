# Cardputer Aim Calibration Tool

This is a separate diagnostic program for measuring the exact mapping between
Cardputer relative USB HID reports and movement observed in a yellow-target
simulator on the primary display.

Close ColorDetector before starting this tool so only one process owns the
Cardputer serial port.

## Controls

- `F8`: measure the complete 2x2 HID-to-cursor movement matrix with +40 X
  and +40 Y probes.
- `Q`: detect the topmost connected yellow target and make one straight move
  from that point to screen center using the saved matrix.
- `Esc`: exit.

Calibration is saved as `aim-calibration.json` beside the executable. The
detector requires an exact color from `colors.txt` and at least 11 connected
saved-color pixels, matching the main application's target rule.

All generated movement is sent through the persistent Cardputer serial
connection. The tool does not use Windows input injection.

Non-interactive modes:

```powershell
AimCalibrationTool.exe --calibrate
AimCalibrationTool.exe --calibrate-visual
AimCalibrationTool.exe --aim
AimCalibrationTool.exe --aim-visual
```

`--aim` centers the real cursor using Cardputer HID, performs one straight
center-to-target movement, reports residual error, and updates the persisted
full-distance gain from the observed result.

`--calibrate-visual` keeps the simulator as the foreground application and
derives the matrix from movement of the detected yellow target itself. Use
this mode for a raw-input or locked-camera simulator.

`--aim-visual` solves the saved visual matrix and performs one straight
Cardputer movement from the detected target to screen center without reading
or generating Windows cursor input.
