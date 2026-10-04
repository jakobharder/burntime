namespace Burntime.Platform.Graphics;

/// <summary>Recolors strongly green clothing in decoded RGBA pixels.</summary>
public static class GreenClothingRecolor
{
    public static void Apply(byte[] rgba, uint targetRgb)
    {
        int targetR = (int)((targetRgb >> 16) & 255);
        int targetG = (int)((targetRgb >> 8) & 255);
        int targetB = (int)(targetRgb & 255);
        for (int i = 0; i + 3 < rgba.Length; i += 4)
        {
            int green = rgba[i + 1];
            if (rgba[i + 3] == 0 || green * 10 <= rgba[i] * 13 ||
                green * 10 <= rgba[i + 2] * 13)
                continue;

            rgba[i] = (byte)((green * targetR + 127) / 255);
            rgba[i + 1] = (byte)((green * targetG + 127) / 255);
            rgba[i + 2] = (byte)((green * targetB + 127) / 255);
        }
    }
}
