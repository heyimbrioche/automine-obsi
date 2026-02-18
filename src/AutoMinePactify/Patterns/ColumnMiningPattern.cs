using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMinePactify.Models;
using AutoMinePactify.Services;

namespace AutoMinePactify.Patterns;

/// <summary>
/// Mine une colonne d'obsidienne position par position vers le bas,
/// en utilisant /home pour revenir au point de depart et /sethome
/// pour decaler le home sur la largeur.
///
/// Le joueur doit :
/// 1. Se placer au coin de la colonne, camera 90/90 (droit vers le bas)
/// 2. Avoir fait /sethome [nom] a cette position
/// 3. Lancer le minage
///
/// Algorithme :
/// Pour chaque bande de largeur (w = 0 a W-1) :
///   - Miner les positions l = L-1 (loin) jusqu'a l = 1 (proche du home)
///   - A chaque position : /home, reculer de l blocs (S), miner D blocs vers le bas
///   - Quand la bande est finie : /home, strafe gauche 1, /sethome, strafe droite 1,
///     miner D blocs a l'ancien home (position l=0)
///   - Repeter pour la bande suivante
/// Derniere bande : miner aussi la position home directement a la fin
/// </summary>
public class ColumnMiningPattern : IMiningPattern
{
    public string Name => "Colonne";
    public string Description => "Mine une colonne vers le bas avec /home et /sethome.";

