using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMinePactify.Helpers;

namespace AutoMinePactify.Services;

/// <summary>
/// Polls global keyboard state for hotkey detection (F6 toggle, F8 emergency stop).
/// Uses GetAsyncKeyState for simplicity and reliability.
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

                    await Task.Delay(50, ct);
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
