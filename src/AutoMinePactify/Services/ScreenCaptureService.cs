using System;
using AutoMinePactify.Helpers;
using AutoMinePactify.Models;

namespace AutoMinePactify.Services;

/// <summary>
/// Capture le contenu de la fenetre Minecraft via l'API Windows BitBlt.
/// Minecraft doit etre en mode fenetre (pas plein ecran exclusif).
/// </summary>
public class ScreenCaptureService
{
    /// <summary>
    /// Capture les pixels de la zone client de la fenetre donnee.
    /// Retourne un ScreenFrame avec les pixels BGRA.
    /// </summary>
    public ScreenFrame CaptureWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return default;

        // Taille de la zone client (sans les bordures)
        NativeMethods.GetClientRect(hwnd, out var clientRect);
        int width = clientRect.Right - clientRect.Left;
        int height = clientRect.Bottom - clientRect.Top;

        if (width <= 0 || height <= 0)
            return default;

        IntPtr hdcWindow = IntPtr.Zero;
        IntPtr hdcMem = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr hOld = IntPtr.Zero;

        try
        {
            hdcWindow = NativeMethods.GetDC(hwnd);
            if (hdcWindow == IntPtr.Zero) return default;

            hdcMem = NativeMethods.CreateCompatibleDC(hdcWindow);
            if (hdcMem == IntPtr.Zero) return default;

            hBitmap = NativeMethods.CreateCompatibleBitmap(hdcWindow, width, height);
            if (hBitmap == IntPtr.Zero) return default;

            hOld = NativeMethods.SelectObject(hdcMem, hBitmap);

            // Copier les pixels de la fenetre
            NativeMethods.BitBlt(hdcMem, 0, 0, width, height, hdcWindow, 0, 0, NativeMethods.SRCCOPY);

            // Lire les pixels en BGRA
            var bmi = new NativeMethods.BITMAPINFO
            {
                bmiHeader = new NativeMethods.BITMAPINFOHEADER
                {
                    biSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = height, // positif = bottom-up
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0 // BI_RGB
                }
            };

            byte[] pixels = new byte[width * height * 4];
            NativeMethods.GetDIBits(hdcMem, hBitmap, 0, (uint)height, pixels, ref bmi, NativeMethods.DIB_RGB_COLORS);

            return new ScreenFrame(pixels, width, height);
        }
        finally
        {
            if (hOld != IntPtr.Zero && hdcMem != IntPtr.Zero)
                NativeMethods.SelectObject(hdcMem, hOld);
            if (hBitmap != IntPtr.Zero)
                NativeMethods.DeleteObject(hBitmap);
            if (hdcMem != IntPtr.Zero)
                NativeMethods.DeleteDC(hdcMem);
            if (hdcWindow != IntPtr.Zero)
                NativeMethods.ReleaseDC(hwnd, hdcWindow);
        }
    }
}
