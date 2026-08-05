using System;
using System.Runtime.InteropServices;

namespace Promptino.Platform;

/// <summary>
/// Service interface to toggle mouse click-through behavior for native OS window handles.
/// </summary>
public interface IWindowClickThroughService
{
    /// <summary>
    /// Enables or disables click-through on the specified window.
    /// </summary>
    /// <param name="windowHandle">Native OS window handle (HWND).</param>
    /// <param name="enableClickThrough"><c>true</c> to pass mouse inputs through to windows below; <c>false</c> to capture mouse inputs.</param>
    /// <returns><c>true</c> if the window style was updated successfully.</returns>
    bool SetClickThrough(IntPtr windowHandle, bool enableClickThrough);
}

/// <summary>
/// Windows platform implementation of <see cref="IWindowClickThroughService"/> modifying WS_EX_TRANSPARENT window styles.
/// </summary>
public sealed class WindowClickThroughService : IWindowClickThroughService
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    /// <inheritdoc />
    public bool SetClickThrough(IntPtr windowHandle, bool enableClickThrough)
    {
        if (!OperatingSystem.IsWindows() || windowHandle == IntPtr.Zero)
            return false;

        try
        {
            var exStyle = GetWindowLongPtr(windowHandle, GWL_EXSTYLE).ToInt64();
            if (enableClickThrough)
            {
                exStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
            }
            else
            {
                exStyle &= ~WS_EX_TRANSPARENT;
            }
            SetWindowLongPtr(windowHandle, GWL_EXSTYLE, new IntPtr(exStyle));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
            return GetWindowLongPtr64(hWnd, nIndex);
        return GetWindowLong32(hWnd, nIndex);
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8)
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        return SetWindowLong32(hWnd, nIndex, dwNewLong);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}

/// <summary>
/// Fallback no-op implementation of <see cref="IWindowClickThroughService"/> for non-supported platforms.
/// </summary>
public sealed class NoOpWindowClickThroughService : IWindowClickThroughService
{
    /// <inheritdoc />
    public bool SetClickThrough(IntPtr windowHandle, bool enableClickThrough) => false;
}
