using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Promptino.Platform;

/// <summary>
/// Bitwise flags representing modifier keys (Alt, Control, Shift, Win) for global hotkey registration.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    /// <summary>No modifier key.</summary>
    None = 0,
    /// <summary>Alt key.</summary>
    Alt = 1,
    /// <summary>Control key.</summary>
    Control = 2,
    /// <summary>Shift key.</summary>
    Shift = 4,
    /// <summary>Windows logo key.</summary>
    Win = 8,
}

/// <summary>
/// Represents a global hotkey shortcut combination of modifier flags and virtual key code.
/// </summary>
/// <param name="Modifiers">Modifier keys combination.</param>
/// <param name="VirtualKey">Windows virtual key code (e.g. 0x20 for Space).</param>
public readonly record struct GlobalHotkey(HotkeyModifiers Modifiers, int VirtualKey)
{
    /// <summary>Gets default hotkey (Control + Alt + Space).</summary>
    public static GlobalHotkey Default => new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x20); // Space

    /// <summary>Gets a value indicating whether the hotkey combination is valid.</summary>
    public bool IsValid => Modifiers != HotkeyModifiers.None && VirtualKey is >= 0x08 and <= 0xFE;
}

/// <summary>
/// Result snapshot returned when registering or updating global hotkeys.
/// </summary>
/// <param name="Success"><c>true</c> if all hotkeys were registered successfully.</param>
/// <param name="IsConflict"><c>true</c> if registration failed due to shortcut conflict with another running app.</param>
/// <param name="Warning">User-facing error or warning message.</param>
public readonly record struct HotkeyRegistrationResult(bool Success, bool IsConflict, string? Warning)
{
    /// <summary>Creates a successful registration result.</summary>
    public static HotkeyRegistrationResult Ok() => new(true, false, null);

    /// <summary>Creates a conflict error result when a hotkey is already in use.</summary>
    public static HotkeyRegistrationResult Conflict() => new(false, true, "Global hotkey unavailable: shortcut already in use by another app. Choose a different shortcut.");

    /// <summary>Creates a failure result with the given warning message.</summary>
    public static HotkeyRegistrationResult Failure(string warning) => new(false, false, warning);
}

/// <summary>
/// Provides system-wide global hotkey registration and event dispatching.
/// </summary>
public interface IGlobalHotkeyService : IDisposable
{
    /// <summary>Occurs when a registered global hotkey is pressed.</summary>
    event Action<int>? HotkeyPressed;

    /// <summary>Updates and registers active global hotkeys with OS bindings.</summary>
    /// <param name="hotkeys">Collection of hotkey bindings keyed by integer ID.</param>
    /// <returns>A <see cref="HotkeyRegistrationResult"/> indicating status.</returns>
    HotkeyRegistrationResult UpdateHotkeys(IEnumerable<(int Id, GlobalHotkey Hotkey)> hotkeys);

    /// <summary>Stops global hotkey listening worker thread and unregisters hotkeys.</summary>
    void Stop();
}

/// <summary>
/// Fallback no-op hotkey service implementation for non-Windows platforms.
/// </summary>
public sealed class NoOpGlobalHotkeyService : IGlobalHotkeyService
{
    /// <inheritdoc />
    public event Action<int>? HotkeyPressed
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public HotkeyRegistrationResult UpdateHotkeys(IEnumerable<(int Id, GlobalHotkey Hotkey)> hotkeys)
        => HotkeyRegistrationResult.Failure("Global hotkeys are available only on Windows.");

    /// <inheritdoc />
    public void Stop() { }

    /// <inheritdoc />
    public void Dispose() { }
}

/// <summary>
/// Windows implementation of <see cref="IGlobalHotkeyService"/> using Win32 <c>RegisterHotKey</c> and STA message loop.
/// </summary>
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

    /// <inheritdoc />
    public event Action<int>? HotkeyPressed;

    /// <inheritdoc />
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

    /// <inheritdoc />
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

                IntPtr sigHandle = sig.SafeWaitHandle.DangerousGetHandle();
                if (sigHandle != IntPtr.Zero)
                {
                    MsgWaitForMultipleObjectsEx(1, new[] { sigHandle }, 200, QS_HOTKEY | QS_POSTMESSAGE, MWMO_INPUTAVAILABLE);
                }
                else
                {
                    Thread.Sleep(50);
                }

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

                while (PeekMessage(out var msg, IntPtr.Zero, 0, 0, 1))
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
                    else
                    {
                        TranslateMessage(ref msg);
                        DispatchMessage(ref msg);
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

    private const uint QS_HOTKEY = 0x0080;
    private const uint QS_POSTMESSAGE = 0x0098;
    private const uint MWMO_INPUTAVAILABLE = 0x0004;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint MsgWaitForMultipleObjectsEx(uint nCount, IntPtr[] pHandles, uint dwMilliseconds, uint dwWakeMask, uint dwFlags);
}
