using System.Threading;
using System.Threading.Tasks;

namespace AutoMinePactify.Models;

/// <summary>
/// Jeton de pause asynchrone. Permet de mettre en pause et reprendre
/// une tache en cours sans la detruire (contrairement a CancellationToken).
/// Thread-safe avec lock pour eviter les race conditions.
/// </summary>
public class PauseToken
{
    private readonly object _lock = new();
    private TaskCompletionSource<bool>? _pauseTcs;

    /// <summary>True si actuellement en pause.</summary>
    public bool IsPaused
    {
        get
        {
            lock (_lock)
            {
                return _pauseTcs != null;
            }
        }
    }

    /// <summary>Met en pause : les prochains appels a WaitIfPausedAsync vont bloquer.</summary>
    public void Pause()
    {
        lock (_lock)
        {
            _pauseTcs ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>Reprend : debloque tous les appels a WaitIfPausedAsync.</summary>
    public void Resume()
    {
        TaskCompletionSource<bool>? tcs;
        lock (_lock)
        {
            tcs = _pauseTcs;
            _pauseTcs = null;
        }
        // TrySetResult en dehors du lock pour eviter un deadlock
        tcs?.TrySetResult(true);
    }

    /// <summary>
    /// Attend si en pause, sinon retourne immediatement.
    /// Respecte le CancellationToken pour pouvoir arreter meme en pause.
    /// </summary>
    public async Task WaitIfPausedAsync(CancellationToken ct)
    {
        TaskCompletionSource<bool>? tcs;
        lock (_lock)
        {
            tcs = _pauseTcs;
        }

        if (tcs == null) return;

        // Si on annule pendant la pause, on leve OperationCanceledException
        using var reg = ct.Register(() => tcs.TrySetCanceled());
        await tcs.Task;
    }
}
