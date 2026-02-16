namespace AutoMinePactify.Models;

/// <summary>
/// Contient les pixels captures de la fenetre Minecraft.
/// Les pixels sont en format BGRA (4 octets par pixel).
/// L'index du pixel (x,y) = (y * Width + x) * 4
/// BGRA : [0]=Blue, [1]=Green, [2]=Red, [3]=Alpha
/// Note : les lignes sont inversees (la premiere ligne du tableau = le bas de l'image).
/// </summary>
public readonly struct ScreenFrame
{
    public readonly byte[] Pixels;
    public readonly int Width;
    public readonly int Height;

    public ScreenFrame(byte[] pixels, int width, int height)
    {
        Pixels = pixels;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Retourne la couleur (R, G, B) du pixel aux coordonnees ecran (x, y).
    /// (0,0) = coin haut-gauche de la fenetre.
    /// </summary>
    public (byte R, byte G, byte B) GetPixel(int x, int y)
    {
        // Les lignes sont inversees dans le buffer DIB (bottom-up)
        int flippedY = Height - 1 - y;
        int idx = (flippedY * Width + x) * 4;
        if (idx < 0 || idx + 2 >= Pixels.Length)
            return (0, 0, 0);
        return (Pixels[idx + 2], Pixels[idx + 1], Pixels[idx]);
    }

    public bool IsValid => Pixels != null && Pixels.Length > 0 && Width > 0 && Height > 0;
}
