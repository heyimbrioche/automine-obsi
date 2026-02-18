using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoMinePactify.Models;
using AutoMinePactify.Services;

namespace AutoMinePactify.ViewModels;

/// <summary>
/// ViewModel autonome pour le module "Commandes Rapides".
/// Fonctionne independamment du minage.
/// </summary>
public partial class QuickCommandsViewModel : ObservableObject, IDisposable
{
    private readonly InputSimulator _inputSimulator;
    private readonly MinecraftWindowService _mcService;
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    // ─── Observable ─────────────────────────────────────────────────

    public ObservableCollection<QuickCommandItemViewModel> Commands { get; } = new();

    public ObservableCollection<string> LogEntries { get; } = new();

    [ObservableProperty]
    private bool _globalEnabled = true;

    [ObservableProperty]
    private string _statusText = "Pret";

    [ObservableProperty]
    private int _executionCount;

    // ─── Constructor ────────────────────────────────────────────────

    public QuickCommandsViewModel()
    {
        _inputSimulator = new InputSimulator();
        _mcService = new MinecraftWindowService();
    }

    public QuickCommandsViewModel(InputSimulator inputSimulator, MinecraftWindowService mcService)
    {
        _inputSimulator = inputSimulator;
        _mcService = mcService;
    }

    // ─── Polling hotkeys ────────────────────────────────────────────

    /// <summary>
    /// Demarre l'ecoute des hotkeys pour les commandes rapides.
    /// </summary>
    public void StartListening()
    {
        if (_pollTask != null && !_pollTask.IsCompleted) return;

        _pollCts = new CancellationTokenSource();
        var ct = _pollCts.Token;

        _pollTask = Task.Run(async () =>
        {
            // Track key states per VK code
            var wasDown = new Dictionary<int, bool>();

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (!GlobalEnabled)
                    {
                        await Task.Delay(50, ct);
                        continue;
                    }

                    // Snapshot commands on UI thread (supports modifier combos)
                    List<(bool shift, bool ctrl, bool alt, int mainVk, string cmd, QuickCommandSpeed speed, string name, bool capturing)>? snapshot = null;
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        snapshot = Commands
                            .Where(c => c.Enabled && !string.IsNullOrWhiteSpace(c.Key) && !string.IsNullOrWhiteSpace(c.Command))
                            .Select(c =>
                            {
                                var parsed = HotkeyService.ParseKeybind(c.Key);
                                return (
                                    shift: parsed.shift,
                                    ctrl: parsed.ctrl,
                                    alt: parsed.alt,
                                    mainVk: parsed.mainVk,
                                    cmd: c.Command,
                                    speed: c.Speed,
                                    name: c.Name,
                                    capturing: c.IsCapturing
                                );
                            })
                            .Where(x => x.mainVk > 0 && x.mainVk != 0x75 && x.mainVk != 0x77 && !x.capturing) // pas F6/F8, pas en capture
                            .ToList();
                    });

                    if (snapshot == null || snapshot.Count == 0)
                    {
                        await Task.Delay(50, ct);
                        continue;
                    }

                    foreach (var (shift, ctrl, alt, mainVk, cmd, speed, name, _) in snapshot)
                    {
                        bool isDown = (Helpers.NativeMethods.GetAsyncKeyState(mainVk) & 0x8000) != 0;

                        // Verifier que les modifiers correspondent
                        if (isDown)
                        {
                            bool shiftOk = !shift || HotkeyService.IsShiftDown();
                            bool ctrlOk = !ctrl || HotkeyService.IsCtrlDown();
                            bool altOk = !alt || HotkeyService.IsAltDown();
                            isDown = shiftOk && ctrlOk && altOk;
                        }

                        wasDown.TryGetValue(mainVk, out bool was);

                        if (isDown && !was)
                        {
                            // Key just pressed with matching modifiers
                            if (_mcService.IsMinecraftFocused())
                            {
                                _ = ExecuteCommand(cmd, speed, name);
                            }
                        }

                        wasDown[mainVk] = isDown;
                    }

                    // Clean up old keys that are no longer registered
                    var activeVks = snapshot.Select(s => s.mainVk).ToHashSet();
                    foreach (var key in wasDown.Keys.ToList())
                    {
                        if (!activeVks.Contains(key))
                            wasDown.Remove(key);
                    }

                    await Task.Delay(25, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(100, ct);
                }
            }
        }, ct);

        AddLog("Ecoute des commandes rapides demarree.");
    }

    /// <summary>
    /// Arrete l'ecoute.
    /// </summary>
    public void StopListening()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }

    private async Task ExecuteCommand(string command, QuickCommandSpeed speed, string name)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AddLog($"Execution : {name} → {command}");
                StatusText = $"Derniere : {name}";
                ExecutionCount++;
            });

            await _inputSimulator.TypeChatCommandFast(command, speed);
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                AddLog($"Erreur ({name}) : {ex.Message}"));
        }
    }

    // ─── Commands (UI) ──────────────────────────────────────────────

    [RelayCommand]
    private void AddCommand()
    {
        Commands.Add(new QuickCommandItemViewModel
        {
            Name = $"Commande {Commands.Count + 1}",
            Key = "F7",
            Command = "/feed",
            Speed = QuickCommandSpeed.Fast,
            Enabled = true
        });
        AddLog($"Commande ajoutee ({Commands.Count} au total).");
    }

    [RelayCommand]
    private void RemoveCommand(QuickCommandItemViewModel? item)
    {
        if (item == null) return;
        Commands.Remove(item);
        AddLog($"Commande \"{item.Name}\" supprimee.");
    }

    [RelayCommand]
    private void ClearLogs()
    {
        LogEntries.Clear();
    }

    // ─── Persistence ────────────────────────────────────────────────

    public void LoadFromConfig(List<QuickCommandEntry>? entries, bool globalEnabled)
    {
        Commands.Clear();
        GlobalEnabled = globalEnabled;

        if (entries == null || entries.Count == 0)
        {
            // Default : une commande /feed sur F7
            Commands.Add(new QuickCommandItemViewModel
            {
                Name = "Feed",
                Key = "F7",
                Command = "/feed",
                Speed = QuickCommandSpeed.Fast,
                Enabled = true
            });
            return;
        }

        foreach (var entry in entries)
        {
            Commands.Add(QuickCommandItemViewModel.FromEntry(entry));
        }
    }

    public List<QuickCommandEntry> ToEntries()
    {
        return Commands.Select(c => c.ToEntry()).ToList();
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private void AddLog(string message)
    {
        string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        LogEntries.Add(entry);

        while (LogEntries.Count > 300)
            LogEntries.RemoveAt(0);
    }

    public void Dispose()
    {
        StopListening();
        GC.SuppressFinalize(this);
    }
}
