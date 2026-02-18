using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMinePactify.Models;
using AutoMinePactify.Services;

namespace AutoMinePactify.Patterns;

/// <summary>
/// Common interface for all mining patterns.
/// Each pattern defines a sequence of actions to execute in Minecraft.
/// </summary>
public interface IMiningPattern
{
    /// <summary>Display name of the pattern.</summary>
    string Name { get; }

    /// <summary>Short description of what the pattern does.</summary>
    string Description { get; }

    /// <summary>
    /// Execute the mining pattern asynchronously.
    /// </summary>
    /// <param name="input">Input simulator for sending keyboard/mouse actions.</param>
    /// <param name="config">Mining configuration (lengths, delays, etc.).</param>
    /// <param name="log">Callback to log messages to the UI.</param>
    /// <param name="onProgress">Callback to report progress (blocks mined).</param>
    /// <param name="safetyCheck">Callback that returns false if mining should stop (MC lost focus, etc.).</param>
    /// <param name="pauseToken">Token de pause : appeler WaitIfPausedAsync pour bloquer si en pause.</param>
    /// <param name="ct">Cancellation token to stop mining.</param>
    Task ExecuteAsync(
        InputSimulator input,
        MiningConfig config,
        Action<string> log,
        Action<int> onProgress,
        Func<bool> safetyCheck,
        PauseToken pauseToken,
        CancellationToken ct);
}
