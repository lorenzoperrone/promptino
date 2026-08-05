using System;
using System.Runtime.InteropServices;

namespace Promptino.Platform;

/// <summary>
/// Service interface for window priority policies (Always-On-Top and Screen-Share Safe Mode).
/// </summary>
public interface IWindowPriorityService
{
    /// <summary>
    /// Attempts to apply the always-on-top window placement policy.
    /// </summary>
    /// <param name="applyAlwaysOnTop">Callback that applies the top-most flag to the target window.</param>
    /// <param name="enabled"><c>true</c> to enable top-most placement; <c>false</c> to restore standard placement.</param>
    /// <param name="warningMessage">Out parameter populated with user warnings on failure.</param>
    /// <returns><c>true</c> if successfully applied.</returns>
    bool TrySetAlwaysOnTop(Action<bool> applyAlwaysOnTop, bool enabled, out string warningMessage);

    /// <summary>
    /// Attempts to apply Win32 display affinity protection to hide the prompter window from screen-capture tools.
    /// </summary>
    /// <param name="windowHandle">Native OS window handle (HWND).</param>
    /// <param name="enabled"><c>true</c> to hide window from captures; <c>false</c> to restore normal visibility.</param>
    /// <param name="warningMessage">Out parameter populated with user warnings on failure.</param>
    /// <returns><c>true</c> if display affinity was set successfully.</returns>
    bool TrySetScreenShareSafeMode(nint windowHandle, bool enabled, out string warningMessage);
}

/// <summary>
/// Windows platform implementation of <see cref="IWindowPriorityService"/> using <c>SetWindowDisplayAffinity</c>.
/// </summary>
public sealed class WindowPriorityService : IWindowPriorityService
{
    private const uint WdaNone = 0x0;
    private const uint WdaMonitor = 0x1;
    private const uint WdaExcludeFromCapture = 0x11;

    /// <inheritdoc />
    public bool TrySetAlwaysOnTop(Action<bool> applyAlwaysOnTop, bool enabled, out string warningMessage)
    {
        try
        {
            applyAlwaysOnTop(enabled);
            warningMessage = string.Empty;
            return true;
        }
        catch
        {
            warningMessage = "Could not apply always-on-top in this session. Reading still works normally.";
            return false;
        }
    }

    /// <inheritdoc />
    public bool TrySetScreenShareSafeMode(nint windowHandle, bool enabled, out string warningMessage)
    {
        if (!OperatingSystem.IsWindows())
        {
            warningMessage = "Screen-share safe mode is available only on Windows.";
            return false;
        }

        if (windowHandle == 0 || !IsWindow(windowHandle))
        {
            warningMessage = "Could not apply screen-share safe mode because the prompter window handle is unavailable.";
            return false;
        }

        if (!enabled)
        {
            SetWindowDisplayAffinity(windowHandle, WdaNone);
            warningMessage = string.Empty;
            return true;
        }

        var success = SetWindowDisplayAffinity(windowHandle, WdaExcludeFromCapture);
        if (!success)
        {
            success = SetWindowDisplayAffinity(windowHandle, WdaMonitor);
        }

        if (success)
        {
            warningMessage = string.Empty;
            return true;
        }

        warningMessage = "Screen-share safe mode could not be applied in this session. Verify privacy in your capture app.";
        return false;
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(nint hWnd, uint dwAffinity);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint hWnd);
}
