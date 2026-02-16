using System;
using AutoMinePactify.Models;

namespace AutoMinePactify.Services;

/// <summary>
/// Detecte l'obsidienne a l'ecran en analysant la couleur des pixels.
/// Supporte la calibration : le joueur pointe sur de l'obsidienne,
/// on memorise la couleur exacte, puis on cherche les pixels similaires.
/// </summary>
public class ObsidianDetector
{
    // Couleur de reference (par defaut = obsidienne vanilla)
    private byte _refR = 20;
    private byte _refG = 15;
    private byte _refB = 35;
    private int _tolerance = 25;

    public bool IsCalibrated { get; private set; }

    public byte RefR => _refR;
    public byte RefG => _refG;
    public byte RefB => _refB;

    /// <summary>
    /// Calibre la couleur de l'obsidienne en lisant les pixels au centre de l'ecran.
    /// Le joueur doit pointer sur de l'obsidienne avant d'appeler cette methode.
    /// </summary>
    public bool Calibrate(ScreenFrame frame)
    {
        if (!frame.IsValid) return false;

        int cx = frame.Width / 2;
        int cy = frame.Height / 2;

        // Moyenne des pixels dans une zone 7x7 autour du centre
        int totalR = 0, totalG = 0, totalB = 0, count = 0;
        for (int dy = -3; dy <= 3; dy++)
        {
            for (int dx = -3; dx <= 3; dx++)
            {
                int px = cx + dx;
                int py = cy + dy;
                if (px < 0 || px >= frame.Width || py < 0 || py >= frame.Height)
                    continue;
                var (r, g, b) = frame.GetPixel(px, py);
                totalR += r;
                totalG += g;
                totalB += b;
                count++;
            }
        }

        if (count == 0) return false;

        _refR = (byte)(totalR / count);
        _refG = (byte)(totalG / count);
        _refB = (byte)(totalB / count);
        IsCalibrated = true;
        return true;
    }

    /// <summary>
    /// Verifie si la couleur donnee ressemble a de l'obsidienne.
    /// </summary>
    public bool IsObsidianColor(byte r, byte g, byte b)
    {
        int dr = Math.Abs(r - _refR);
        int dg = Math.Abs(g - _refG);
        int db = Math.Abs(b - _refB);
        return dr <= _tolerance && dg <= _tolerance && db <= _tolerance;
    }

    /// <summary>
    /// Verifie si le viseur (centre de l'ecran) pointe sur de l'obsidienne.
    /// Teste une zone 5x5 autour du centre. Retourne true si au moins
    /// la moitie des pixels matchent.
    /// </summary>
    public bool CheckCrosshair(ScreenFrame frame)
    {
        if (!frame.IsValid) return false;

        int cx = frame.Width / 2;
        int cy = frame.Height / 2;
        int matches = 0;
        int total = 0;

        for (int dy = -2; dy <= 2; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                // Ignorer le pixel exact du centre (c'est la croix du viseur)
                if (dx == 0 && dy == 0) continue;

                int px = cx + dx;
                int py = cy + dy;
                if (px < 0 || px >= frame.Width || py < 0 || py >= frame.Height)
                    continue;

                var (r, g, b) = frame.GetPixel(px, py);
                total++;
                if (IsObsidianColor(r, g, b))
                    matches++;
            }
        }

        return total > 0 && matches >= total / 2;
    }

    /// <summary>
    /// Scanne l'ecran en spirale depuis le centre pour trouver un bloc d'obsidienne.
    /// Retourne l'offset (dx, dy) par rapport au centre, ou null si rien trouve.
    /// Le scan saute de 8 pixels a chaque pas pour aller plus vite
    /// (un bloc Minecraft fait environ 16-40 pixels selon la distance).
    /// </summary>
    public (int dx, int dy)? ScanForObsidian(ScreenFrame frame, int maxRadius = 300)
    {
        if (!frame.IsValid) return null;

        int cx = frame.Width / 2;
        int cy = frame.Height / 2;
        int step = 8; // sauter de 8 pixels

        // Scan en spirale carree
        for (int radius = step; radius <= maxRadius; radius += step)
        {
            // Cote haut
            for (int dx = -radius; dx <= radius; dx += step)
            {
                if (CheckPixelGroup(frame, cx + dx, cy - radius))
                    return (dx, -radius);
            }
            // Cote droit
            for (int dy = -radius; dy <= radius; dy += step)
            {
                if (CheckPixelGroup(frame, cx + radius, cy + dy))
                    return (radius, dy);
            }
            // Cote bas
            for (int dx = radius; dx >= -radius; dx -= step)
            {
                if (CheckPixelGroup(frame, cx + dx, cy + radius))
                    return (dx, radius);
            }
            // Cote gauche
            for (int dy = radius; dy >= -radius; dy -= step)
            {
                if (CheckPixelGroup(frame, cx - radius, cy + dy))
                    return (-radius, dy);
            }
        }

        return null;
    }

    /// <summary>
    /// Verifie un petit groupe de pixels (3x3) pour confirmer que c'est de l'obsidienne
    /// et pas juste un pixel isole.
    /// </summary>
    private bool CheckPixelGroup(ScreenFrame frame, int x, int y)
    {
        if (x < 2 || x >= frame.Width - 2 || y < 2 || y >= frame.Height - 2)
            return false;

        int matches = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                var (r, g, b) = frame.GetPixel(x + dx, y + dy);
                if (IsObsidianColor(r, g, b))
                    matches++;
            }
        }
        // Au moins 5 sur 9 pixels doivent matcher
        return matches >= 5;
    }
}
