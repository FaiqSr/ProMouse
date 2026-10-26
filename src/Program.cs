using System.Runtime.InteropServices;
using System.Text;
using SDL3;

internal static class Program
{
    private const uint SDL_INIT_JOYSTICK = 0x00000200;
    private const uint SDL_INIT_GAMEPAD = 0x00002000;
    private const uint SDL_EVENT_QUIT = 0x100;
    private const uint SDL_EVENT_GAMEPAD_BUTTON_DOWN = 0x650;
    private const uint SDL_EVENT_GAMEPAD_BUTTON_UP = 0x651;
    private const uint SDL_EVENT_GAMEPAD_ADDED = 0x653;
    private const uint SDL_EVENT_GAMEPAD_REMOVED = 0x654;
    private const uint SDL_EVENT_KEY_DOWN = 0x300;
    private const int SDL_SENSOR_GYRO = 2;
    private const uint SDL_BUTTON_SOUTH = 0, SDL_BUTTON_EAST = 1, SDL_BUTTON_WEST = 2, SDL_BUTTON_NORTH = 3;
    private const uint SDL_BUTTON_BACK = 4, SDL_BUTTON_START = 6, SDL_BUTTON_LEFT_STICK = 7, SDL_BUTTON_RIGHT_STICK = 8;
    private const uint SDL_BUTTON_RIGHT_SHOULDER = 10, SDL_BUTTON_LEFT_SHOULDER = 9;
    private const uint SDL_BUTTON_DPAD_UP = 11, SDL_BUTTON_DPAD_DOWN = 12, SDL_BUTTON_DPAD_LEFT = 13, SDL_BUTTON_DPAD_RIGHT = 14;
    private const uint SDL_BUTTON_GUIDE = 5;
    private static readonly IntPtr HWND = GetConsoleWindow();
    private static readonly NotifyIcon Tray = new();
    private static bool active = true, visible = true, running = true;
    private static IntPtr gamepad;
    private static bool prevSouth, prevEast, prevWest, prevNorth, prevBack, prevStart, prevL3, prevR3, prevLB, prevRB;
    private static bool prevL2, prevR2, gyroEnabled = true, gyroHeld;
    private static bool prevUp, prevDown, prevLeft, prevRight;
    private static float StickDeadzone = 0.12f;
    private static float MouseStickGain = 14f;
    private static float StickCurve = 2f;
    private static float ScrollMaxSpeed = 30f;
    private static float GyroGain = 24f, GyroDeadzone = 0.035f, GyroCalibrationSeconds = 1.5f;
    private static float VerticalScrollDirection = 1f, HorizontalScrollDirection = 1f;
    private static readonly float[] gyroBias = new float[3];
    private static readonly float[] gyroCalibrationSum = new float[3];
    private static int gyroCalibrationSamples;
    private static long gyroCalibrationStarted;
    private static string controllerStatus = "Terputus";
    private static float scrollRemainder, horizontalScrollRemainder;
    private static float gyroMouseRemainderX, gyroMouseRemainderY;
    private static long lastScrollUpdate;
    private static long lastMove, lastStatusUpdate;
    private static readonly float[] gyro = new float[3];

    [STAThread]
    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Tray.Initialize();
        LoadConfig();
        SetConsoleCtrlHandler(CtrlHandler, true);
        SDL_SetHint("SDL_JOYSTICK_ENHANCED_REPORTS", "1");
        if (!SDL_Init(SDL_INIT_JOYSTICK | SDL_INIT_GAMEPAD))
        { Console.WriteLine("SDL init gagal: " + SDL_GetError()); return 1; }
        OpenFirstGamepad();
        Console.WriteLine("ProMouse — Switch Pro Controller ke mouse & keyboard");
        Console.WriteLine("Y: sembunyikan/tampilkan CLI  |  Ctrl+C: keluar  |  Tray: klik kanan untuk kontrol");
        Console.WriteLine("Analog kiri/gyro = gerakkan mouse; analog kanan = scroll. R3 toggle gyro. R1/L1 = Alt-Tab/Alt-Shift-Tab; R2/L2 = tab browser berikut/sebelumnya.");
        Console.WriteLine("A klik kiri, B klik kanan, X klik tengah, + Windows, D-pad panah.");

