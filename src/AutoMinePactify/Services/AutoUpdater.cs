using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace AutoMinePactify.Services;

/// <summary>
/// Telecharge la nouvelle version et remplace l'exe automatiquement.
/// Comme un exe ne peut pas se remplacer lui-meme sur Windows,
/// on telecharge le nouveau, on lance un script batch qui attend
/// que le process se ferme, remplace l'ancien exe, et relance le nouveau.
/// </summary>
public static class AutoUpdater
{
    public static event Action<double>? OnProgressChanged;
    public static event Action<string>? OnStatusChanged;

    /// <summary>
    /// Telecharge le nouvel exe et lance le remplacement automatique.
    /// </summary>
    public static async Task<bool> DownloadAndUpdate(string downloadUrl)
    {
        try
        {
            string currentExe = Process.GetCurrentProcess().MainModule?.FileName
                ?? Environment.ProcessPath
                ?? "";

            if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe))
            {
                OnStatusChanged?.Invoke("Impossible de trouver l'exe actuel.");
                return false;
            }

            string exeDir = Path.GetDirectoryName(currentExe)!;
            string exeName = Path.GetFileName(currentExe);
            string tempExe = Path.Combine(exeDir, exeName + ".update");
            string batchFile = Path.Combine(exeDir, "_update.bat");

            // Etape 1 : Telecharger le nouvel exe
            OnStatusChanged?.Invoke("Telechargement en cours...");

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.Add("User-Agent", "AutoMinePactify-Updater");

            using var response = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            long downloadedBytes = 0;

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(tempExe, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                downloadedBytes += bytesRead;

                if (totalBytes > 0)
                {
                    double progress = (double)downloadedBytes / totalBytes * 100;
                    OnProgressChanged?.Invoke(progress);

                    double mb = downloadedBytes / 1024.0 / 1024.0;
                    double totalMb = totalBytes / 1024.0 / 1024.0;
                    OnStatusChanged?.Invoke($"Telechargement : {mb:F1} / {totalMb:F1} Mo");
                }
            }

            fileStream.Close();

            OnProgressChanged?.Invoke(100);
            OnStatusChanged?.Invoke("Installation de la mise a jour...");

            // Etape 2 : Creer le script batch de remplacement
            // Le script attend que le process se termine, remplace l'exe, et relance
            int currentPid = Environment.ProcessId;

            string batchContent = $@"@echo off
chcp 65001 >nul
echo Mise a jour en cours...
echo Attente de la fermeture du programme...

:waitloop
tasklist /FI ""PID eq {currentPid}"" 2>NUL | find ""{currentPid}"" >NUL
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto waitloop
)

echo Remplacement de l'exe...
del ""{currentExe}"" >nul 2>&1
move ""{tempExe}"" ""{currentExe}"" >nul 2>&1

if exist ""{currentExe}"" (
    echo Mise a jour terminee ! Relancement...
    start """" ""{currentExe}""
) else (
    echo ERREUR : la mise a jour a echoue.
    pause
)

del ""%~f0"" >nul 2>&1
";

            await File.WriteAllTextAsync(batchFile, batchContent, System.Text.Encoding.Default);

            // Etape 3 : Lancer le script batch et quitter
            OnStatusChanged?.Invoke("Redemarrage...");

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batchFile}\"",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            Process.Start(startInfo);

            return true;
        }
        catch (Exception ex)
        {
            OnStatusChanged?.Invoke($"Erreur : {ex.Message}");
            return false;
        }
    }
}
