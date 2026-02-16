using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMinePactify.Models;
using AutoMinePactify.Services;

namespace AutoMinePactify.Patterns;

public class AutoClickPattern : IMiningPattern
{
    public string Name => "Auto-Clic";
    public string Description => "Casse le bloc devant toi en boucle.";

    public async Task ExecuteAsync(
        InputSimulator input,
        MiningConfig config,
        Action<string> log,
        Action<int> onProgress,
        Func<bool> safetyCheck,
        CancellationToken ct)
    {
        log($"Auto-Clic : {config.BlockCount} blocs a casser");

        // On prend la pioche
        await input.SelectSlot(config.PickaxeSlot, ct);
        log($"Pioche prise (case {config.PickaxeSlot})");
        await Task.Delay(300, ct);

        for (int i = 0; i < config.BlockCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            if (!safetyCheck())
            {
                log("T'as clique ailleurs que Minecraft, on arrete par securite.");
                return;
            }

            log($"Bloc {i + 1}/{config.BlockCount} - en train de casser...");

            await input.MineBlock(config.MiningDurationMs, ct);
            await Task.Delay(config.ActionDelayMs, ct);

            onProgress(i + 1);
        }

        log($"Fini ! {config.BlockCount} blocs casses !");
    }
}
