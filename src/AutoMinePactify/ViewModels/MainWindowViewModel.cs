using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoMinePactify.Models;
using AutoMinePactify.Services;

namespace AutoMinePactify.ViewModels;

/// <summary>
/// ViewModel principal orchestrant l'état de l'interface.
/// </summary>
public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    // ─── Services ───────────────────────────────────────────────────

    private readonly InputSimulator _inputSimulator;
    private readonly MinecraftWindowService _mcService;
    private readonly SafetyService _safetyService;
    private readonly MiningEngine _engine;
    private readonly HotkeyService _hotkeyService;

    // ─── Observable properties ──────────────────────────────────────

    [ObservableProperty]
    private string _statusText = "Pas lance";

    [ObservableProperty]
    private string _mcStatusText = "Pas trouve";

    [ObservableProperty]
    private bool _isMcDetected;

    [ObservableProperty]
    private bool _isMining;

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private int _maxProgress = 20;

    [ObservableProperty]
    private SettingsViewModel _settings = new();

    [ObservableProperty]
    private bool _updateAvailable;

    [ObservableProperty]
    private string _updateMessage = "";

    [ObservableProperty]
    private string _updateDownloadUrl = "";

    public string VersionText => $"v{Services.UpdateChecker.CurrentVersion}";

    public ObservableCollection<string> LogEntries { get; } = new();

    // ─── Constructor ────────────────────────────────────────────────

    public MainWindowViewModel()
    {
        _inputSimulator = new InputSimulator();
        _mcService = new MinecraftWindowService();
        _safetyService = new SafetyService(_mcService, _inputSimulator);
        _engine = new MiningEngine(_inputSimulator, _mcService, _safetyService);
        _hotkeyService = new HotkeyService();

        // Wire up engine events
        _engine.OnLog += msg =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                string entry = $"[{DateTime.Now:HH:mm:ss}] {msg}";
                LogEntries.Add(entry);

                // Keep log under 500 entries
                while (LogEntries.Count > 500)
                    LogEntries.RemoveAt(0);
            });
        };

        _engine.OnStateChanged += state =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsMining = state == EngineState.Mining;
                StatusText = state switch
                {
                    EngineState.Idle => "Pas lance",
                    EngineState.Detecting => "Recherche de Minecraft...",
                    EngineState.Ready => "Pret a miner",
                    EngineState.Mining => "En train de miner...",
                    EngineState.Paused => "En pause",
                    EngineState.Error => "Probleme",
                    _ => "???"
                };
            });
        };

        _engine.OnProgressChanged += p =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Progress = p;
            });
        };

        // Wire up hotkeys
        _hotkeyService.OnToggleHotkey += () =>
        {
            Dispatcher.UIThread.InvokeAsync(() => ToggleMining());
        };

        _hotkeyService.OnEmergencyStop += () =>
        {
            Dispatcher.UIThread.InvokeAsync(() => EmergencyStop());
        };

        // Start hotkey listener
        _hotkeyService.Start();

        // Charger les settings sauvegardees
        Settings.LoadFromDisk();

        AddLog("AutoMine Obsidienne pret !");
        AddLog("Appuie sur F6 pour lancer le minage.");
        AddLog("Si ca bug, appuie sur F8 pour tout arreter direct.");
    }

    // ─── Commands ───────────────────────────────────────────────────

    [RelayCommand]
    private void DetectMinecraft()
    {
        bool found = _engine.DetectMinecraft();
        IsMcDetected = found;
        McStatusText = found ? _mcService.GetWindowTitle() : "Pas trouve";
    }

    [RelayCommand]
    private void StartMining()
    {
        if (IsMining) return;

        // Calculer MaxProgress selon le mode sélectionné
        MaxProgress = Settings.TotalBlocks;
        Progress = 0;

        var config = Settings.ToConfig();
        _engine.StartMining(config);
    }

    [RelayCommand]
    private void StopMining()
    {
        _engine.StopMining();
    }

    [RelayCommand]
    private void EmergencyStop()
    {
        _engine.EmergencyStop();
        AddLog("STOP ! Tout est arrete.");
    }

    [RelayCommand]
    private void ClearLogs()
    {
        LogEntries.Clear();
    }

    [RelayCommand]
    private void CalibrateObsidian()
    {
        if (!IsMcDetected)
        {
            // Detecter d'abord
            DetectMinecraft();
            if (!IsMcDetected)
            {
                AddLog("Trouve Minecraft d'abord !");
                return;
            }
        }

        AddLog("Lecture de la couleur au centre de l'ecran...");
        bool ok = _engine.CalibrateObsidian();
        Settings.IsCalibrated = ok;
        if (ok)
        {
            AddLog("Couleur de l'obsidienne apprise ! Tu peux lancer le Full Auto.");
        }
    }

    [RelayCommand]
    private void DismissUpdate()
    {
        UpdateAvailable = false;
    }

    /// <summary>
    /// Affiche la notification de mise a jour.
    /// </summary>
    public void ShowUpdateNotification(Services.UpdateChecker.UpdateInfo info)
    {
        UpdateAvailable = true;
        UpdateMessage = $"Nouvelle version {info.Version} disponible !";
        if (!string.IsNullOrWhiteSpace(info.Changelog))
            UpdateMessage += $"\n{info.Changelog}";
        UpdateDownloadUrl = info.DownloadUrl;
        AddLog($"MISE A JOUR : version {info.Version} disponible !");
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private void ToggleMining()
    {
        if (IsMining)
        {
            StopMining();
        }
        else
        {
            MaxProgress = Settings.TotalBlocks;
            Progress = 0;
            var config = Settings.ToConfig();
            _engine.ToggleMining(config);
        }
    }

    private void AddLog(string message)
    {
        string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        LogEntries.Add(entry);
    }

    public void Dispose()
    {
        // Sauvegarder les settings avant de fermer
        Settings.SaveToDisk();

        _hotkeyService.Dispose();
        _engine.StopMining();
        GC.SuppressFinalize(this);
    }
}
