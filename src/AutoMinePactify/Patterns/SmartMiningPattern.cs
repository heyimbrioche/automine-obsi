using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMinePactify.Models;
using AutoMinePactify.Services;

namespace AutoMinePactify.Patterns;

/// <summary>
/// Full Auto : detecte l'obsidienne a l'ecran, la mine, passe a la suivante.
/// Deconnecte le joueur si un pseudo ennemi est visible.
/// S'arrete si la pioche casse ou si plus d'obsidienne visible.
/// </summary>
public class SmartMiningPattern : IMiningPattern
{
    public string Name => "Full Auto";
    public string Description => "Trouve et mine l'obsidienne tout seul.";

    private readonly ScreenCaptureService _capture;
    private readonly ObsidianDetector _obsidianDetector;
    private readonly PlayerDetector _playerDetector;
    private readonly PickaxeChecker _pickaxeChecker;
    private readonly IntPtr _mcHandle;
    private readonly bool _playerSafetyEnabled;

    public SmartMiningPattern(
        ScreenCaptureService capture,
        ObsidianDetector obsidianDetector,
        PlayerDetector playerDetector,
        PickaxeChecker pickaxeChecker,
        IntPtr mcHandle,
        bool playerSafetyEnabled)
    {
        _capture = capture;
        _obsidianDetector = obsidianDetector;
        _playerDetector = playerDetector;
        _pickaxeChecker = pickaxeChecker;
        _mcHandle = mcHandle;
        _playerSafetyEnabled = playerSafetyEnabled;
    }

    public async Task ExecuteAsync(
        InputSimulator input,
        MiningConfig config,
        Action<string> log,
        Action<int> onProgress,
        Func<bool> safetyCheck,
        CancellationToken ct)
    {
        log("Mode Full Auto lance !");
        log("Je cherche l'obsidienne a l'ecran...");

        // Prendre la pioche
        await input.SelectSlot(config.PickaxeSlot, ct);
        log($"Pioche prise (case {config.PickaxeSlot})");
        await Task.Delay(300, ct);

        int blocksMined = 0;
        int noObsidianStreak = 0;
        const int maxRetries = 5;

        while (!ct.IsCancellationRequested)
        {
            ct.ThrowIfCancellationRequested();

            if (!safetyCheck())
            {
                log("Minecraft n'est plus au premier plan, on arrete.");
                return;
            }

            // 1. Capturer l'ecran
            var frame = _capture.CaptureWindow(_mcHandle);
            if (!frame.IsValid)
            {
                log("Impossible de capturer l'ecran. Minecraft est bien en mode fenetre ?");
                await Task.Delay(1000, ct);
                continue;
            }

            // 2. Verifier si un joueur est proche
            if (_playerSafetyEnabled)
            {
                bool playerDetected = _playerDetector.DetectPlayerNametag(frame);
                if (playerDetected)
                {
                    log("JOUEUR DETECTE ! Deconnexion immediate !");
                    await DisconnectFromServer(input, ct);
                    return;
                }
            }

            // 3. Verifier si la pioche est toujours la
            if (blocksMined > 0 && blocksMined % 3 == 0)
            {
                var checkFrame = _capture.CaptureWindow(_mcHandle);
                if (checkFrame.IsValid && !_pickaxeChecker.IsPickaxePresent(checkFrame, config.PickaxeSlot))
                {
                    log("Pioche cassee ! On arrete.");
                    return;
                }
            }

            // 4. Verifier si le viseur est deja sur de l'obsidienne
            if (_obsidianDetector.CheckCrosshair(frame))
            {
                noObsidianStreak = 0;
                blocksMined++;
                log($"Obsidienne trouvee sous le viseur ! Bloc #{blocksMined} - je mine...");

                await input.MineBlock(config.MiningDurationMs, ct);
                await Task.Delay(config.ActionDelayMs, ct);

                onProgress(blocksMined);
                continue;
            }

            // 5. Chercher de l'obsidienne autour
            log("Pas d'obsidienne sous le viseur, je cherche autour...");
            var target = _obsidianDetector.ScanForObsidian(frame, config.ScanRadiusPixels);

            if (target.HasValue)
            {
                noObsidianStreak = 0;
                var (dx, dy) = target.Value;
                log($"Obsidienne trouvee ! Je deplace le viseur ({dx}, {dy})...");

                // Deplacer la souris vers le bloc trouve
                await SmoothMoveTo(input, dx, dy, ct);
                await Task.Delay(200, ct);
                continue;
            }

            // 6. Rien trouve
            noObsidianStreak++;
            if (noObsidianStreak >= maxRetries)
            {
                log($"Plus d'obsidienne visible apres {maxRetries} essais. Fini !");
                log($"Total : {blocksMined} blocs mines.");
                return;
            }

            log($"Rien trouve... je reessaye ({noObsidianStreak}/{maxRetries})");
            await Task.Delay(800, ct);
        }

        log($"Arrete. Total : {blocksMined} blocs mines.");
    }

