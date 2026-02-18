using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoMinePactify.Helpers;

namespace AutoMinePactify.Services;

public enum LicenseStatus
{
    Valid,
    InvalidKey,
    HwidMismatch,
    Expired,
    NetworkError,
    Unknown
}

public class LicenseResult
{
    public LicenseStatus Status { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>
/// Handles license validation via KeyAuth and local encrypted caching.
///
/// BEFORE BUILDING: replace the 4 constants below with your real KeyAuth app credentials.
/// You get them from https://keyauth.cc/app/ after creating an application.
/// </summary>
[SupportedOSPlatform("windows")]
public class LicenseService : IDisposable
{
    // ──────────────────────────────────────────────────────────────────
    //  KeyAuth credentials – FILL THESE IN from your KeyAuth dashboard
    // ──────────────────────────────────────────────────────────────────
    private const string KA_APP_NAME = "Dialogue's Application";
    private const string KA_OWNER_ID = "Pk1DZEyxn4";
    private const string KA_APP_SECRET = "cd37f58054ec0fc7eb39585c34f116d1b5da99028984b3cdfb012a82eeaca128";
    private const string KA_VERSION = "1.0";
    // ──────────────────────────────────────────────────────────────────

    private const string ApiBase = "https://keyauth.win/api/1.2/";

    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutoMinePactify");

    private static readonly string CachePath = Path.Combine(CacheDir, "license.dat");

    private string? _sessionId;
    private readonly HttpClient _http;
    private Timer? _revalidationTimer;
    private string? _activeKey;

    public LicenseService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>
    /// Try to validate a previously cached license key without user interaction.
    /// </summary>
    public async Task<LicenseResult> ValidateCachedAsync()
    {
        string? cached = LoadCachedKey();
        if (string.IsNullOrEmpty(cached))
            return new LicenseResult { Status = LicenseStatus.InvalidKey, Message = "Aucune licence en cache." };

        return await ValidateAsync(cached);
    }

    /// <summary>
    /// Validate a license key against KeyAuth (init session + license check).
    /// </summary>
    public async Task<LicenseResult> ValidateAsync(string key)
    {
        try
        {
            var initResult = await InitSession();
            if (!initResult.success)
                return new LicenseResult { Status = LicenseStatus.NetworkError, Message = initResult.message };

            string hwid = HwidGenerator.GetHwid();

            var form = new Dictionary<string, string>
            {
                ["type"] = "license",
                ["key"] = key,
                ["hwid"] = hwid,
                ["sessionid"] = _sessionId!,
                ["name"] = KA_APP_NAME,
                ["ownerid"] = KA_OWNER_ID
            };

            using var resp = await _http.PostAsync(ApiBase, new FormUrlEncodedContent(form));
            string body = await resp.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            bool success = root.GetProperty("success").GetBoolean();
            string message = root.TryGetProperty("message", out var msgProp)
                ? msgProp.GetString() ?? ""
                : "";

            if (success)
            {
                _activeKey = key;
                SaveCachedKey(key);
                return new LicenseResult { Status = LicenseStatus.Valid, Message = "Licence valide !" };
            }

            if (message.Contains("HWID", StringComparison.OrdinalIgnoreCase))
                return new LicenseResult
                {
                    Status = LicenseStatus.HwidMismatch,
                    Message = "Cette cle est deja utilisee sur un autre PC.\nContacte le vendeur pour un reset HWID."
                };

            if (message.Contains("expired", StringComparison.OrdinalIgnoreCase))
                return new LicenseResult { Status = LicenseStatus.Expired, Message = "Licence expiree." };

            return new LicenseResult { Status = LicenseStatus.InvalidKey, Message = "Cle de licence invalide." };
        }
        catch (HttpRequestException)
        {
            return new LicenseResult
            {
                Status = LicenseStatus.NetworkError,
                Message = "Impossible de contacter le serveur.\nVerifie ta connexion internet."
            };
        }
        catch (TaskCanceledException)
        {
            return new LicenseResult
            {
                Status = LicenseStatus.NetworkError,
                Message = "Le serveur ne repond pas (timeout).\nReessaie dans quelques instants."
            };
        }
        catch (Exception ex)
        {
            return new LicenseResult
            {
                Status = LicenseStatus.Unknown,
                Message = $"Erreur inattendue : {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Start periodic background revalidation (every 2 hours).
    /// </summary>
    public void StartPeriodicCheck()
    {
        if (_activeKey == null) return;

        _revalidationTimer = new Timer(async _ =>
        {
            if (_activeKey == null) return;
            var result = await ValidateAsync(_activeKey);
            if (result.Status != LicenseStatus.Valid)
            {
                Environment.Exit(0);
            }
        }, null, TimeSpan.FromHours(2), TimeSpan.FromHours(2));
    }

    public void ClearCache()
    {
        try { if (File.Exists(CachePath)) File.Delete(CachePath); } catch { }
    }

    // ─── KeyAuth session init ────────────────────────────────────────

    private async Task<(bool success, string message)> InitSession()
    {
        var form = new Dictionary<string, string>
        {
            ["type"] = "init",
            ["ver"] = KA_VERSION,
            ["name"] = KA_APP_NAME,
            ["ownerid"] = KA_OWNER_ID
        };

        using var resp = await _http.PostAsync(ApiBase, new FormUrlEncodedContent(form));
        string body = await resp.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        bool success = root.GetProperty("success").GetBoolean();
        string message = root.TryGetProperty("message", out var msgProp)
            ? msgProp.GetString() ?? ""
            : "";

        if (success)
        {
            _sessionId = root.GetProperty("sessionid").GetString();
            return (true, message);
        }

        return (false, $"Erreur KeyAuth : {message}");
    }

    // ─── Encrypted local cache ───────────────────────────────────────

    private void SaveCachedKey(string key)
    {
        try
        {
            if (!Directory.Exists(CacheDir))
                Directory.CreateDirectory(CacheDir);

            byte[] encrypted = EncryptAes(key, HwidGenerator.GetHwid());
            File.WriteAllBytes(CachePath, encrypted);
        }
        catch { }
    }

    private string? LoadCachedKey()
    {
        try
        {
            if (!File.Exists(CachePath)) return null;
            byte[] encrypted = File.ReadAllBytes(CachePath);
            return DecryptAes(encrypted, HwidGenerator.GetHwid());
        }
        catch
        {
            return null;
        }
    }

    private static byte[] EncryptAes(string plainText, string password)
    {
        byte[] key = DeriveKey(password);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs, Encoding.UTF8))
        {
            sw.Write(plainText);
        }
        return ms.ToArray();
    }

    private static string DecryptAes(byte[] cipherData, string password)
    {
        byte[] key = DeriveKey(password);
        using var aes = Aes.Create();
        aes.Key = key;

        byte[] iv = new byte[16];
        Array.Copy(cipherData, 0, iv, 0, 16);
        aes.IV = iv;

        using var ms = new MemoryStream(cipherData, 16, cipherData.Length - 16);
        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var sr = new StreamReader(cs, Encoding.UTF8);
        return sr.ReadToEnd();
    }

    private static byte[] DeriveKey(string password)
    {
        byte[] salt = Encoding.UTF8.GetBytes("AutoMinePactify_Salt_2025");
        using var kdf = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        return kdf.GetBytes(32);
    }

    public void Dispose()
    {
        _revalidationTimer?.Dispose();
        _http.Dispose();
        GC.SuppressFinalize(this);
    }
}
