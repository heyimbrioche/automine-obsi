using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;
using AutoMinePactify.Helpers;

namespace AutoMinePactify;

[SupportedOSPlatform("windows")]
class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONERROR = 0x00000010;

    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // ── Protection anti-debug ──
            if (!IntegrityChecker.IsClean())
            {
                // Un debugger est detecte, on ferme silencieusement
                Environment.Exit(0);
            }

            // Masquer le thread principal des debuggers
            IntegrityChecker.HideFromDebugger();

            // Lancer la verification periodique anti-debug en background
            IntegrityChecker.StartPeriodicCheck();

            // ── Lancement normal ──
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            MessageBox(IntPtr.Zero,
                $"Le programme a plante au demarrage !\n\nErreur : {ex.Message}\n\nDetails : {ex.StackTrace}",
                "AutoMine Obsidienne - Erreur",
                MB_OK | MB_ICONERROR);
            Environment.Exit(1);
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
