using AutoMinePactify.Models;

namespace AutoMinePactify.Services;

/// <summary>
/// Detecte les pseudos de joueurs a l'ecran.
/// Les pseudos Minecraft sont du texte blanc vif sur un fond sombre semi-transparent,
/// affiches au-dessus de la tete des joueurs.
/// On scanne la moitie haute de l'ecran pour trouver des lignes horizontales
/// de pixels blancs (signe d'un pseudo visible).
/// </summary>
public class PlayerDetector
{
    // Seuil de blanc pour considerer un pixel comme faisant partie d'un pseudo
    private const int WhiteThreshold = 220;

    // Nombre minimum de pixels blancs alignes pour considerer que c'est un pseudo
    private const int MinNametageWidth = 12;

    /// <summary>
    /// Detecte si un pseudo de joueur est visible a l'ecran.
    /// Ignore le centre (c'est le viseur) et les bords (c'est l'UI).
    /// </summary>
    public bool DetectPlayerNametag(ScreenFrame frame)
    {
        if (!frame.IsValid) return false;

        int cx = frame.Width / 2;
        int cy = frame.Height / 2;

        // Zone a ignorer autour du centre (le viseur + la main du joueur)
        int centerExcludeX = 60;
        int centerExcludeY = 60;

        // Marge pour ignorer les bords (UI du jeu : barre de vie, hotbar, etc.)
        int marginX = 40;
        int marginTop = 30;

        // On scanne la moitie haute de l'ecran (les pseudos sont au dessus des tetes)
        // On saute de 3 lignes en 3 pour aller plus vite
        int scanBottom = cy - centerExcludeY; // on arrete avant le centre

        for (int y = marginTop; y < scanBottom; y += 3)
        {
            int consecutiveWhite = 0;

            for (int x = marginX; x < frame.Width - marginX; x++)
            {
                // Ignorer la zone centrale
                if (x > cx - centerExcludeX && x < cx + centerExcludeX &&
                    y > cy - centerExcludeY && y < cy + centerExcludeY)
                {
                    consecutiveWhite = 0;
                    continue;
                }

                var (r, g, b) = frame.GetPixel(x, y);

                if (r >= WhiteThreshold && g >= WhiteThreshold && b >= WhiteThreshold)
                {
                    consecutiveWhite++;
                    if (consecutiveWhite >= MinNametageWidth)
                    {
                        // Verifier que c'est bien un pseudo :
                        // les pixels au dessus et en dessous doivent etre plus sombres
                        // (fond semi-transparent du nametag)
                        if (HasDarkSurround(frame, x - MinNametageWidth / 2, y))
                            return true;
                    }
                }
                else
                {
                    consecutiveWhite = 0;
                }
            }
        }

        // Aussi scanner sous le centre (joueurs proches en dessous du viseur)
        int scanTop2 = cy + centerExcludeY;
        int scanBottom2 = (int)(frame.Height * 0.65); // pas trop bas (hotbar)

        for (int y = scanTop2; y < scanBottom2; y += 3)
        {
            int consecutiveWhite = 0;

            for (int x = marginX; x < frame.Width - marginX; x++)
            {
                var (r, g, b) = frame.GetPixel(x, y);

                if (r >= WhiteThreshold && g >= WhiteThreshold && b >= WhiteThreshold)
                {
                    consecutiveWhite++;
                    if (consecutiveWhite >= MinNametageWidth)
                    {
                        if (HasDarkSurround(frame, x - MinNametageWidth / 2, y))
                            return true;
                    }
                }
                else
                {
                    consecutiveWhite = 0;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Verifie que les pixels au dessus et en dessous d'un texte blanc
    /// sont sombres (fond semi-transparent du nametag).
    /// Cela aide a distinguer un pseudo d'un nuage blanc ou du ciel.
    /// </summary>
    private static bool HasDarkSurround(ScreenFrame frame, int x, int y)
    {
        // Verifier 3 pixels au-dessus
        int darkCount = 0;
        for (int dy = -3; dy <= -1; dy++)
        {
            int checkY = y + dy;
            if (checkY < 0 || checkY >= frame.Height) continue;
            var (r, g, b) = frame.GetPixel(x, checkY);
            if (r < 80 && g < 80 && b < 80)
                darkCount++;
        }

        // Verifier 3 pixels en-dessous
        for (int dy = 1; dy <= 3; dy++)
        {
            int checkY = y + dy;
            if (checkY < 0 || checkY >= frame.Height) continue;
            var (r, g, b) = frame.GetPixel(x, checkY);
            if (r < 80 && g < 80 && b < 80)
                darkCount++;
        }

        // Au moins 3 pixels sombres sur 6 (haut + bas)
        return darkCount >= 3;
    }
}
