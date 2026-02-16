using AutoMinePactify.Models;

namespace AutoMinePactify.Services;

/// <summary>
/// Verifie si la pioche est encore presente dans la hotbar en analysant les pixels.
/// La hotbar est en bas au centre de l'ecran.
/// Un slot vide a des pixels gris/sombres uniformes.
/// Un slot avec un item a des pixels plus varies et plus clairs.
/// </summary>
public class PickaxeChecker
{
    // Dimensions approximatives de la hotbar en pixels (pour 1080p)
    // La hotbar fait environ 364 pixels de large, chaque slot ~40px
    // On ajuste proportionnellement a la resolution

    /// <summary>
    /// Verifie si le slot donne (1-9) contient un item (pioche).
    /// Retourne true si le slot semble contenir un item, false si vide.
    /// </summary>
    public bool IsPickaxePresent(ScreenFrame frame, int slot)
    {
        if (!frame.IsValid || slot < 1 || slot > 9) return true; // en cas de doute, on dit oui

        // La hotbar est centree en bas de l'ecran
        // Largeur totale hotbar ~ 33.7% de la largeur de l'ecran (pour le scaling standard)
        // Chaque slot ~ 3.7% de la largeur
        double hotbarWidthRatio = 0.337;
        double slotWidthRatio = hotbarWidthRatio / 9.0;

        int hotbarTotalWidth = (int)(frame.Width * hotbarWidthRatio);
        int slotWidth = (int)(frame.Width * slotWidthRatio);

        int hotbarLeft = (frame.Width - hotbarTotalWidth) / 2;
        int hotbarBottom = frame.Height - (int)(frame.Height * 0.02); // 2% du bas
        int slotHeight = slotWidth; // les slots sont carres

        // Position du slot demande
        int slotX = hotbarLeft + (slot - 1) * slotWidth + slotWidth / 2;
        int slotY = hotbarBottom - slotHeight / 2;

        // Echantillonner une zone 8x8 au centre du slot
        int sampleSize = 4;
        int colorVariance = 0;
        int totalBrightness = 0;
        int count = 0;

        int prevR = -1, prevG = -1, prevB = -1;

        for (int dy = -sampleSize; dy <= sampleSize; dy += 2)
        {
            for (int dx = -sampleSize; dx <= sampleSize; dx += 2)
            {
                int px = slotX + dx;
                int py = slotY + dy;
                if (px < 0 || px >= frame.Width || py < 0 || py >= frame.Height)
                    continue;

                var (r, g, b) = frame.GetPixel(px, py);
                totalBrightness += r + g + b;
                count++;

                if (prevR >= 0)
                {
                    colorVariance += System.Math.Abs(r - prevR) +
                                     System.Math.Abs(g - prevG) +
                                     System.Math.Abs(b - prevB);
                }
                prevR = r;
                prevG = g;
                prevB = b;
            }
        }

        if (count == 0) return true;

        int avgBrightness = totalBrightness / count;
        int avgVariance = colorVariance / System.Math.Max(1, count - 1);

        // Un slot vide a une faible variance et une luminosite specifique (fond gris ~50-80)
        // Un slot avec item a plus de variance de couleurs
        // Si la variance est tres faible ET la luminosite est dans la zone "gris vide", c'est vide
        bool looksEmpty = avgVariance < 15 && avgBrightness < 300;

        return !looksEmpty;
    }
}
