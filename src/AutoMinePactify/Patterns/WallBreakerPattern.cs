using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMinePactify.Models;
using AutoMinePactify.Services;

namespace AutoMinePactify.Patterns;

public class WallBreakerPattern : IMiningPattern
{
    public string Name => "Casse-Mur";
    public string Description => "Casse un mur d'obsidienne tout seul.";

    private const int PixelsPerBlock = 80;

    public async Task ExecuteAsync(
        InputSimulator input,
        MiningConfig config,
        Action<string> log,
        Action<int> onProgress,
        Func<bool> safetyCheck,
        PauseToken pauseToken,
        CancellationToken ct)
    {
        int width = config.WallWidth;
        int height = config.WallHeight;
        int totalBlocks = width * height;

        log($"Casse-Mur : {width} de large x {height} de haut = {totalBlocks} blocs");

        // On prend la pioche
        await input.SelectSlot(config.PickaxeSlot, ct);
        log($"Pioche prise (case {config.PickaxeSlot})");
        await Task.Delay(300, ct);

        int blocksDone = 0;
        bool goingRight = true;

        for (int row = 0; row < height; row++)
        {
            ct.ThrowIfCancellationRequested();

            log($"Ligne {row + 1}/{height}");

            for (int col = 0; col < width; col++)
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

                // On vise le bloc d'a cote (sauf le dernier de la ligne)
                if (col < width - 1)
                {
                    int dx = goingRight ? PixelsPerBlock : -PixelsPerBlock;
                    await SmoothMouseMove(input, dx, 0, ct);
                    await Task.Delay(150, ct);
                }
            }

            // On descend a la ligne du dessous
            if (row < height - 1)
            {
                await SmoothMouseMove(input, 0, PixelsPerBlock, ct);
                await Task.Delay(200, ct);
            }

            goingRight = !goingRight;
        }

        log($"Mur fini ! {totalBlocks} blocs casses !");
    }

    private static async Task SmoothMouseMove(InputSimulator input, int dx, int dy, CancellationToken ct)
    {
        int steps = 4;
        int stepDx = dx / steps;
        int stepDy = dy / steps;

        for (int i = 0; i < steps; i++)
        {
            await input.MouseMove(stepDx, stepDy, ct);
        }
    }
}
