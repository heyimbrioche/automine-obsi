using System.Threading;
using System.Threading.Tasks;

namespace AutoMinePactify.Models;

/// <summary>
/// Jeton de pause asynchrone. Permet de mettre en pause et reprendre
/// une tache en cours sans la detruire (contrairement a CancellationToken).
/// </summary>
public class PauseToken
{
    private volatile TaskCompletionSource<bool>? _pauseTcs;

    /// <summary>True si actuellement en pause.</summary>
    public bool IsPaused => _pauseTcs != null;

    /// <summary>Met en pause : les prochains appels a WaitIfPausedAsync vont bloquer.</summary>
    public void Pause()
    {
        // Creer le TCS seulement si pas deja en pause
        _pauseTcs ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>Reprend : debloque tous les appels a WaitIfPausedAsync.</summary>
    public void Resume()
    {
        var tcs = _pauseTcs;
        _pauseTcs = null;
        tcs?.TrySetResult(true);
    }

    /// <summary>
    /// Attend si en pause, sinon retourne immediatement.
    /// Respecte le CancellationToken pour pouvoir arreter meme en pause.
    /// </summary>
    public async Task WaitIfPausedAsync(CancellationToken ct)
    {
        var tcs = _pauseTcs;
        if (tcs == null) return;

        // Si on annule pendant la pause, on leve OperationCanceledException
        using var reg = ct.Register(() => tcs.TrySetCanceled());
        await tcs.Task;
    }
}
