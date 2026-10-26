# ProMouse Agent Notes

ProMouse is a Windows CLI utility that uses SDL3 to read Nintendo Switch Pro Controller input and Win32 `SendInput` to control mouse and keyboard. The entry point and behavior live in `src/Program.cs`; project settings and dependencies are in `src/ProMouse.csproj`.

## Environment

- Target: `net10.0-windows`, `win-x64`; Windows Forms is enabled for the system-tray menu.
- Dependencies: `SDL3-CS` and `SDL3-CS.Windows`, pinned to `3.4.18`. The latter supplies the native SDL3 runtime.
- Requires the .NET 10 SDK to build. Check availability with `dotnet --info`.

## Build and publish

Run from the repository root. The project file is `ProMouse.csproj` at the root:

```sh
dotnet build ProMouse.csproj -p:PublishAot=false -p:SelfContained=false
dotnet publish ProMouse.csproj -c Release -r win-x64 --self-contained true -p:PublishAot=false
```

On the configured Hermes Windows setup, `Build-ProMouse.bat` publishes a self-contained release to `bin/Release/net10.0-windows/win-x64/publish-new/`. The default `dotnet publish` output is `bin/Release/net10.0-windows/win-x64/publish/`. `Jalankan-ProMouse.bat` launches the default publish output and pauses after exit to expose errors. Build and publish artifacts stay in repository-root `bin/` and `obj/` folders.

## Code conventions

- Keep implementation in the existing `internal static class Program` in `Program.cs`; P/Invoke declarations and SDL event structs are declared alongside the input loop.
- Use SDL gamepad axis indices `0..5` (left X/Y, right X/Y, right/left trigger) and SDL gamepad button indices defined near the top of `Program.cs`.
- Mouse and keyboard synthesis uses Win32 `SendInput`; release held inputs in `ReleaseAll()` on shutdown/disconnect/pause paths.
- Runtime status text is Indonesian. Current bindings: A left click, B right click, X middle click, + Windows, D-pad arrows, R1/L1 Alt-Tab/reverse Alt-Tab, R2 Ctrl+Tab, L2 Ctrl+Shift+Tab, R3 toggles gyro, Y toggles CLI. Update README and CLI help text whenever bindings change.

## Pitfalls

- `PublishAot` must remain disabled: WinForms/trimming is rejected by the SDK. Publish with `-p:PublishAot=false`.
- SDL3 APIs are Cdecl; keep their P/Invoke declarations consistent with SDL3 signatures. SDL event structs must preserve the explicit 128-byte union and the current x64 field offsets.
- `SDL_GetGamepads` returns an SDL-allocated ID array; release it with `SDL_free`.
- The publish directory is the user-facing app; rebuilding Debug alone does not update its executable.
- There is no test project, test script, or lint configuration in the repository. Verify changes with the exact build and publish commands above; controller behavior still needs a physical-device test.
