<div align="center">

# ProMouse

**Turn a Nintendo Switch Pro Controller into a Windows mouse and keyboard.**

[![Platform](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![SDL](https://img.shields.io/badge/SDL-3-1E1E1E?logo=SDL&logoColor=white)](https://www.libsdl.org)
[![License](https://img.shields.io/badge/license-not%20specified-lightgrey)](#license)

</div>

---

## What is this?

ProMouse is a Windows tray and CLI utility that reads Nintendo Switch Pro Controller input through SDL3 and sends mouse and keyboard input through Win32 `SendInput`.

Use the left stick and gyro to move the pointer, the right stick to scroll, and controller buttons for mouse clicks, keyboard keys, and shortcuts. Sensitivity and scroll behavior can be adjusted in `config.ini` without rebuilding.

## Features

- **Pointer control** — left stick with a configurable response curve, plus gyro aiming.
- **Gyro drift reduction** — calibrates the gyro while the controller is held still when gyro is enabled.
- **Smooth scrolling** — analog right stick scrolls vertically and horizontally, with configurable speed and direction.
- **Live configuration** — edit `config.ini` and press F5 to reload settings.
- **Tray controls and status** — see controller connection, gyro, and input pause state; pause or resume from the tray menu.
- **Controller reconnect** — detects disconnects and opens a controller again when it reconnects.
- **Mouse and keyboard bindings** — controller buttons provide clicks, Enter, Space, Windows, arrows, Alt-Tab, and browser tab shortcuts.
- **Windows cursor** — uses the system's normal cursor; ProMouse does not replace it.

## Requirements

- Windows 10/11, x64
- Nintendo Switch Pro Controller
- .NET 10 SDK to build from source

The published application is self-contained; users can run `ProMouse.exe` without installing the .NET runtime. SDL3 native runtime is supplied by the `SDL3-CS.Windows` package.

## Getting started

### Run a published build

Download or publish the Windows x64 build, then run `ProMouse.exe`. The app creates a default `config.ini` beside the executable on first launch. Connect the controller over USB or Bluetooth.

### Build from source

```bash
dotnet build ProMouse.csproj -p:PublishAot=false -p:SelfContained=false
```

### Publish a self-contained release

```bash
dotnet publish ProMouse.csproj -c Release -r win-x64 --self-contained true -p:PublishAot=false
```

The release executable is written to:

```text
bin/Release/net10.0-windows/win-x64/publish/ProMouse.exe
```

Run `Jalankan-ProMouse.bat` from the repository root to launch that published build and keep the console open after exit.

## Controls

| Controller input | Action |
| --- | --- |
| Left stick | Move pointer |
| Gyro | Aim pointer; toggle with R3 |
| Right stick up/down | Vertical scroll |
| Right stick left/right | Horizontal scroll |
| A | Left mouse click |
| B | Right mouse click |
| X | Enter |
| + | Windows key |
| D-pad | Arrow keys |
| R1 / L1 | Alt-Tab / reverse Alt-Tab |
| R2 / L2 | Next / previous browser tab |
| Y | Show or hide the CLI |
| Tray menu | Pause/resume input, show/hide CLI, or quit |
| F5 | Reload `config.ini` |
| Ctrl+C | Quit |

When enabling gyro, hold the controller still during the configured calibration interval.

## Configuration

`config.ini` is created beside the executable. Values use invariant decimal notation (a dot for decimal separator). Edit the file and press F5 to apply changes immediately.

| Setting | Default | Description |
| --- | ---: | --- |
| `StickDeadzone` | `0.12` | Left-stick deadzone, from `0` to `0.8` |
| `MouseStickGain` | `14` | Pointer movement gain for the left stick |
| `StickCurve` | `2` | Stick response exponent; higher values favor precision near center |
| `GyroGain` | `24` | Gyro pointer sensitivity |
| `GyroDeadzone` | `0.035` | Gyro angular-velocity deadzone |
| `GyroCalibrationSeconds` | `1.5` | Time to hold still while calibrating gyro bias |
| `ScrollMaxSpeed` | `30` | Maximum right-stick scroll speed |
| `VerticalScrollDirection` | `1` | Vertical direction (`1` normal, `-1` reversed) |
| `HorizontalScrollDirection` | `1` | Horizontal direction (`1` normal, `-1` reversed) |

Invalid or out-of-range values fall back to their supported range or default. Gyro calibration runs at startup when gyro is enabled and is reset when R3 enables gyro again.

## Tech stack

| Layer | Choice |
| --- | --- |
| Runtime | .NET 10, `net10.0-windows`, `win-x64` |
| Controller input | SDL3 via `SDL3-CS` 3.4.18 |
| Native SDL runtime | `SDL3-CS.Windows` 3.4.18 |
| Mouse and keyboard output | Win32 `SendInput` |
| System tray | Windows Forms `NotifyIcon` |

## Project layout

| Path | Purpose |
| --- | --- |
| `src/Program.cs` | Controller polling, input mappings, tray UI, and Win32/SDL declarations |
| `ProMouse.csproj` | Target framework, publish settings, and package references |
| `Jalankan-ProMouse.bat` | Launch the published executable and pause after exit |
| `AGENTS.md` | Repository-specific development notes |

## Development and verification

Build and publish from the repository root. Native controller behavior—especially gyro calibration, stick direction, scroll feel, and reconnect—requires testing with a physical Nintendo Switch Pro Controller.

```bash
dotnet build ProMouse.csproj -p:PublishAot=false -p:SelfContained=false
dotnet publish ProMouse.csproj -c Release -r win-x64 --self-contained true -p:PublishAot=false
```

There is currently no separate test project or lint configuration. A successful build does not replace physical-device testing.

## License

No license is currently specified. All rights reserved unless a license is added to this repository.
