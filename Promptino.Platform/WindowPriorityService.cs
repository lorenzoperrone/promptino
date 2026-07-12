using System;
using System.Runtime.InteropServices;

namespace Promptino.Platform;

public interface IWindowPriorityService
{
    bool TrySetAlwaysOnTop(Action<bool> applyAlwaysOnTop, bool enabled, out string warningMessage);
    bool TrySetScreenShareSafeMode(nint windowHandle, bool enabled, out string warningMessage);
}

public sealed class WindowPriorityService : IWindowPriorityService
{
    private const uint WdaNone = 0x0;
    private const uint WdaExcludeFromCapture = 0x11;

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

        var affinity = enabled ? WdaExcludeFromCapture : WdaNone;
        var success = SetWindowDisplayAffinity(windowHandle, affinity);
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
