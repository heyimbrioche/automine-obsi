using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoMinePactify.Models;
using AutoMinePactify.Services;
using AutoMinePactify.Helpers;

namespace AutoMinePactify.ViewModels;

/// <summary>
/// ViewModel pour une seule commande rapide dans la liste.
/// </summary>
public partial class QuickCommandItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "Commande";

    [ObservableProperty]
    private string _key = "F7";

    [ObservableProperty]
    private string _command = "/feed";

    [ObservableProperty]
    private QuickCommandSpeed _speed = QuickCommandSpeed.Fast;

    [ObservableProperty]
    private bool _enabled = true;

    // ─── Key capture ──────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isCapturing;

    [ObservableProperty]
    private string _keyDisplayText = "F7";

    private CancellationTokenSource? _captureCts;

    partial void OnKeyChanged(string value)
    {
        KeyDisplayText = string.IsNullOrWhiteSpace(value) ? "Aucune" : value;
    }

    /// <summary>
    /// Lance la capture de touche. L'utilisateur appuie sur une touche/souris
    /// et elle est enregistree comme keybind.
    /// </summary>
    [RelayCommand]
    private async Task CaptureKey()
    {
        if (IsCapturing)
        {
            // Annuler la capture en cours
            _captureCts?.Cancel();
            return;
        }

        IsCapturing = true;
        KeyDisplayText = "Appuie sur une touche...";

        _captureCts = new CancellationTokenSource();
        var ct = _captureCts.Token;

        try
        {
            // Attendre que toutes les touches soient relachees d'abord
            await WaitAllKeysReleased(ct);

            // Puis attendre une nouvelle pression
            string? captured = await WaitForKeyPress(ct);

            if (captured != null)
            {
                Key = captured;
            }
        }
        catch (OperationCanceledException)
        {
            // Capture annulee
        }
        finally
        {
            IsCapturing = false;
            KeyDisplayText = string.IsNullOrWhiteSpace(Key) ? "Aucune" : Key;
            _captureCts?.Dispose();
            _captureCts = null;
        }
    }

    private static async Task WaitAllKeysReleased(CancellationToken ct)
    {
        // Attendre max 2 secondes que tout soit relache
        for (int i = 0; i < 80; i++)
        {
            ct.ThrowIfCancellationRequested();
            bool anyDown = HotkeyService.IsShiftDown() ||
                           HotkeyService.IsCtrlDown() ||
                           HotkeyService.IsAltDown() ||
                           (NativeMethods.GetAsyncKeyState(0x01) & 0x8000) != 0; // left click

            if (!anyDown)
            {
                var (vk, _) = HotkeyService.DetectPressedKey();
                if (vk == 0) return; // Tout est relache
            }
            await Task.Delay(25, ct);
        }
    }

    private static async Task<string?> WaitForKeyPress(CancellationToken ct)
    {
        // Timeout: 10 secondes
        for (int i = 0; i < 400; i++)
        {
            ct.ThrowIfCancellationRequested();

            // Detecter une touche non-modifier pressee
            var (vk, keyName) = HotkeyService.DetectPressedKey();

            if (vk > 0 && keyName != null)
            {
                // Lire les modifiers actuels
                bool shift = HotkeyService.IsShiftDown();
                bool ctrl = HotkeyService.IsCtrlDown();
                bool alt = HotkeyService.IsAltDown();

                string result = HotkeyService.ComposeKeybind(shift, ctrl, alt, keyName);
                return result;
            }

            await Task.Delay(25, ct);
        }

        return null; // Timeout
    }

    // ─── Speed helpers ───────────────────────────────────────────────

    public bool IsSpeedSlow => Speed == QuickCommandSpeed.Slow;
    public bool IsSpeedNormal => Speed == QuickCommandSpeed.Normal;
    public bool IsSpeedFast => Speed == QuickCommandSpeed.Fast;
    public bool IsSpeedUltra => Speed == QuickCommandSpeed.Ultra;

    public string SpeedLabel => Speed switch
    {
        QuickCommandSpeed.Slow => "Lent",
        QuickCommandSpeed.Normal => "Normal",
        QuickCommandSpeed.Fast => "Rapide",
        QuickCommandSpeed.Ultra => "Ultra",
        _ => "?"
    };

    partial void OnSpeedChanged(QuickCommandSpeed value)
    {
        OnPropertyChanged(nameof(IsSpeedSlow));
        OnPropertyChanged(nameof(IsSpeedNormal));
        OnPropertyChanged(nameof(IsSpeedFast));
        OnPropertyChanged(nameof(IsSpeedUltra));
        OnPropertyChanged(nameof(SpeedLabel));
    }

    [RelayCommand]
    private void SetSpeed(string s)
    {
        Speed = s switch
        {
            "0" => QuickCommandSpeed.Slow,
            "1" => QuickCommandSpeed.Normal,
            "2" => QuickCommandSpeed.Fast,
            "3" => QuickCommandSpeed.Ultra,
            _ => QuickCommandSpeed.Fast
        };
    }

    // ─── Conversion ─────────────────────────────────────────────────

    public QuickCommandEntry ToEntry() => new()
    {
        Name = Name,
        Key = Key,
        Command = Command,
        Speed = Speed,
        Enabled = Enabled
    };

    public static QuickCommandItemViewModel FromEntry(QuickCommandEntry entry) => new()
    {
        Name = entry.Name,
        Key = entry.Key,
        KeyDisplayText = string.IsNullOrWhiteSpace(entry.Key) ? "Aucune" : entry.Key,
        Command = entry.Command,
        Speed = entry.Speed,
        Enabled = entry.Enabled
    };
}
