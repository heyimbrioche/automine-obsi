using System;
using System.Diagnostics;
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

    [DllImport("kernel32.dll")]
    private static extern bool IsDebuggerPresent();

    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONERROR = 0x00000010;

    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            if (Debugger.IsAttached || IsDebuggerPresent())
            {
                Environment.Exit(0);
            }

            if (!AdminHelper.IsRunAsAdmin())
            {
                MessageBox(IntPtr.Zero,
                    "Ce programme a besoin des droits Administrateur pour fonctionner !\n\nFais clic droit sur le .exe puis \"Exécuter en tant qu'administrateur\".",
                    "AutoMine Obsidienne - Il faut lancer en admin",
                    MB_OK | MB_ICONERROR);
                Environment.Exit(1);
            }

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