    public async Task ExecuteAsync(
        InputSimulator input,
        MiningConfig config,
        Action<string> log,
        Action<int> onProgress,
        Func<bool> safetyCheck,
        PauseToken pauseToken,
        CancellationToken ct)
    {
        int width = Math.Max(1, config.ColumnWidth);
        int length = Math.Max(1, config.ColumnLength);
        int depth = Math.Max(1, config.ColumnLayers);
        var moveMode = config.ColumnMovement;

        int totalBlocks = width * length * depth;
        string homeCmd = $"/home {config.HomeName}".Trim();
        string setHomeCmd = $"/sethome {config.HomeName}".Trim();
        int msPerBlock = InputSimulator.MsPerBlock(moveMode);

        string modeName = moveMode switch
        {
            ColumnMoveMode.Walk => "Marche",
            ColumnMoveMode.Sprint => "Sprint",
            ColumnMoveMode.Sneak => "Accroupi",
            _ => "Marche"
        };

        log($"Mode Colonne : {width} largeur x {length} longueur x {depth} profondeur ({totalBlocks} blocs)");
        log($"Deplacement : {modeName} ({msPerBlock}ms/bloc)");
        log($"Home : {homeCmd} | SetHome : {setHomeCmd}");

        int blocksMined = 0;

        for (int w = 0; w < width; w++)
        {
            ct.ThrowIfCancellationRequested();
            await pauseToken.WaitIfPausedAsync(ct);
            log($"=== Bande {w + 1}/{width} ===");

            // ── Miner les positions l = length-1 (loin) jusqu'a l = 1 (proche) ──
            for (int l = length - 1; l >= 1; l--)
            {
                ct.ThrowIfCancellationRequested();
                await pauseToken.WaitIfPausedAsync(ct);

                if (!safetyCheck())
                {
                    log("Minecraft n'est plus au premier plan, on arrete.");
                    return;
                }

                // 1. /home pour revenir au point de depart
                log($"  /home -> retour au depart");
                await input.TypeChatCommand(homeCmd, ct);
                await Task.Delay(5300, ct); // attendre la teleportation (5s + marge sur Pactify)

                // 2. Prendre la pioche
                await input.SelectSlot(config.PickaxeSlot, ct);
                await Task.Delay(200, ct);

                // 3. Reculer de l blocs
                log($"  Recul de {l} bloc(s) ({modeName})...");
                await input.MoveBackwardWithMode(l, moveMode, ct);
                await Task.Delay(300, ct);

                // 4. Miner D blocs vers le bas (le joueur tombe a chaque bloc)
                log($"  Minage de {depth} blocs en profondeur (pos {l})...");
                blocksMined = await MineDepthColumn(input, config, depth, blocksMined, totalBlocks, log, onProgress, pauseToken, ct);

                log($"  Position ({w},{l}) terminee ! [{blocksMined}/{totalBlocks}]");
            }

            // ── Gestion de la position l=0 (bloc du home) ──
            if (w < width - 1)
            {
                // Pas la derniere bande : decaler le home a gauche avant de miner l=0
                ct.ThrowIfCancellationRequested();
                await pauseToken.WaitIfPausedAsync(ct);

                if (!safetyCheck())
                {
                    log("Minecraft n'est plus au premier plan, on arrete.");
                    return;
                }

                // /home pour revenir au point de depart
                log($"  /home -> retour au depart pour decalage");
                await input.TypeChatCommand(homeCmd, ct);
                await Task.Delay(5300, ct); // attendre la teleportation (5s + marge sur Pactify)

                await input.SelectSlot(config.PickaxeSlot, ct);
                await Task.Delay(200, ct);

                // Strafe gauche de 1 bloc
                log($"  Decalage de 1 bloc vers la gauche...");
                await input.MoveLeftWithMode(1, moveMode, ct);
                await Task.Delay(300, ct);

                // /sethome pour mettre a jour le home
                log($"  {setHomeCmd} -> nouveau home");
                await input.TypeChatCommand(setHomeCmd, ct);
                await Task.Delay(1500, ct);

                // Strafe droite de 1 bloc pour revenir a l'ancien home (position l=0)
                log($"  Retour de 1 bloc vers la droite...");
                await input.MoveRightWithMode(1, moveMode, ct);
                await Task.Delay(300, ct);

                // Reprendre la pioche
                await input.SelectSlot(config.PickaxeSlot, ct);
                await Task.Delay(200, ct);

                // Miner D blocs vers le bas a l'ancien home
                log($"  Minage de {depth} blocs a l'ancien home (pos 0)...");
                blocksMined = await MineDepthColumn(input, config, depth, blocksMined, totalBlocks, log, onProgress, pauseToken, ct);

                log($"  Position ({w},0) terminee ! [{blocksMined}/{totalBlocks}]");
            }
            else
            {
                // Derniere bande : miner la position home directement
                ct.ThrowIfCancellationRequested();
                await pauseToken.WaitIfPausedAsync(ct);

                if (!safetyCheck())
                {
                    log("Minecraft n'est plus au premier plan, on arrete.");
                    return;
                }

                // /home pour revenir
                log($"  /home -> retour au depart (derniere position)");
                await input.TypeChatCommand(homeCmd, ct);
                await Task.Delay(5300, ct); // attendre la teleportation (5s + marge sur Pactify)

                await input.SelectSlot(config.PickaxeSlot, ct);
                await Task.Delay(200, ct);

                // Miner directement sous soi
                log($"  Minage de {depth} blocs au home (derniere position)...");
                blocksMined = await MineDepthColumn(input, config, depth, blocksMined, totalBlocks, log, onProgress, pauseToken, ct);

                log($"  Position ({w},0) terminee ! [{blocksMined}/{totalBlocks}]");
            }
        }

        log($"Fini ! {blocksMined} blocs mines au total.");
    }

    /// <summary>
    /// Mine une colonne verticale de <paramref name="depth"/> blocs vers le bas.
    /// Le joueur regarde deja en bas (90/90), il mine le bloc sous lui et tombe.
    /// </summary>
    private static async Task<int> MineDepthColumn(
        InputSimulator input,
        MiningConfig config,
        int depth,
        int blocksMined,
        int totalBlocks,
        Action<string> log,
        Action<int> onProgress,
        PauseToken pauseToken,
        CancellationToken ct)
    {
        for (int d = 0; d < depth; d++)
        {
            ct.ThrowIfCancellationRequested();
            await pauseToken.WaitIfPausedAsync(ct);

            blocksMined++;

            // Miner le bloc sous soi
            await input.MineBlock(config.MiningDurationMs, ct);

            // Petit delai pour laisser le joueur tomber d'un bloc
            await Task.Delay(config.ActionDelayMs, ct);

            onProgress(blocksMined);
        }

        return blocksMined;
    }
}
