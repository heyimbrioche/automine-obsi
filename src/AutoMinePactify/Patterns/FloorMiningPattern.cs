using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMinePactify.Models;
using AutoMinePactify.Services;

namespace AutoMinePactify.Patterns;

public class FloorMiningPattern : IMiningPattern
{
    public string Name => "Minage de Sol";
    public string Description => "Casse le sol d'obsidienne sous tes pieds.";

    public async Task ExecuteAsync(
        InputSimulator input,
        MiningConfig config,
        Action<string> log,
        Action<int> onProgress,
        Func<bool> safetyCheck,
        PauseToken pauseToken,
        CancellationToken ct)
    {
        int width = config.FloorWidth;
        int depth = config.FloorDepth;
        int totalBlocks = width * depth;

        log($"Minage de Sol : {width} de large x {depth} de long = {totalBlocks} blocs");

        // On prend la pioche
        await input.SelectSlot(config.PickaxeSlot, ct);
        log($"Pioche prise (case {config.PickaxeSlot})");
        await Task.Delay(300, ct);

        // On regarde vers le bas
        log("Le perso regarde vers le bas...");
        await input.LookDown(300, ct);
        await Task.Delay(200, ct);

        int blocksDone = 0;
        bool goingForward = true;

        for (int row = 0; row < width; row++)
        {
            ct.ThrowIfCancellationRequested();

            log($"Ligne {row + 1}/{width}");

            for (int col = 0; col < depth; col++)
            {
                ct.ThrowIfCancellationRequested();
                await pauseToken.WaitIfPausedAsync(ct);

                if (!safetyCheck())
                {
                    log("T'as clique ailleurs que Minecraft, on arrete par securite.");
                    return;
                }

                blocksDone++;
                log($"Bloc {blocksDone}/{totalBlocks}");

                await input.MineBlock(config.MiningDurationMs, ct);
                await Task.Delay(config.ActionDelayMs, ct);

                onProgress(blocksDone);

                // On avance au bloc suivant
                if (col < depth - 1)
                {
                    if (config.AntiChute)
                    {
                        await input.SneakAction(
                            async token => await input.MoveForward(380, token), ct);
                    }
                    else
                    {
                        await input.MoveForward(380, ct);
                    }
                    await Task.Delay(150, ct);
                }
            }

            // On passe a la ligne d'a cote
            if (row < width - 1)
            {
                if (config.AntiChute)
                {
                    await input.SneakAction(
                        async token => await input.MoveRight(380, token), ct);
                }
                else
                {
                    await input.MoveRight(380, ct);
                }

                await Task.Delay(200, ct);

                // Demi-tour
                await input.TurnAround(ct);
                await Task.Delay(300, ct);

                goingForward = !goingForward;
            }
        }

        // On remet le regard normal
        log("On remet le regard droit...");
        await input.LookUp(300, ct);

        log($"Sol fini ! {totalBlocks} blocs casses !");
    }
}