        while (running)
        {
            while (SDL_PollEvent(out var e))
            {
                if (e.type == SDL_EVENT_QUIT) running = false;
                if (e.type == SDL_EVENT_GAMEPAD_ADDED && gamepad == IntPtr.Zero) OpenGamepad(e.gdevice.which);
                if (e.type == SDL_EVENT_GAMEPAD_REMOVED && e.gdevice.which == SDL_GetGamepadInstanceID(gamepad)) CloseGamepad();
                if (e.type == SDL_EVENT_GAMEPAD_BUTTON_DOWN && e.gbutton.button == SDL_BUTTON_NORTH) ToggleConsole();
                if (e.type == SDL_EVENT_KEY_DOWN && e.key.key == 0x4000003E) LoadConfig();
            }
            long statusNow = Environment.TickCount64;
            if (statusNow - lastStatusUpdate >= 1000) { UpdateStatus(); lastStatusUpdate = statusNow; }
            if (gamepad != IntPtr.Zero) ReadInput();
            Thread.Sleep(5);
        }
        ReleaseAll(); CloseGamepad(); SDL_Quit(); Tray.Dispose(); return 0;
    }

    private static void OpenFirstGamepad()
    {
        IntPtr ids = SDL_GetGamepads(out int count);
        if (ids != IntPtr.Zero)
        {
            try { if (count > 0) OpenGamepad((uint)Marshal.ReadInt32(ids)); }
            finally { SDL_free(ids); }
        }
    }
    private static void OpenGamepad(uint id)
    {
        gamepad = SDL_OpenGamepad(id);
        if (gamepad == IntPtr.Zero) { controllerStatus = "Gagal terhubung"; Console.WriteLine("Gagal membuka controller: " + Marshal.PtrToStringUTF8(SDL_GetError())); return; }
        controllerStatus = Marshal.PtrToStringUTF8(SDL_GetGamepadName(gamepad)) ?? "Terhubung";
        Console.WriteLine("Terhubung: " + controllerStatus);
        ResetGyroCalibration();
        if (SDL_GamepadHasSensor(gamepad, SDL_SENSOR_GYRO)) SDL_SetGamepadSensorEnabled(gamepad, SDL_SENSOR_GYRO, true);
        else Console.WriteLine("Gyro tidak terdeteksi oleh SDL untuk device ini.");
    }
    private static void CloseGamepad() { ReleaseAll(); if (gamepad != IntPtr.Zero) SDL_CloseGamepad(gamepad); gamepad = IntPtr.Zero; controllerStatus = "Terputus"; Console.WriteLine("Controller terputus."); }
    private static void UpdateStatus()
    {
        string state = $"Controller: {(gamepad != IntPtr.Zero ? controllerStatus : "Terputus")} | Gyro: {(gyroEnabled ? "aktif" : "mati")} | Input: {(active ? "aktif" : "dijeda")}";
        if (visible)
        {
            try
            {
                int row = Console.CursorTop;
                Console.Write("\r" + state.PadRight(Math.Max(0, Console.WindowWidth - 1)));
                Console.SetCursorPosition(0, row);
            }
            catch (IOException) { visible = false; }
            catch (InvalidOperationException) { visible = false; }
        }
        Tray.SetStatus(state);
    }

    private static void LoadConfig()
    {
        const string defaults = "; ProMouse config — perubahan dibaca saat startup atau F5\nStickDeadzone=0.12\nMouseStickGain=14\nStickCurve=2\nGyroGain=24\nGyroDeadzone=0.035\nGyroCalibrationSeconds=1.5\nScrollMaxSpeed=30\nVerticalScrollDirection=1\nHorizontalScrollDirection=1\n";
        string path = Path.Combine(AppContext.BaseDirectory, "config.ini");
        if (!File.Exists(path)) File.WriteAllText(path, defaults);
        var values = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in File.ReadAllLines(path))
        {
            string[] pair = line.Split('=', 2);
            if (pair.Length == 2 && float.TryParse(pair[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value) && float.IsFinite(value)) values[pair[0].Trim()] = value;
        }
        float Get(string key, float fallback, float min, float max) => values.TryGetValue(key, out float value) ? Math.Clamp(value, min, max) : fallback;
        StickDeadzone = Get("StickDeadzone", 0.12f, 0f, 0.8f);
        MouseStickGain = Get("MouseStickGain", 14f, 0f, 500f);
        StickCurve = Get("StickCurve", 2f, 0.2f, 5f);
        GyroGain = Get("GyroGain", 24f, 0f, 1000f);
        GyroDeadzone = Get("GyroDeadzone", 0.035f, 0f, 10f);
        GyroCalibrationSeconds = Get("GyroCalibrationSeconds", 1.5f, 0.1f, 10f);
        ScrollMaxSpeed = Get("ScrollMaxSpeed", 30f, 0f, 500f);
        VerticalScrollDirection = Get("VerticalScrollDirection", 1f, -1f, 1f) < 0 ? -1f : 1f;
        HorizontalScrollDirection = Get("HorizontalScrollDirection", 1f, -1f, 1f) < 0 ? -1f : 1f;
        Console.WriteLine("Config dimuat: " + path);
    }

    private static void ResetGyroCalibration()
    {
        Array.Clear(gyroBias); Array.Clear(gyroCalibrationSum);
        gyroCalibrationSamples = 0; gyroCalibrationStarted = Environment.TickCount64;
        gyroMouseRemainderX = gyroMouseRemainderY = 0;
    }

    private static void ReadInput()
    {
        if (active)
        {
            long now = Environment.TickCount64;
            if (now - lastMove >= 8)
            {
                float elapsedMove = Math.Clamp((now - lastMove) / 1000f, 0f, 0.05f);
                int dx = StickDelta(Axis(0), MouseStickGain), dy = StickDelta(Axis(1), MouseStickGain);
                if (gyroEnabled && !gyroHeld)
                {
                    if (SDL_GetGamepadSensorData(gamepad, SDL_SENSOR_GYRO, gyro, 3))
                    {
                        if (gyroCalibrationSamples >= 0)
                        {
                            if (now - gyroCalibrationStarted < (long)(GyroCalibrationSeconds * 1000))
                            {
                                for (int i = 0; i < 3; i++) gyroCalibrationSum[i] += gyro[i];
                                gyroCalibrationSamples++;
                            }
                            else if (gyroCalibrationSamples > 0)
                            {
                                for (int i = 0; i < 3; i++) gyroBias[i] = gyroCalibrationSum[i] / gyroCalibrationSamples;
                                gyroCalibrationSamples = -1;
                                Console.WriteLine("Kalibrasi gyro selesai.");
                            }
                        }
                        if (gyroCalibrationSamples == -1)
                        {
                            gyroMouseRemainderX += -ApplyGyroDeadzone(gyro[1] - gyroBias[1]) * GyroGain * elapsedMove;
                            gyroMouseRemainderY += -ApplyGyroDeadzone(gyro[0] - gyroBias[0]) * GyroGain * elapsedMove;
                            int gyroDx = (int)gyroMouseRemainderX, gyroDy = (int)gyroMouseRemainderY;
                            dx += gyroDx; dy += gyroDy;
                            gyroMouseRemainderX -= gyroDx; gyroMouseRemainderY -= gyroDy;
                        }
                    }
                }
                if (dx != 0 || dy != 0) MouseMove(dx, dy);
                lastMove = now;
            }
            float elapsed = Math.Clamp((now - lastScrollUpdate) / 1000f, 0f, 0.03f);
            lastScrollUpdate = now;
            scrollRemainder += -ApplyDeadzone(Axis(3)) * ScrollMaxSpeed * VerticalScrollDirection * elapsed;
            int wheel = (int)scrollRemainder;
            if (wheel != 0) { MouseWheel(wheel); scrollRemainder -= wheel; }
            horizontalScrollRemainder += ApplyDeadzone(Axis(2)) * ScrollMaxSpeed * HorizontalScrollDirection * elapsed;
            int horizontalWheel = (int)horizontalScrollRemainder;
            if (horizontalWheel != 0) { MouseHorizontalWheel(horizontalWheel); horizontalScrollRemainder -= horizontalWheel; }
        }
        else { scrollRemainder = horizontalScrollRemainder = 0; lastScrollUpdate = Environment.TickCount64; }

        bool south = Button(SDL_BUTTON_SOUTH), east = Button(SDL_BUTTON_EAST), west = Button(SDL_BUTTON_WEST), north = Button(SDL_BUTTON_NORTH);
        bool back = Button(SDL_BUTTON_BACK), start = Button(SDL_BUTTON_START), l3 = Button(SDL_BUTTON_LEFT_STICK), r3 = Button(SDL_BUTTON_RIGHT_STICK);
        bool lb = Button(SDL_BUTTON_LEFT_SHOULDER), rb = Button(SDL_BUTTON_RIGHT_SHOULDER);
        bool l2 = SDL_GetGamepadAxis(gamepad, 5) > 16000, r2 = SDL_GetGamepadAxis(gamepad, 4) > 16000;
        bool up = Button(SDL_BUTTON_DPAD_UP), down = Button(SDL_BUTTON_DPAD_DOWN), left = Button(SDL_BUTTON_DPAD_LEFT), right = Button(SDL_BUTTON_DPAD_RIGHT);
        gyroHeld = l3;
        if (active)
        {
            SetMouse(1, south, prevSouth); SetMouse(0, east, prevEast); SetMouse(2, west, prevWest);
            if (Pressed(r3, prevR3))
            {
                gyroEnabled = !gyroEnabled;
                if (gyroEnabled) ResetGyroCalibration();
                Console.WriteLine(gyroEnabled ? "Gyro aktif — tahan controller diam untuk kalibrasi." : "Gyro nonaktif.");
            }
            if (Pressed(rb, prevRB)) TapAltTab(false);
            if (Pressed(lb, prevLB)) TapAltTab(true);
            if (Pressed(r2, prevR2)) TapCtrlTab(false);
            if (Pressed(l2, prevL2)) TapCtrlTab(true);
            if (Pressed(back, prevBack)) Key(0x20, true); if (Released(back, prevBack)) Key(0x20, false);
            if (Pressed(start, prevStart)) Key(0x5B, true); if (Released(start, prevStart)) Key(0x5B, false);
            if (Pressed(up, prevUp)) Key(0x26, true); if (Released(up, prevUp)) Key(0x26, false);
            if (Pressed(down, prevDown)) Key(0x28, true); if (Released(down, prevDown)) Key(0x28, false);
            if (Pressed(left, prevLeft)) Key(0x25, true); if (Released(left, prevLeft)) Key(0x25, false);
            if (Pressed(right, prevRight)) Key(0x27, true); if (Released(right, prevRight)) Key(0x27, false);
        }
        prevSouth = south; prevEast = east; prevWest = west; prevNorth = north; prevBack = back; prevStart = start; prevL3 = l3; prevR3 = r3;
        prevLB = lb; prevRB = rb; prevL2 = l2; prevR2 = r2; prevUp = up; prevDown = down; prevLeft = left; prevRight = right;
    }

    private static int StickDelta(float raw, float gain)
    {
        float value = ApplyDeadzone(raw);
        return (int)(MathF.CopySign(MathF.Pow(Math.Abs(value), StickCurve), value) * gain);
    }
    private static float ApplyDeadzone(float raw) => Math.Abs(raw) <= StickDeadzone ? 0f : MathF.CopySign((Math.Abs(raw) - StickDeadzone) / (1f - StickDeadzone), raw);
    private static float ApplyGyroDeadzone(float value) => Math.Abs(value) <= GyroDeadzone ? 0f : MathF.CopySign(Math.Abs(value) - GyroDeadzone, value);
    private static float Axis(int axis) => SDL_GetGamepadAxis(gamepad, axis) / 32768f;
    private static void MouseWheel(int amount) { var input = new INPUT { type = 0, U = new INPUTUNION { mi = new MOUSEINPUT { mouseData = unchecked((uint)(amount * 120)), dwFlags = 0x0800 } } }; SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()); }
    private static void MouseHorizontalWheel(int amount) { var input = new INPUT { type = 0, U = new INPUTUNION { mi = new MOUSEINPUT { mouseData = unchecked((uint)(amount * 120)), dwFlags = 0x1000 } } }; SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()); }
    private static bool Button(uint b) => SDL_GetGamepadButton(gamepad, b);
    private static bool Pressed(bool now, bool old) => now && !old;
    private static bool Released(bool now, bool old) => !now && old;
    private static void SetMouse(uint button, bool now, bool old) { if (Pressed(now, old)) MouseButton(button, true); if (Released(now, old)) MouseButton(button, false); }
    private static void ToggleConsole() { visible = !visible; ShowWindow(HWND, visible ? 5 : 0); if (visible) { Console.WriteLine(active ? "Input aktif." : "Input dijeda."); } }
    private static void ReleaseAll() { for (uint i = 0; i < 3; i++) MouseButton(i, false); foreach (ushort k in new ushort[] { 0x09,0x10,0x12,0x0D,0x20,0x5B,0x26,0x28,0x25,0x27 }) Key(k, false); }
    private static readonly ConsoleHandler CtrlHandler = _ => { running = false; ReleaseAll(); return true; };

    private static void TapCtrlTab(bool reverse)
    {
        INPUT KeyInput(ushort key, bool down) => new() { type = 1, U = new INPUTUNION { ki = new KEYBDINPUT { wVk = key, dwFlags = down ? 0u : 2u } } };
        var inputs = reverse
            ? new[] { KeyInput(0x11, true), KeyInput(0x10, true), KeyInput(0x09, true), KeyInput(0x09, false), KeyInput(0x10, false), KeyInput(0x11, false) }
            : new[] { KeyInput(0x11, true), KeyInput(0x09, true), KeyInput(0x09, false), KeyInput(0x11, false) };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }
    private static void TapAltTab(bool reverse) { Key(0x12, true); if (reverse) Key(0x10, true); Key(0x09, true); Key(0x09, false); if (reverse) Key(0x10, false); Key(0x12, false); }
    private static void TapKey(ushort vk, bool shift)
    {
        INPUT KeyInput(ushort key, bool down) => new() { type = 1, U = new INPUTUNION { ki = new KEYBDINPUT { wVk = key, dwFlags = down ? 0u : 2u } } };
        if (!shift)
        {
            var inputs = new[] { KeyInput(vk, true), KeyInput(vk, false) };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
            return;
        }
        var shiftDown = KeyInput(0x10, true);
        SendInput(1, new[] { shiftDown }, Marshal.SizeOf<INPUT>());
        Thread.Sleep(50);
        var shiftedTap = new[] { KeyInput(vk, true), KeyInput(vk, false), KeyInput(0x10, false) };
        SendInput((uint)shiftedTap.Length, shiftedTap, Marshal.SizeOf<INPUT>());
    }
    private static void Key(ushort vk, bool down) { var input = new INPUT { type = 1, U = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = down ? 0u : 2u } } }; SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()); }
    private static void MouseButton(uint b, bool down) { uint flag = b switch { 0 => down ? 0x0002u : 0x0004u, 1 => down ? 0x0008u : 0x0010u, 2 => down ? 0x0020u : 0x0040u, 3 => down ? 0x0800u : 0x1000u, _ => down ? 0x0800u : 0x1000u }; var input = new INPUT { type = 0, U = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = flag } } }; SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()); }
    private static void MouseMove(int dx, int dy) { var input = new INPUT { type = 0, U = new INPUTUNION { mi = new MOUSEINPUT { dx = dx, dy = dy, dwFlags = 0x0001 } } }; SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()); }

    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public INPUTUNION U; }
    [StructLayout(LayoutKind.Explicit)] private struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT { public int dx,dy; public uint mouseData,dwFlags,time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort wVk,wScan; public uint dwFlags,time; public IntPtr dwExtraInfo; }
    [DllImport("user32.dll")] private static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("kernel32.dll")] private static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int cmd);
    private delegate bool ConsoleHandler(int ctrlType);
    [DllImport("kernel32.dll")] private static extern bool SetConsoleCtrlHandler(ConsoleHandler handler, bool add);
    [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] private static extern bool SDL_Init(uint flags);
    [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] private static extern void SDL_Quit();
    [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] private static extern bool SDL_PollEvent(out SDL_Event e);
    [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr SDL_GetGamepads(out int count);
    [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] private static extern void SDL_free(IntPtr mem);
    [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr SDL_OpenGamepad(uint id);
    [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] private static extern void SDL_CloseGamepad(IntPtr pad);
    [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr SDL_GetGamepadName(IntPtr pad);
    [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] private static extern uint SDL_GetGamepadInstanceID(IntPtr pad);
    [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] private static extern bool SDL_GamepadHasSensor(IntPtr pad, int type);
    [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] private static extern bool SDL_SetGamepadSensorEnabled(IntPtr pad, int type, bool enabled);
    [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] private static extern bool SDL_GetGamepadSensorData(IntPtr pad, int type, [Out] float[] data, int count);
    [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] private static extern bool SDL_GetGamepadButton(IntPtr pad, uint button);
    [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] private static extern short SDL_GetGamepadAxis(IntPtr pad, int axis);
    [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] private static extern bool SDL_SetHint(string name, string value);
    [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr SDL_GetError();

    [StructLayout(LayoutKind.Explicit, Size=128)] private struct SDL_Event { [FieldOffset(0)] public uint type; [FieldOffset(16)] public SDL_DeviceEvent gdevice; [FieldOffset(16)] public SDL_ButtonEvent gbutton; [FieldOffset(16)] public SDL_KeyboardEvent key; }
    [StructLayout(LayoutKind.Sequential)] private struct SDL_KeyboardEvent { public uint type; public uint reserved; public ulong timestamp; public uint windowID; public uint which; public int key; public ushort scancode; public ushort mod; public ushort raw; public byte down; public byte repeat; }
    [StructLayout(LayoutKind.Sequential)] private struct SDL_DeviceEvent { public uint type; public uint padding; public ulong timestamp; public uint which; }
    [StructLayout(LayoutKind.Sequential)] private struct SDL_ButtonEvent { public uint type; public uint padding; public ulong timestamp; public uint which; public byte button; public byte down; }

    private sealed class NotifyIcon : IDisposable
    {
        private readonly Thread thread;
        private System.Windows.Forms.NotifyIcon? icon;
        public NotifyIcon() { thread = new Thread(() => { icon = new System.Windows.Forms.NotifyIcon { Text = "ProMouse", Icon = System.Drawing.SystemIcons.Application, Visible = true, ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip() }; icon.ContextMenuStrip.Items.Add("Tampilkan/sembunyikan CLI", null, (_,_) => ToggleConsole()); icon.ContextMenuStrip.Items.Add("Jeda/aktifkan input", null, (_,_) => { active = !active; if (!active) ReleaseAll(); Console.WriteLine(active ? "Input aktif." : "Input dijeda."); }); icon.ContextMenuStrip.Items.Add("Keluar", null, (_,_) => running = false); System.Windows.Forms.Application.Run(); }) { IsBackground = true }; thread.SetApartmentState(ApartmentState.STA); thread.Start(); }
        public void Initialize() { Thread.Sleep(200); }
        public void SetStatus(string text) { try { if (icon is not null) icon.Text = text.Length > 63 ? text[..63] : text; } catch { } }
        public void Dispose() { if (icon is not null) icon.Visible = false; }
    }
}
