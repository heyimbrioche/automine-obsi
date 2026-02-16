using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AutoMinePactify.Services;

/// <summary>
/// Verifie si une nouvelle version est disponible via l'API GitHub Releases.
/// Fonctionne automatiquement : tu crées une release sur GitHub, l'app la detecte.
///
/// Pour mettre a jour :
/// 1. Change CurrentVersion ici (ex: "1.1.0")
/// 2. Build et publie l'exe
/// 3. Cree une release sur GitHub avec le tag "v1.1.0" et attache l'exe
/// 4. Les utilisateurs verront le message au prochain lancement
/// </summary>
public static class UpdateChecker
{
    /// <summary>
    /// Version actuelle du programme. A incrementer a chaque release.
    /// </summary>
    public const string CurrentVersion = "1.0.0";

    /// <summary>
    /// Ton repo GitHub : "pseudo/nom-du-repo"
    /// Exemple : "dialogue__/automine-obsidienne"
    /// Mets ton vrai repo ici. Laisse vide pour desactiver.
    /// </summary>
    private const string GitHubRepo = "heyimbrioche/automine-obsi";

    public class UpdateInfo
    {
        public string Version { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string Changelog { get; set; } = "";
    }

    public class UpdateResult
    {
        public bool UpdateAvailable { get; set; }
        public UpdateInfo? Info { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Verifie si une mise a jour est disponible via GitHub Releases.
    /// </summary>
    public static async Task<UpdateResult> CheckForUpdate()
    {
        if (string.IsNullOrWhiteSpace(GitHubRepo))
        {
            return new UpdateResult { UpdateAvailable = false };
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            // GitHub API demande un User-Agent
            http.DefaultRequestHeaders.Add("User-Agent", "AutoMinePactify");

            string url = $"https://api.github.com/repos/{GitHubRepo}/releases/latest";
            string json = await http.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // tag_name = "v1.2.0" ou "1.2.0"
            string tagName = root.GetProperty("tag_name").GetString() ?? "";
            string version = tagName.TrimStart('v', 'V');

            // html_url = lien vers la page de la release
            string htmlUrl = root.GetProperty("html_url").GetString() ?? "";

            // body = description de la release (changelog)
            string body = "";
            if (root.TryGetProperty("body", out var bodyProp) && bodyProp.ValueKind == JsonValueKind.String)
            {
                body = bodyProp.GetString() ?? "";
                // Garder juste la premiere ligne pour l'affichage
                int newline = body.IndexOf('\n');
                if (newline > 0) body = body.Substring(0, newline).Trim();
            }

            // Chercher l'exe dans les assets
            string downloadUrl = htmlUrl; // par defaut : lien vers la page
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? htmlUrl;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                return new UpdateResult { UpdateAvailable = false };
            }

            bool isNewer = IsNewerVersion(version, CurrentVersion);

            return new UpdateResult
            {
                UpdateAvailable = isNewer,
                Info = new UpdateInfo
                {
                    Version = version,
                    DownloadUrl = downloadUrl,
                    Changelog = body
                }
            };
        }
        catch (Exception ex)
        {
            return new UpdateResult
            {
                UpdateAvailable = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Compare deux versions "X.Y.Z". Retourne true si remote > local.
    /// </summary>
    private static bool IsNewerVersion(string remote, string local)
    {
        try
        {
            var remoteV = new Version(remote);
            var localV = new Version(local);
            return remoteV > localV;
        }
        catch
        {
            return remote != local;
        }
    }
}
