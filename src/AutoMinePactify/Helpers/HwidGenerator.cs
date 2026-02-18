using System;
using System.Management;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace AutoMinePactify.Helpers;

[SupportedOSPlatform("windows")]
public static class HwidGenerator
{
    private static string? _cached;

    public static string GetHwid()
    {
        if (_cached != null) return _cached;

        try
        {
            string raw = string.Join("|",
                GetWmiValue("Win32_Processor", "ProcessorId"),
                GetWmiValue("Win32_BaseBoard", "SerialNumber"),
                GetWmiValue("Win32_DiskDrive", "SerialNumber"));

            if (string.IsNullOrWhiteSpace(raw) || raw == "||")
                raw = FallbackId();

            _cached = HashSha256(raw);
        }
        catch
        {
            _cached = HashSha256(FallbackId());
        }

        return _cached;
    }

    private static string GetWmiValue(string wmiClass, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
            foreach (var obj in searcher.Get())
            {
                string? val = obj[property]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(val) && val != "To Be Filled By O.E.M.")
                    return val;
            }
        }
        catch { }
        return "";
    }

    private static string FallbackId()
    {
        return $"{Environment.MachineName}|{Environment.UserName}|{Environment.ProcessorCount}";
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
