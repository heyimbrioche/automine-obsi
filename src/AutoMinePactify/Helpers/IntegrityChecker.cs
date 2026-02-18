using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace AutoMinePactify.Helpers;

/// <summary>
/// Protections anti-crack : anti-debug, anti-tamper, anti-dump.
/// Appelees au demarrage dans Program.cs.
/// </summary>
[SupportedOSPlatform("windows")]
public static class IntegrityChecker
{
    [DllImport("kernel32.dll")]
    private static extern bool IsDebuggerPresent();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, out bool isDebuggerPresent);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSetInformationThread(IntPtr threadHandle, int threadInformationClass, IntPtr threadInformation, int threadInformationLength);

    private const int ThreadHideFromDebugger = 0x11;

    /// <summary>
    /// Verifie qu'aucun debugger n'est attache au processus.
    /// Retourne true si c'est safe, false si un debugger est detecte.
    /// </summary>
    public static bool IsClean()
    {
        try
        {
            // Check 1 : .NET Debugger.IsAttached
            if (Debugger.IsAttached)
                return false;

            // Check 2 : Win32 IsDebuggerPresent
            if (IsDebuggerPresent())
                return false;

            // Check 3 : Remote debugger (ex: x64dbg attache a distance)
            if (CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, out bool remoteDebugger) && remoteDebugger)
                return false;

            return true;
        }
        catch
        {
            // En cas d'erreur, on laisse passer (pas bloquer les users legit)
            return true;
        }
    }

    /// <summary>
    /// Masque le thread principal des debuggers (ThreadHideFromDebugger).
    /// Un debugger attache apres ce call crashera au lieu de fonctionner.
    /// </summary>
    public static void HideFromDebugger()
    {
        try
        {
            NtSetInformationThread(
                (IntPtr)(-2), // pseudo-handle du thread courant
                ThreadHideFromDebugger,
                IntPtr.Zero,
                0);
        }
        catch
        {
            // Silencieux si ca marche pas
        }
    }

    /// <summary>
    /// Calcule le hash SHA256 du .exe courant pour pouvoir verifier l'integrite.
    /// </summary>
    public static string GetExeHash()
    {
        try
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return "";

            using var stream = File.OpenRead(exePath);
            byte[] hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Verification periodique en background (appeler dans un Timer).
    /// Si un debugger est detecte en cours d'execution, ferme le programme.
    /// </summary>
    public static void StartPeriodicCheck()
    {
        var timer = new System.Threading.Timer(_ =>
        {
            if (!IsClean())
            {
                Environment.Exit(0);
            }
        }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2));

        // Garder une reference pour eviter le GC
        GC.KeepAlive(timer);
    }
}
