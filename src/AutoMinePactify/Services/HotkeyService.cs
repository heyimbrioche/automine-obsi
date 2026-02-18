using System;
using System.Collections.Generic;
using System.Linq;
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

    // ─── VK constants ─────────────────────────────────────────────────

    public const int VK_LSHIFT = 0xA0;
    public const int VK_RSHIFT = 0xA1;
    public const int VK_LCONTROL = 0xA2;
    public const int VK_RCONTROL = 0xA3;
    public const int VK_LALT = 0xA4;
    public const int VK_RALT = 0xA5;
    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_ALT = 0x12;

    public const int VK_LBUTTON = 0x01;
    public const int VK_RBUTTON = 0x02;
    public const int VK_MBUTTON = 0x04;
    public const int VK_XBUTTON1 = 0x05;
    public const int VK_XBUTTON2 = 0x06;

    // ─── Key name ↔ VK code mapping (bidirectional) ───────────────────

    private static readonly Dictionary<string, int> KeyNameToVk = new(StringComparer.OrdinalIgnoreCase)
    {
        // Function keys
        { "F1", 0x70 }, { "F2", 0x71 }, { "F3", 0x72 }, { "F4", 0x73 },
        { "F5", 0x74 }, { "F6", 0x75 }, { "F7", 0x76 }, { "F8", 0x77 },
        { "F9", 0x78 }, { "F10", 0x79 }, { "F11", 0x7A }, { "F12", 0x7B },

        // Letters
        { "A", 0x41 }, { "B", 0x42 }, { "C", 0x43 }, { "D", 0x44 },
        { "E", 0x45 }, { "F", 0x46 }, { "G", 0x47 }, { "H", 0x48 },
        { "I", 0x49 }, { "J", 0x4A }, { "K", 0x4B }, { "L", 0x4C },
        { "M", 0x4D }, { "N", 0x4E }, { "O", 0x4F }, { "P", 0x50 },
        { "Q", 0x51 }, { "R", 0x52 }, { "S", 0x53 }, { "T", 0x54 },
        { "U", 0x55 }, { "V", 0x56 }, { "W", 0x57 }, { "X", 0x58 },
        { "Y", 0x59 }, { "Z", 0x5A },

        // Numbers
        { "0", 0x30 }, { "1", 0x31 }, { "2", 0x32 }, { "3", 0x33 },
        { "4", 0x34 }, { "5", 0x35 }, { "6", 0x36 }, { "7", 0x37 },
        { "8", 0x38 }, { "9", 0x39 },

        // Numpad
        { "Num0", 0x60 }, { "Num1", 0x61 }, { "Num2", 0x62 }, { "Num3", 0x63 },
        { "Num4", 0x64 }, { "Num5", 0x65 }, { "Num6", 0x66 }, { "Num7", 0x67 },
        { "Num8", 0x68 }, { "Num9", 0x69 },
        { "Num*", 0x6A }, { "Num+", 0x6B }, { "Num-", 0x6D }, { "Num.", 0x6E }, { "Num/", 0x6F },

        // Mouse buttons
        { "Mouse4", 0x05 }, { "Mouse5", 0x06 },
        { "MouseMiddle", 0x04 },

        // Special keys
        { "Space", 0x20 }, { "Enter", 0x0D }, { "Tab", 0x09 },
        { "Escape", 0x1B }, { "Backspace", 0x08 }, { "Delete", 0x2E },
        { "Insert", 0x2D }, { "Home", 0x24 }, { "End", 0x23 },
        { "PageUp", 0x21 }, { "PageDown", 0x22 },
        { "Up", 0xAD }, { "Down", 0xAE }, { "Left", 0x25 }, { "Right", 0x27 },
        { "CapsLock", 0x14 }, { "NumLock", 0x90 }, { "ScrollLock", 0x91 },
        { "PrintScreen", 0x2C }, { "Pause", 0x13 },

        // Punctuation
        { ";", 0xBA }, { "=", 0xBB }, { ",", 0xBC }, { "-", 0xBD },
        { ".", 0xBE }, { "/", 0xBF }, { "`", 0xC0 },
        { "[", 0xDB }, { "\\", 0xDC }, { "]", 0xDD }, { "'", 0xDE },
    };

    private static readonly Dictionary<int, string> VkToKeyName;

    static HotkeyService()
    {
        VkToKeyName = new Dictionary<int, string>();
        foreach (var kvp in KeyNameToVk)
        {
            // Premier nom trouvé pour chaque VK (priorité au premier)
            if (!VkToKeyName.ContainsKey(kvp.Value))
                VkToKeyName[kvp.Value] = kvp.Key;
        }
    }

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
    /// Convertit un VK code en nom de touche affichable.
    /// Retourne null si non reconnu.
    /// </summary>
    public static string? VkCodeToKeyName(int vk)
    {
        return VkToKeyName.TryGetValue(vk, out string? name) ? name : null;
    }

    /// <summary>
    /// Retourne la liste des noms de touches disponibles.
    /// </summary>
    public static IReadOnlyCollection<string> AvailableKeys => KeyNameToVk.Keys;

    // ─── Modifier helpers ────────────────────────────────────────────

    /// <summary>VK codes considered as modifiers (not standalone keys for capture).</summary>
    private static readonly HashSet<int> ModifierVks = new()
    {
        VK_SHIFT, VK_CONTROL, VK_ALT,
        VK_LSHIFT, VK_RSHIFT, VK_LCONTROL, VK_RCONTROL, VK_LALT, VK_RALT
    };

    public static bool IsModifier(int vk) => ModifierVks.Contains(vk);

    public static bool IsShiftDown() =>
        (NativeMethods.GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;

    public static bool IsCtrlDown() =>
        (NativeMethods.GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

    public static bool IsAltDown() =>
        (NativeMethods.GetAsyncKeyState(VK_ALT) & 0x8000) != 0;

    /// <summary>
    /// Parse un keybind string comme "Shift+P" ou "Ctrl+Shift+F7" en (modifiers, mainVk).
    /// </summary>
    public static (bool shift, bool ctrl, bool alt, int mainVk) ParseKeybind(string keybind)
    {
        if (string.IsNullOrWhiteSpace(keybind))
            return (false, false, false, 0);

        bool shift = false, ctrl = false, alt = false;
        string[] parts = keybind.Split('+');
        string mainKey = parts[^1].Trim();

        for (int i = 0; i < parts.Length - 1; i++)
        {
            string mod = parts[i].Trim();
            if (mod.Equals("Shift", StringComparison.OrdinalIgnoreCase)) shift = true;
            else if (mod.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) ctrl = true;
            else if (mod.Equals("Alt", StringComparison.OrdinalIgnoreCase)) alt = true;
        }

        int vk = KeyNameToVkCode(mainKey);
        return (shift, ctrl, alt, vk);
    }

    /// <summary>
    /// Compose un keybind string depuis les composants.
    /// </summary>
    public static string ComposeKeybind(bool shift, bool ctrl, bool alt, string keyName)
    {
        var parts = new List<string>();
        if (ctrl) parts.Add("Ctrl");
        if (shift) parts.Add("Shift");
        if (alt) parts.Add("Alt");
        parts.Add(keyName);
        return string.Join("+", parts);
    }

    /// <summary>
    /// Scan all known VK codes and return the first non-modifier key that is currently pressed.
    /// Returns (vk, keyName) or (0, null) if nothing pressed.
    /// </summary>
    public static (int vk, string? keyName) DetectPressedKey()
    {
        foreach (var kvp in VkToKeyName)
        {
            int vk = kvp.Key;
            if (IsModifier(vk)) continue;
            if ((NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0)
                return (vk, kvp.Value);
        }
        return (0, null);
    }

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
