using System;
using System.Collections.Generic;
using System.Text;
using AutoMinePactify.Helpers;

namespace AutoMinePactify.Services;

/// <summary>
/// Detects and manages the Minecraft Java Edition window.
/// </summary>
public class MinecraftWindowService
{
    private IntPtr _minecraftHandle = IntPtr.Zero;

    public IntPtr MinecraftHandle => _minecraftHandle;

    public bool IsMinecraftDetected =>
        _minecraftHandle != IntPtr.Zero && NativeMethods.IsWindow(_minecraftHandle);

    /// <summary>
    /// Scans all visible windows to find a Minecraft Java Edition game window.
    /// Returns true if found.
    /// </summary>
    public bool DetectMinecraft()
    {
        _minecraftHandle = IntPtr.Zero;
        var candidates = new List<(IntPtr handle, string title)>();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd)) return true;

            int length = NativeMethods.GetWindowTextLength(hWnd);
            if (length == 0) return true;

            var sb = new StringBuilder(length + 1);
            NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();

            // Minecraft Java Edition title: "Minecraft* X.XX.X" (in-game)
            // Exclude the Launcher
            if (title.Contains("Minecraft", StringComparison.OrdinalIgnoreCase) &&
                !title.Contains("Launcher", StringComparison.OrdinalIgnoreCase) &&
                !title.Contains("Updater", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add((hWnd, title));
            }

            return true;
        }, IntPtr.Zero);

        if (candidates.Count > 0)
        {
            _minecraftHandle = candidates[0].handle;
            return true;
        }

        return false;
    }

    /// <summary>Brings the Minecraft window to the foreground.</summary>
    public bool BringToForeground()
    {
        if (!IsMinecraftDetected) return false;
        return NativeMethods.SetForegroundWindow(_minecraftHandle);
    }

    /// <summary>Checks if Minecraft is currently the focused window.</summary>
    public bool IsMinecraftFocused()
    {
        if (!IsMinecraftDetected) return false;
        return NativeMethods.GetForegroundWindow() == _minecraftHandle;
    }

    /// <summary>Returns the title of the detected Minecraft window.</summary>
    public string GetWindowTitle()
    {
        if (!IsMinecraftDetected) return "Non détecté";
        var sb = new StringBuilder(256);
        NativeMethods.GetWindowText(_minecraftHandle, sb, sb.Capacity);
        return sb.ToString();
    }
}
