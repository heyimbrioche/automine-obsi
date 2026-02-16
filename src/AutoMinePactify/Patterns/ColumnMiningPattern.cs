using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMinePactify.Models;
using AutoMinePactify.Services;

namespace AutoMinePactify.Patterns;

/// <summary>
/// Mine une colonne d'obsidienne vers le bas, couche par couche.
/// Chaque couche fait ColumnWidth x ColumnLength blocs.
/// Apres chaque couche, fait /home pour revenir au point de depart
/// et recommencer la couche suivante (qui est maintenant visible).
///
/// Le joueur doit :
/// 1. Se placer au coin de la colonne, regarder vers le bas
/// 2. Avoir fait /sethome [nom] a cette position
/// 3. Lancer le minage
/// </summary>
public class ColumnMiningPattern : IMiningPattern
{
    public string Name => "Colonne";
    public string Description => "Mine une colonne vers le bas avec /home.";

    public async Task ExecuteAsync(
        InputSimulator input,
        MiningConfig config,
        Action<string> log,
        Action<int> onProgress,
        Func<bool> safetyCheck,
        CancellationToken ct)
    {
        int width = Math.Max(1, config.ColumnWidth);
        int length = Math.Max(1, config.ColumnLength);
        int layers = Math.Max(1, config.ColumnLayers);
        int blocksPerLayer = width * length;
        int totalBlocks = blocksPerLayer * layers;
        string homeCmd = $"/home {config.HomeName}".Trim();

        log($"Mode Colonne : {width}x{length}, {layers} couches ({totalBlocks} blocs)");
        log($"Home : {homeCmd}");

        // Prendre la pioche
        await input.SelectSlot(config.PickaxeSlot, ct);
        log($"Pioche prise (case {config.PickaxeSlot})");
        await Task.Delay(300, ct);

        int blocksMined = 0;

        for (int layer = 0; layer < layers; layer++)
        {
            ct.ThrowIfCancellationRequested();
            log($"--- Couche {layer + 1}/{layers} ---");

            // Regarder vers le bas pour miner sous soi
            log("Je regarde en bas...");
            await input.LookDown(300, ct);
            await Task.Delay(200, ct);

            // Miner la couche : serpentin largeur x longueur
            for (int z = 0; z < length; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!safetyCheck())
                    {
                        log("Minecraft n'est plus au premier plan, on arrete.");
                        return;
                    }

                    blocksMined++;
                    log($"Bloc {blocksMined}/{totalBlocks} (couche {layer + 1}, pos {x + 1},{z + 1})");

                    // Miner le bloc
                    await input.MineBlock(config.MiningDurationMs, ct);
                    await Task.Delay(config.ActionDelayMs, ct);

                    onProgress(blocksMined);

                    // Se deplacer au bloc suivant dans la largeur (sauf le dernier)
                    if (x < width - 1)
                    {
                        // Decaler le viseur vers la droite pour viser le bloc d'a cote
                        // En regardant en bas, un bloc fait ~40-50 pixels selon la distance
                        await input.MouseMove(45, 0, ct);
                        await Task.Delay(150, ct);
                    }
                }

                // Fin de la rangee, passer a la rangee suivante (en longueur)
                if (z < length - 1)
                {
                    // Decaler le viseur vers l'avant
                    await input.MouseMove(0, -45, ct);
                    await Task.Delay(150, ct);

                    // Inverser la direction de la largeur pour le serpentin
                    // (on revient en arriere sur la prochaine rangee)
                    if (width > 1)
                    {
                        await input.MouseMove(-(width - 1) * 45, 0, ct);
                        await Task.Delay(150, ct);
                    }
                }
            }

            log($"Couche {layer + 1} terminee !");

            // Si c'est la derniere couche, pas besoin de /home
            if (layer >= layers - 1)
            {
                log("Derniere couche finie !");
                break;
            }

            // /home pour revenir au point de depart
            log($"Retour au home ({homeCmd})...");
            await input.TypeChatCommand(homeCmd, ct);
            await Task.Delay(2000, ct); // attendre la teleportation

            // Reprendre la pioche apres le TP (au cas ou)
            await input.SelectSlot(config.PickaxeSlot, ct);
            await Task.Delay(300, ct);
        }

        log($"Fini ! {blocksMined} blocs mines au total.");
    }
}
