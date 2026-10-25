# ProMouse Agent Notes

ProMouse is a Windows CLI utility that uses SDL3 to read Nintendo Switch Pro Controller input and Win32 `SendInput` to control mouse and keyboard. The entry point and behavior live in `Program.cs`; project settings and dependencies are in `ProMouse.csproj`.

## Environment

- Target: `net10.0-windows`, `win-x64`; Windows Forms is enabled for the system-tray menu.
- Dependencies: `SDL3-CS` and `SDL3-CS.Windows`, pinned to `3.4.18`. The latter supplies the native SDL3 runtime.
- Requires the .NET 10 SDK to build. Check availability with `dotnet --info`.

## Build and publish

Run from the repository root:

```sh
dotnet build -p:PublishAot=false -p:SelfContained=false
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishAot=false
```

Release executable output: `bin/Release/net10.0-windows/win-x64/publish/ProMouse.exe`. `Jalankan-ProMouse.bat` launches this output and pauses after exit to expose errors.

## Code conventions

- Keep implementation in the existing `internal static class Program` in `Program.cs`; P/Invoke declarations and SDL event structs are declared alongside the input loop.
- Use SDL gamepad axis indices `0..5` (left X/Y, right X/Y, right/left trigger) and SDL gamepad button indices defined near the top of `Program.cs`.
- Mouse and keyboard synthesis uses Win32 `SendInput`; release held inputs in `ReleaseAll()` on shutdown/disconnect/pause paths.
- Runtime status text is Indonesian. Preserve the existing button-to-action mapping and update the CLI help text whenever bindings change.

## Pitfalls

- `PublishAot` must remain disabled: WinForms/trimming is rejected by the SDK. Publish with `-p:PublishAot=false`.
- SDL3 APIs are Cdecl; keep their P/Invoke declarations consistent with SDL3 signatures. SDL event structs must preserve the explicit 128-byte union and the current x64 field offsets.
- `SDL_GetGamepads` returns an SDL-allocated ID array; release it with `SDL_free`.
- The publish directory is the user-facing app; rebuilding Debug alone does not update its executable.
- There is no test project, test script, or lint configuration in the repository. Verify changes with the exact build and publish commands above; controller behavior still needs a physical-device test.