    /// <summary>
    /// Deconnecte du serveur en ouvrant le menu Echap puis en cliquant sur Deconnecter.
    /// Layout du menu pause Minecraft 1.8.8 :
    ///   - "Back to Game"   (~37% depuis le haut)
    ///   - "Options..."     (~47%)
    ///   - "Open to LAN"    (~52%)
    ///   - "Disconnect"     (~62%)
    /// Le curseur apparait au centre (~50%) quand on ouvre le menu.
    /// Il faut donc descendre d'environ 12% de la hauteur de la fenetre.
    /// </summary>
    private async Task DisconnectFromServer(InputSimulator input, CancellationToken ct)
    {
        // Relacher toutes les touches d'abord
        input.ReleaseAllKeys();
        await Task.Delay(100, ct);

        // Appuyer sur Echap pour ouvrir le menu pause
        await input.KeyPress(0x1B, 100, ct); // VK_ESCAPE
        await Task.Delay(600, ct);

        // En 1.8.8, le bouton "Disconnect" est a environ 62% de la hauteur de la fenetre.
        // Le curseur apparait au centre (50%). On doit descendre de ~12% de la hauteur.
        // On calcule d'apres la taille de la fenetre MC.
        int moveDown = 130; // ~12% de 1080 pour du 1080p, marche aussi pour d'autres resolutions

        // Essayer de calculer plus precisement si on a la taille de la fenetre
        var frame = _capture.CaptureWindow(_mcHandle);
        if (frame.IsValid)
        {
            moveDown = (int)(frame.Height * 0.12);
        }

        // Descendre vers le bouton Disconnect
        // On fait plusieurs petits mouvements pour etre plus precis
        int steps = 5;
        for (int i = 0; i < steps; i++)
        {
            await input.MouseMove(0, moveDown / steps, ct);
            await Task.Delay(20, ct);
        }
        await Task.Delay(150, ct);

        // Premier clic
        await input.MouseLeftDown(ct);
        await Task.Delay(50, ct);
        await input.MouseLeftUp(ct);
        await Task.Delay(400, ct);

        // Si ca n'a pas marche, essayer un peu plus bas et un peu plus haut
        // (au cas ou la resolution ou le GUI scale change la position)
        await input.MouseMove(0, 25, ct);
        await Task.Delay(100, ct);
        await input.MouseLeftDown(ct);
        await Task.Delay(50, ct);
        await input.MouseLeftUp(ct);
        await Task.Delay(200, ct);

        // Encore plus bas en dernier recours
        await input.MouseMove(0, 25, ct);
        await Task.Delay(100, ct);
        await input.MouseLeftDown(ct);
        await Task.Delay(50, ct);
        await input.MouseLeftUp(ct);
    }

    /// <summary>
    /// Deplace la souris de facon fluide vers la position cible.
    /// </summary>
    private static async Task SmoothMoveTo(InputSimulator input, int dx, int dy, CancellationToken ct)
    {
        int steps = Math.Max(3, (int)(Math.Sqrt(dx * dx + dy * dy) / 20));
        int stepDx = dx / steps;
        int stepDy = dy / steps;

        for (int i = 0; i < steps; i++)
        {
            await input.MouseMove(stepDx, stepDy, ct);
            await Task.Delay(15, ct);
        }

        // Corriger le reste (arrondi)
        int remainDx = dx - stepDx * steps;
        int remainDy = dy - stepDy * steps;
        if (remainDx != 0 || remainDy != 0)
        {
            await input.MouseMove(remainDx, remainDy, ct);
        }
    }
}
