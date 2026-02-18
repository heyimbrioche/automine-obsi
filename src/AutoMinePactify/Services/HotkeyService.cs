using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMinePactify.Helpers;

namespace AutoMinePactify.Services;

/// <summary>
/// Polls global keyboard state for hotkey detection (F6 toggle, F8 emergency stop).
/// Les commandes rapides sont gerees par QuickCommandsViewModel de facon autonome.
/// </summary>
public class HotkeyService : IDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;

    /// <summary>Fired when F6 is pressed (toggle mining).</summary>
    public event Action? OnToggleHotkey;

    /// <summary>Fired when F8 is pressed (emergency stop).</summary>
    public event Action? OnEmergencyStop;

    public bool IsRunning => _pollingTask != null && !_pollingTask.IsCompleted;

    // ─── Key name ↔ VK code mapping ─────────────────────────────────

    private static readonly Dictionary<string, int> KeyNameToVk = new(StringComparer.OrdinalIgnoreCase)
    {
        { "F1", 0x70 }, { "F2", 0x71 }, { "F3", 0x72 }, { "F4", 0x73 },
        { "F5", 0x74 }, { "F6", 0x75 }, { "F7", 0x76 }, { "F8", 0x77 },
        { "F9", 0x78 }, { "F10", 0x79 }, { "F11", 0x7A }, { "F12", 0x7B },
        { "A", 0x41 }, { "B", 0x42 }, { "C", 0x43 }, { "D", 0x44 },
        { "E", 0x45 }, { "F", 0x46 }, { "G", 0x47 }, { "H", 0x48 },
        { "I", 0x49 }, { "J", 0x4A }, { "K", 0x4B }, { "L", 0x4C },
        { "M", 0x4D }, { "N", 0x4E }, { "O", 0x4F }, { "P", 0x50 },
        { "Q", 0x51 }, { "R", 0x52 }, { "S", 0x53 }, { "T", 0x54 },
        { "U", 0x55 }, { "V", 0x56 }, { "W", 0x57 }, { "X", 0x58 },
        { "Y", 0x59 }, { "Z", 0x5A },
        { "0", 0x30 }, { "1", 0x31 }, { "2", 0x32 }, { "3", 0x33 },
        { "4", 0x34 }, { "5", 0x35 }, { "6", 0x36 }, { "7", 0x37 },
        { "8", 0x38 }, { "9", 0x39 },
    };

    /// <summary>
    /// Convertit un nom de touche (ex: "F7", "G") en VK code.
    /// Retourne 0 si non reconnu.
    /// </summary>
    public static int KeyNameToVkCode(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName)) return 0;
        return KeyNameToVk.TryGetValue(keyName.Trim(), out int vk) ? vk : 0;
    }

    /// <summary>
    /// Retourne la liste des noms de touches disponibles.
    /// </summary>
    public static IReadOnlyCollection<string> AvailableKeys => KeyNameToVk.Keys;

    // ─── Polling ────────────────────────────────────────────────────

    /// <summary>Start listening for hotkeys in the background.</summary>
    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _pollingTask = Task.Run(async () =>
        {
            bool f6WasDown = false;
            bool f8WasDown = false;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // F6 - toggle
                    bool f6IsDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_F6) & 0x8000) != 0;
                    if (f6IsDown && !f6WasDown)
                    {
                        OnToggleHotkey?.Invoke();
                    }
                    f6WasDown = f6IsDown;

                    // F8 - emergency stop
                    bool f8IsDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_F8) & 0x8000) != 0;
                    if (f8IsDown && !f8WasDown)
                    {
                        OnEmergencyStop?.Invoke();
                    }
                    f8WasDown = f8IsDown;

                    await Task.Delay(25, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, ct);
    }

    /// <summary>Stop listening for hotkeys.</summary>
    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
