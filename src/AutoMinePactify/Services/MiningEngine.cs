using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMinePactify.Models;
using AutoMinePactify.Patterns;

namespace AutoMinePactify.Services;

/// <summary>
/// Moteur central qui orchestre les opérations de minage d'obsidienne.
/// Gère les transitions d'état, l'exécution des patterns et les contrôles de sécurité.
/// </summary>
public class MiningEngine
{
    private readonly InputSimulator _input;
    private readonly MinecraftWindowService _mcService;
    private readonly SafetyService _safety;
    private readonly ScreenCaptureService _screenCapture;
    private readonly ObsidianDetector _obsidianDetector;
    private readonly PlayerDetector _playerDetector;
    private readonly PickaxeChecker _pickaxeChecker;
    private CancellationTokenSource? _cts;
    private Task? _miningTask;

    public EngineState State { get; private set; } = EngineState.Idle;
    public ObsidianDetector ObsidianDetector => _obsidianDetector;

    // ─── Events ─────────────────────────────────────────────────────

    public event Action<string>? OnLog;
    public event Action<EngineState>? OnStateChanged;
    public event Action<int>? OnProgressChanged;

    // ─── Constructor ────────────────────────────────────────────────

    public MiningEngine(InputSimulator input, MinecraftWindowService mcService, SafetyService safety)
    {
        _input = input;
        _mcService = mcService;
        _safety = safety;
        _screenCapture = new ScreenCaptureService();
        _obsidianDetector = new ObsidianDetector();
        _playerDetector = new PlayerDetector();
        _pickaxeChecker = new PickaxeChecker();
    }

    /// <summary>
    /// Calibre la couleur de l'obsidienne en capturant l'ecran et en lisant le centre.
    /// </summary>
    public bool CalibrateObsidian()
    {
        if (!_mcService.IsMinecraftDetected)
        {
            Log("Trouve Minecraft d'abord avant de calibrer !");
            return false;
        }

        var frame = _screenCapture.CaptureWindow(_mcService.MinecraftHandle);
        if (!frame.IsValid)
        {
            Log("Impossible de capturer l'ecran. Minecraft est en mode fenetre ?");
            return false;
        }

        bool ok = _obsidianDetector.Calibrate(frame);
        if (ok)
        {
            Log($"Couleur apprise ! R={_obsidianDetector.RefR} G={_obsidianDetector.RefG} B={_obsidianDetector.RefB}");
        }
        else
        {
            Log("Probleme pour lire la couleur. Reessaye.");
        }
        return ok;
    }

    // ─── Public API ─────────────────────────────────────────────────

    public bool DetectMinecraft()
    {
        SetState(EngineState.Detecting);
        bool found = _mcService.DetectMinecraft();

        if (found)
        {
            Log($"Minecraft trouve ! ({_mcService.GetWindowTitle()})");
            SetState(EngineState.Ready);
        }
        else
        {
            Log("Minecraft pas trouve. Lance le jeu d'abord et reessaye.");
            SetState(EngineState.Error);
        }

        return found;
    }

    public void StartMining(MiningConfig config)
    {
        if (State == EngineState.Mining)
        {
            Log("C'est deja en train de miner la !");
            return;
        }

        if (!_mcService.IsMinecraftDetected)
        {
            if (!DetectMinecraft())
                return;
        }

        // Configure input simulator
        _input.Humanize = config.HumanizeDelays;

        // Résoudre le pattern obsidienne
        IMiningPattern pattern = config.PatternType switch
        {
            MiningPatternType.AutoClick => new AutoClickPattern(),
            MiningPatternType.WallBreaker => new WallBreakerPattern(),
            MiningPatternType.FloorMining => new FloorMiningPattern(),
            MiningPatternType.ColumnMining => new ColumnMiningPattern(),
            MiningPatternType.SmartMining => new SmartMiningPattern(
                _screenCapture, _obsidianDetector, _playerDetector, _pickaxeChecker,
                _mcService.MinecraftHandle, config.PlayerSafetyEnabled),
            _ => throw new ArgumentException($"Pattern inconnu : {config.PatternType}")
        };

        Log($"Mode : {pattern.Name}");
        Log("Place-toi bien dans Minecraft comme dans les instructions !");
        Log("Ca commence dans 3 secondes...");

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _miningTask = Task.Run(async () =>
        {
            try
            {
                // Countdown with log messages
                Log("3...");
                await Task.Delay(1000, ct);
                Log("2...");
                await Task.Delay(1000, ct);
                Log("1...");
                await Task.Delay(1000, ct);

                // Focus Minecraft
                Log("On passe sur Minecraft...");
                _mcService.BringToForeground();
                await Task.Delay(500, ct);

                Log("C'est parti ! Touche plus a rien !");
                SetState(EngineState.Mining);

                await pattern.ExecuteAsync(
                    _input,
                    config,
                    msg => Log(msg),
                    progress => OnProgressChanged?.Invoke(progress),
                    () => _safety.IsSafeToContinue(),
                    ct);

                Log("Fini ! Tout a ete mine.");
                SetState(EngineState.Ready);
            }
            catch (OperationCanceledException)
            {
                Log("Minage arrete.");
                SetState(EngineState.Ready);
            }
            catch (Exception ex)
            {
                Log($"Probleme : {ex.Message}");
                SetState(EngineState.Error);
            }
            finally
            {
                _input.ReleaseAllKeys();
            }
        }, ct);
    }

    public void ToggleMining(MiningConfig config)
    {
        if (State == EngineState.Mining)
        {
            StopMining();
        }
        else if (State == EngineState.Ready || State == EngineState.Idle)
        {
            StartMining(config);
        }
    }

    public void StopMining()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            Log("On arrete le minage...");
            _cts.Cancel();
        }
        _input.ReleaseAllKeys();
    }

    public void EmergencyStop()
    {
        _cts?.Cancel();
        _input.ReleaseAllKeys();
        _safety.EmergencyRelease();
        SetState(EngineState.Idle);
        Log("STOP ! Tout est arrete d'urgence !");
    }

    // ─── Internal ───────────────────────────────────────────────────

    private void SetState(EngineState newState)
    {
        if (State == newState) return;
        State = newState;
        OnStateChanged?.Invoke(newState);
    }

    private void Log(string message)
    {
        OnLog?.Invoke(message);
    }
}
