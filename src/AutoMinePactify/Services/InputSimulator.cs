using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AutoMinePactify.Helpers;

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
    /// Ouvre le chat avec '/', tape la commande, et appuie sur Enter.
    /// </summary>
    public async Task TypeChatCommand(string command, CancellationToken ct = default)
    {
        // Si la commande commence par /, on ouvre le chat avec / directement
        // Sinon on ouvre avec T
        if (command.StartsWith("/"))
        {
            // Appuyer sur / ouvre le chat avec / deja ecrit en 1.8.8
            await TypeUnicodeChar('/', ct);
            await Pause(300, ct);
            // Taper le reste (sans le /)
            await TypeString(command.Substring(1), ct);
        }
        else
        {
            await KeyPress(0x54, 50, ct); // VK_T pour ouvrir le chat
            await Pause(300, ct);
            await TypeString(command, ct);
        }

        await Pause(100, ct);

        // Appuyer sur Enter pour envoyer
        await KeyPress(0x0D, 50, ct); // VK_RETURN
        await Pause(100, ct);
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
            NativeMethods.VK_D, NativeMethods.VK_SPACE, NativeMethods.VK_LSHIFT
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
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = vkCode,
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
