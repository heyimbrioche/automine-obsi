using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AutoMinePactify.Helpers;
using AutoMinePactify.Models;

namespace AutoMinePactify.Services;

/// <summary>
/// Simulates keyboard and mouse inputs via Windows SendInput API.
/// All methods include humanized delays when enabled.
/// </summary>
public class InputSimulator
{
    private readonly Random _random = new();

    public bool Humanize { get; set; } = true;

    // ─── Delay helpers ──────────────────────────────────────────────

    private int Jitter(int baseMs)
    {
        if (!Humanize || baseMs <= 0) return baseMs;
        int variation = Math.Max(1, (int)(baseMs * 0.25));
        return baseMs + _random.Next(-variation, variation);
    }

    private Task Pause(int ms, CancellationToken ct) => Task.Delay(Jitter(ms), ct);

    // ─── Low-level keyboard ─────────────────────────────────────────

    public async Task KeyDown(ushort vkCode, CancellationToken ct = default)
    {
        var input = CreateKeyInput(vkCode, NativeMethods.KEYEVENTF_KEYDOWN);
        NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<NativeMethods.INPUT>());
        await Pause(15, ct);
    }

    public async Task KeyUp(ushort vkCode, CancellationToken ct = default)
    {
        var input = CreateKeyInput(vkCode, NativeMethods.KEYEVENTF_KEYUP);
        NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<NativeMethods.INPUT>());
        await Pause(15, ct);
    }

    public async Task KeyPress(ushort vkCode, int durationMs = 80, CancellationToken ct = default)
    {
        await KeyDown(vkCode, ct);
        await Pause(durationMs, ct);
        await KeyUp(vkCode, ct);
    }

    // ─── Low-level mouse ────────────────────────────────────────────

    public async Task MouseLeftDown(CancellationToken ct = default)
    {
        var input = CreateMouseInput(0, 0, NativeMethods.MOUSEEVENTF_LEFTDOWN);
        NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<NativeMethods.INPUT>());
        await Pause(15, ct);
    }

    public async Task MouseLeftUp(CancellationToken ct = default)
    {
        var input = CreateMouseInput(0, 0, NativeMethods.MOUSEEVENTF_LEFTUP);
        NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<NativeMethods.INPUT>());
        await Pause(15, ct);
    }

    public async Task MouseRightClick(CancellationToken ct = default)
    {
        var down = CreateMouseInput(0, 0, NativeMethods.MOUSEEVENTF_RIGHTDOWN);
        NativeMethods.SendInput(1, new[] { down }, Marshal.SizeOf<NativeMethods.INPUT>());
        await Pause(50, ct);

        var up = CreateMouseInput(0, 0, NativeMethods.MOUSEEVENTF_RIGHTUP);
        NativeMethods.SendInput(1, new[] { up }, Marshal.SizeOf<NativeMethods.INPUT>());
        await Pause(30, ct);
    }

    public async Task MouseMove(int dx, int dy, CancellationToken ct = default)
    {
        var input = CreateMouseInput(dx, dy, NativeMethods.MOUSEEVENTF_MOVE);
        NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<NativeMethods.INPUT>());
        await Pause(20, ct);
    }

    // ─── High-level Minecraft actions ───────────────────────────────

    /// <summary>Hold left click to mine a block for the given duration.</summary>
    public async Task MineBlock(int durationMs = 1500, CancellationToken ct = default)
    {
        await MouseLeftDown(ct);
        await Pause(durationMs, ct);
        await MouseLeftUp(ct);
        await Pause(50, ct);
    }

    /// <summary>Select a hotbar slot (1-9).</summary>
    public async Task SelectSlot(int slot, CancellationToken ct = default)
    {
        if (slot < 1 || slot > 9) return;
        ushort vk = (ushort)(NativeMethods.VK_1 + (slot - 1));
        await KeyPress(vk, 50, ct);
    }

    /// <summary>Walk forward for a duration (hold W).</summary>
    public async Task MoveForward(int durationMs = 380, CancellationToken ct = default)
    {
        await KeyDown(NativeMethods.VK_W, ct);
        await Pause(durationMs, ct);
        await KeyUp(NativeMethods.VK_W, ct);
    }

    /// <summary>Walk backward for a duration (hold S).</summary>
    public async Task MoveBackward(int durationMs = 380, CancellationToken ct = default)
    {
        await KeyDown(NativeMethods.VK_S, ct);
        await Pause(durationMs, ct);
        await KeyUp(NativeMethods.VK_S, ct);
    }

    /// <summary>Strafe left (hold A).</summary>
    public async Task MoveLeft(int durationMs = 380, CancellationToken ct = default)
    {
        await KeyDown(NativeMethods.VK_A, ct);
        await Pause(durationMs, ct);
        await KeyUp(NativeMethods.VK_A, ct);
    }

    /// <summary>Strafe right (hold D).</summary>
    public async Task MoveRight(int durationMs = 380, CancellationToken ct = default)
    {
        await KeyDown(NativeMethods.VK_D, ct);
        await Pause(durationMs, ct);
        await KeyUp(NativeMethods.VK_D, ct);
    }

    /// <summary>Sneak (hold Shift) while executing another action.</summary>
    public async Task SneakAction(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        await KeyDown(NativeMethods.VK_LSHIFT, ct);
        try
        {
            await action(ct);
        }
        finally
        {
            await KeyUp(NativeMethods.VK_LSHIFT, ct);
        }
    }

    /// <summary>Jump (press Space).</summary>
    public async Task Jump(CancellationToken ct = default)
    {
        await KeyPress(NativeMethods.VK_SPACE, 80, ct);
    }

    // ─── Movement with mode (walk/sprint/sneak) ─────────────────────

    /// <summary>
    /// Retourne le nombre de millisecondes pour parcourir 1 bloc selon le mode.
    /// Marche ~232ms, Sprint ~178ms, Accroupi ~772ms.
    /// </summary>
    public static int MsPerBlock(ColumnMoveMode mode) => mode switch
    {
        ColumnMoveMode.Walk => 232,
        ColumnMoveMode.Sprint => 178,
        ColumnMoveMode.Sneak => 772,
        _ => 232
    };

    /// <summary>
    /// Recule de <paramref name="blocks"/> blocs en maintenant le mode de deplacement.
    /// Sprint = Ctrl enfonce, Sneak = Shift enfonce, Walk = rien de plus.
    /// </summary>
    public async Task MoveBackwardWithMode(int blocks, ColumnMoveMode mode, CancellationToken ct)
    {
        if (blocks <= 0) return;
        int totalMs = blocks * MsPerBlock(mode);

        // Appuyer sur la touche modificatrice si besoin
        if (mode == ColumnMoveMode.Sprint)
            await KeyDown(NativeMethods.VK_LCONTROL, ct);
        else if (mode == ColumnMoveMode.Sneak)
            await KeyDown(NativeMethods.VK_LSHIFT, ct);

        try
        {
            await KeyDown(NativeMethods.VK_S, ct);
            await Pause(totalMs, ct);
            await KeyUp(NativeMethods.VK_S, ct);
        }
        finally
        {
            // Relacher la touche modificatrice
            if (mode == ColumnMoveMode.Sprint)
                await KeyUp(NativeMethods.VK_LCONTROL, ct);
            else if (mode == ColumnMoveMode.Sneak)
                await KeyUp(NativeMethods.VK_LSHIFT, ct);
        }
    }

    /// <summary>
    /// Strafe gauche de <paramref name="blocks"/> blocs avec le mode de deplacement.
    /// </summary>
    public async Task MoveLeftWithMode(int blocks, ColumnMoveMode mode, CancellationToken ct)
    {
        if (blocks <= 0) return;
        int totalMs = blocks * MsPerBlock(mode);

        if (mode == ColumnMoveMode.Sprint)
            await KeyDown(NativeMethods.VK_LCONTROL, ct);
        else if (mode == ColumnMoveMode.Sneak)
            await KeyDown(NativeMethods.VK_LSHIFT, ct);

        try
        {
            await KeyDown(NativeMethods.VK_A, ct);
            await Pause(totalMs, ct);
            await KeyUp(NativeMethods.VK_A, ct);
        }
        finally
        {
            if (mode == ColumnMoveMode.Sprint)
                await KeyUp(NativeMethods.VK_LCONTROL, ct);
            else if (mode == ColumnMoveMode.Sneak)
                await KeyUp(NativeMethods.VK_LSHIFT, ct);
        }
    }

    /// <summary>
    /// Strafe droite de <paramref name="blocks"/> blocs avec le mode de deplacement.
    /// </summary>
    public async Task MoveRightWithMode(int blocks, ColumnMoveMode mode, CancellationToken ct)
    {
        if (blocks <= 0) return;
        int totalMs = blocks * MsPerBlock(mode);

        if (mode == ColumnMoveMode.Sprint)
            await KeyDown(NativeMethods.VK_LCONTROL, ct);
        else if (mode == ColumnMoveMode.Sneak)
            await KeyDown(NativeMethods.VK_LSHIFT, ct);

        try
        {
            await KeyDown(NativeMethods.VK_D, ct);
            await Pause(totalMs, ct);
            await KeyUp(NativeMethods.VK_D, ct);
        }
        finally
        {
            if (mode == ColumnMoveMode.Sprint)
                await KeyUp(NativeMethods.VK_LCONTROL, ct);
            else if (mode == ColumnMoveMode.Sneak)
                await KeyUp(NativeMethods.VK_LSHIFT, ct);
        }
    }

    // ─── Camera / look ──────────────────────────────────────────────

    /// <summary>Look down by moving the mouse down.</summary>
    public async Task LookDown(int pixels = 100, CancellationToken ct = default)
    {
        int steps = Math.Max(1, pixels / 20);
        int perStep = pixels / steps;
        for (int i = 0; i < steps; i++)
        {
            await MouseMove(0, perStep, ct);
        }
    }

    /// <summary>Look up by moving the mouse up.</summary>
    public async Task LookUp(int pixels = 100, CancellationToken ct = default)
    {
        int steps = Math.Max(1, pixels / 20);
        int perStep = pixels / steps;
        for (int i = 0; i < steps; i++)
        {
            await MouseMove(0, -perStep, ct);
        }
    }

    /// <summary>Turn left ~90 degrees.</summary>
    public async Task TurnLeft(CancellationToken ct = default)
    {
        int totalPixels = 480;
        int steps = 6;
        int perStep = totalPixels / steps;
        for (int i = 0; i < steps; i++)
        {
            await MouseMove(-perStep, 0, ct);
        }
    }

    /// <summary>Turn right ~90 degrees.</summary>
    public async Task TurnRight(CancellationToken ct = default)
    {
        int totalPixels = 480;
        int steps = 6;
        int perStep = totalPixels / steps;
        for (int i = 0; i < steps; i++)
        {
            await MouseMove(perStep, 0, ct);
        }
    }

    /// <summary>Turn 180 degrees.</summary>
    public async Task TurnAround(CancellationToken ct = default)
    {
        int totalPixels = 960;
        int steps = 10;
        int perStep = totalPixels / steps;
        for (int i = 0; i < steps; i++)
        {
            await MouseMove(perStep, 0, ct);
        }
    }

    // ─── Chat commands ─────────────────────────────────────────────

    private const uint KEYEVENTF_UNICODE = 0x0004;

    /// <summary>
    /// Envoie une commande dans le chat Minecraft (ex: "/home mine").
    /// Ouvre le chat avec T, tape la commande entiere, et appuie sur Enter.
    /// </summary>
    public async Task TypeChatCommand(string command, CancellationToken ct = default)
    {
        // Toujours ouvrir le chat avec T (VK reel, compatible Minecraft/LWJGL)
        await KeyPress(NativeMethods.VK_T, 60, ct);
        await Pause(400, ct); // laisser le chat s'ouvrir

        // Taper la commande entiere (y compris le / si present)
        await TypeString(command, ct);
        await Pause(150, ct);

        // Appuyer sur Enter avec VK + scan code pour compatibilite maximale
        await PressEnter(ct);
        await Pause(200, ct);
    }

    /// <summary>
    /// Version rapide de TypeChatCommand pour les commandes rapides (hotkey).
    /// La vitesse depend du niveau choisi par l'utilisateur.
    /// </summary>
    public async Task TypeChatCommandFast(string command, QuickCommandSpeed speed = QuickCommandSpeed.Fast, CancellationToken ct = default)
    {
        var (chatOpenMs, charDelayMs, preEnterMs, postEnterMs, keyPressMs) = SpeedProfile(speed);

        // Ouvrir le chat avec T
        await KeyPress(NativeMethods.VK_T, keyPressMs, ct);
        await Task.Delay(chatOpenMs, ct);

        // Taper la commande
        foreach (char c in command)
        {
            await TypeUnicodeChar(c, ct);
            if (charDelayMs > 0) await Task.Delay(charDelayMs, ct);
        }

        if (preEnterMs > 0) await Task.Delay(preEnterMs, ct);

        // Appuyer sur Enter
        await PressEnter(ct);

        if (postEnterMs > 0) await Task.Delay(postEnterMs, ct);
    }

    /// <summary>
    /// Retourne les delais (en ms) pour chaque niveau de vitesse.
    /// (chatOpen, charDelay, preEnter, postEnter, keyPress)
    /// </summary>
    private static (int chatOpen, int charDelay, int preEnter, int postEnter, int keyPress) SpeedProfile(QuickCommandSpeed speed) => speed switch
    {
        QuickCommandSpeed.Slow   => (250, 20, 80, 100, 60),
        QuickCommandSpeed.Normal => (150, 10, 40, 60, 40),
        QuickCommandSpeed.Fast   => (80, 4, 15, 25, 30),
        QuickCommandSpeed.Ultra  => (40, 1, 5, 10, 20),
        _ => (150, 10, 40, 60, 40)
    };

    /// <summary>
    /// Appuie sur Enter de facon robuste (VK + scan code).
    /// </summary>
    private async Task PressEnter(CancellationToken ct)
    {
        ushort scanCode = (ushort)NativeMethods.MapVirtualKeyW(NativeMethods.VK_RETURN, NativeMethods.MAPVK_VK_TO_VSC);

        var down = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = NativeMethods.VK_RETURN,
                    wScan = scanCode,
                    dwFlags = NativeMethods.KEYEVENTF_KEYDOWN
                }
            }
        };
        NativeMethods.SendInput(1, new[] { down }, Marshal.SizeOf<NativeMethods.INPUT>());
        await Pause(80, ct);

        var up = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = NativeMethods.VK_RETURN,
                    wScan = scanCode,
                    dwFlags = NativeMethods.KEYEVENTF_KEYUP
                }
            }
        };
        NativeMethods.SendInput(1, new[] { up }, Marshal.SizeOf<NativeMethods.INPUT>());
        await Pause(30, ct);
    }

    /// <summary>
    /// Tape une chaine de caracteres en envoyant des evenements Unicode.
    /// </summary>
    private async Task TypeString(string text, CancellationToken ct)
    {
        foreach (char c in text)
        {
            await TypeUnicodeChar(c, ct);
            await Pause(30, ct);
        }
    }

    /// <summary>
    /// Envoie un caractere Unicode via SendInput.
    /// </summary>
    private async Task TypeUnicodeChar(char c, CancellationToken ct)
    {
        var down = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = (ushort)c,
                    dwFlags = KEYEVENTF_UNICODE
                }
            }
        };

        var up = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = (ushort)c,
                    dwFlags = KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP
                }
            }
        };

        NativeMethods.SendInput(1, new[] { down }, Marshal.SizeOf<NativeMethods.INPUT>());
        await Pause(15, ct);
        NativeMethods.SendInput(1, new[] { up }, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    // ─── Safety ─────────────────────────────────────────────────────

    /// <summary>Release all held keys and mouse buttons immediately.</summary>
    public void ReleaseAllKeys()
    {
        ushort[] keys =
        {
            NativeMethods.VK_W, NativeMethods.VK_A, NativeMethods.VK_S,
            NativeMethods.VK_D, NativeMethods.VK_SPACE, NativeMethods.VK_LSHIFT,
            NativeMethods.VK_LCONTROL
        };

        foreach (var key in keys)
        {
            var input = CreateKeyInput(key, NativeMethods.KEYEVENTF_KEYUP);
            NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<NativeMethods.INPUT>());
        }

        // Release mouse buttons
        var mouseUp = CreateMouseInput(0, 0,
            NativeMethods.MOUSEEVENTF_LEFTUP | NativeMethods.MOUSEEVENTF_RIGHTUP);
        NativeMethods.SendInput(1, new[] { mouseUp }, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private static NativeMethods.INPUT CreateKeyInput(ushort vkCode, uint flags)
    {
        // Toujours inclure le scan code pour compatibilite avec Minecraft/LWJGL
        // qui lit les scan codes hardware et pas les virtual key codes
        ushort scanCode = (ushort)NativeMethods.MapVirtualKeyW(vkCode, NativeMethods.MAPVK_VK_TO_VSC);
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = vkCode,
                    wScan = scanCode,
                    dwFlags = flags
                }
            }
        };
    }

    private static NativeMethods.INPUT CreateMouseInput(int dx, int dy, uint flags)
    {
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            u = new NativeMethods.InputUnion
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dx = dx,
                    dy = dy,
                    dwFlags = flags
                }
            }
        };
    }
}
