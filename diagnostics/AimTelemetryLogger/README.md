# Aim Telemetry Logger

Standalone read-only logger for first-attempt aiming diagnostics. It has no
project reference to ColorDetector, does not open the Cardputer serial port,
and does not generate keyboard or mouse input.

Run it alongside ColorDetector, focus the simulator, and press `Q` normally.
For two seconds after each physical Q press it independently captures the
primary display, finds the closest exact `colors.txt` target satisfying the
11-connected-pixel rule, and writes a CSV trajectory under `logs` beside the
executable.

Each row contains elapsed time, observed target coordinate, intended screen
center, X/Y error, distance error, and detection status. Press `Esc` to exit.
