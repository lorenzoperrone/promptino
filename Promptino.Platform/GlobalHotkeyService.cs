using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Promptino.Platform;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

public readonly record struct GlobalHotkey(HotkeyModifiers Modifiers, int VirtualKey)
{
    public static GlobalHotkey Default => new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x20); // Space

    public bool IsValid => Modifiers != HotkeyModifiers.None && VirtualKey is >= 0x08 and <= 0xFE;
}

public readonly record struct HotkeyRegistrationResult(bool Success, bool IsConflict, string? Warning)
{
    public static HotkeyRegistrationResult Ok() => new(true, false, null);
    public static HotkeyRegistrationResult Conflict() => new(false, true, "Global hotkey unavailable: shortcut already in use by another app. Choose a different shortcut.");
    public static HotkeyRegistrationResult Failure(string warning) => new(false, false, warning);
}

public interface IGlobalHotkeyService : IDisposable
{
    event Action<int>? HotkeyPressed;
    HotkeyRegistrationResult UpdateHotkeys(IEnumerable<(int Id, GlobalHotkey Hotkey)> hotkeys);
    void Stop();
}

public sealed class NoOpGlobalHotkeyService : IGlobalHotkeyService
{
    public event Action<int>? HotkeyPressed
    {
        add { }
        remove { }
    }

    public HotkeyRegistrationResult UpdateHotkeys(IEnumerable<(int Id, GlobalHotkey Hotkey)> hotkeys)
        => HotkeyRegistrationResult.Failure("Global hotkeys are available only on Windows.");

    public void Stop() { }
    public void Dispose() { }
}

[SupportedOSPlatform("windows")]
public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService
{
    private const int WmHotkey = 0x0312;

    private readonly object _sync = new();
    private Thread? _thread;
    private AutoResetEvent? _requestSignal;
    private AutoResetEvent? _responseSignal;
    private List<(int Id, GlobalHotkey Hotkey)> _pendingHotkeys = new();
    private bool _hasPending;
    private HotkeyRegistrationResult _pendingResult;
    private readonly HashSet<int> _registeredIds = new();
    private bool _shutdown;

    private readonly object _updateLock = new();

    public event Action<int>? HotkeyPressed;

    public HotkeyRegistrationResult UpdateHotkeys(IEnumerable<(int Id, GlobalHotkey Hotkey)> hotkeys)
    {
        if (!OperatingSystem.IsWindows()) return HotkeyRegistrationResult.Failure("Global hotkeys are available only on Windows.");
        
        var list = hotkeys.ToList();
        foreach (var item in list)
        {
            if (!item.Hotkey.IsValid) return HotkeyRegistrationResult.Failure($"Hotkey {item.Id} is not valid. Use at least one modifier and one key.");
        }

        lock (_updateLock)
        {
            EnsureWorker();

            lock (_sync)
            {
                _pendingHotkeys = list;
                _hasPending = true;
                _requestSignal!.Set();
            }

            _responseSignal!.WaitOne();
            lock (_sync)
            {
                return _pendingResult;
            }
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            _shutdown = true;
            _requestSignal?.Set();
        }

        _thread?.Join(TimeSpan.FromSeconds(1));
        _thread = null;
    }

    private void EnsureWorker()
    {
        if (_thread is not null) return;

        _requestSignal?.Dispose();
        _responseSignal?.Dispose();
        _requestSignal = new AutoResetEvent(false);
        _responseSignal = new AutoResetEvent(false);
        _shutdown = false;
        _thread = new Thread(Worker) { IsBackground = true, Name = "Promptino.GlobalHotkey" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    private void Worker()
    {
        try
        {
            var current = new List<(int Id, GlobalHotkey Hotkey)>();

            while (true)
            {
                var sig = _requestSignal;
                if (sig == null || _shutdown) return;
                
                sig.WaitOne(10);

                if (_shutdown)
                {
                    lock (_sync)
                    {
                        foreach (var id in _registeredIds) UnregisterHotKey(IntPtr.Zero, id);
                        _registeredIds.Clear();
                    }
                    return;
                }

                if (_hasPending)
                {
                    lock (_sync)
                    {
                        current = _pendingHotkeys;
                        _hasPending = false;
                    }

                    lock (_sync)
                    {
                        foreach (var id in _registeredIds) UnregisterHotKey(IntPtr.Zero, id);
                        _registeredIds.Clear();
                    }

                    bool allOk = true;
                    foreach (var item in current)
                    {
                        var ok = RegisterHotKey(IntPtr.Zero, item.Id, (uint)item.Hotkey.Modifiers, (uint)item.Hotkey.VirtualKey);
                        if (ok) { lock (_sync) _registeredIds.Add(item.Id); }
                        else allOk = false;
                    }

                    lock (_sync)
                    {
                        _pendingResult = allOk
                            ? HotkeyRegistrationResult.Ok()
                            : Marshal.GetLastWin32Error() == 1409
                                ? HotkeyRegistrationResult.Conflict()
                                : HotkeyRegistrationResult.Failure("Some global hotkey registrations failed.");
                    }

                    _responseSignal?.Set();
                }

                while (PeekMessage(out var msg, IntPtr.Zero, WmHotkey, WmHotkey, 1))
                {
                    if (msg.message == WmHotkey)
                    {
                        var id = (int)msg.wParam;
                        bool known;
                        lock (_sync) known = _registeredIds.Contains(id);
                        if (known)
                        {
                            HotkeyPressed?.Invoke(id);
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException or NullReferenceException or InvalidOperationException)
        {
            // Swallow shutdown exceptions
        }
    }

    public void Dispose()
    {
        Stop();
        
        var req = _requestSignal;
        _requestSignal = null;
        req?.Dispose();

        var resp = _responseSignal;
        _responseSignal = null;
        resp?.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);
}
