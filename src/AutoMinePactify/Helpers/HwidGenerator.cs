using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace AutoMinePactify.Helpers;

/// <summary>
/// Genere un identifiant materiel unique (HWID) base sur le registre Windows.
/// Pas besoin de System.Management / WMI.
/// </summary>
[SupportedOSPlatform("windows")]
public static class HwidGenerator
{
    private static string? _cached;

    public static string GetHwid()
    {
        if (_cached != null) return _cached;

        try
        {
            string machineGuid = GetMachineGuid();
            string machineName = Environment.MachineName;
            int cpuCount = Environment.ProcessorCount;
            string userName = Environment.UserName;

            string raw = $"{machineGuid}|{machineName}|{cpuCount}|{userName}";
            _cached = HashSha256(raw);
        }
        catch
        {
            // Fallback si le registre est inaccessible
            string fallback = $"{Environment.MachineName}|{Environment.UserName}|{Environment.ProcessorCount}";
            _cached = HashSha256(fallback);
        }

        return _cached;
    }

    private static string GetMachineGuid()
    {
        try
        {
            object? val = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography",
                "MachineGuid", null);

            if (val is string guid && !string.IsNullOrWhiteSpace(guid))
                return guid;
        }
        catch { }

        return "fallback-no-guid";
    }

    private static string HashSha256(string input)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(64);
        foreach (byte b in hash)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
