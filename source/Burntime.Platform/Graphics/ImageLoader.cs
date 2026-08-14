using StbImageSharp;

namespace Burntime.Platform.Graphics;

internal readonly record struct DecodedImage(int Width, int Height, byte[] BgraData);

internal static class ImageLoader
{
    public static DecodedImage LoadBgra(Stream stream)
    {
        ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        byte[] bgra = new byte[image.Data.Length];

        for (int i = 0; i < image.Data.Length; i += 4)
        {
            bgra[i] = image.Data[i + 2];
            bgra[i + 1] = image.Data[i + 1];
            bgra[i + 2] = image.Data[i];
            bgra[i + 3] = image.Data[i + 3];
        }

        return new DecodedImage(image.Width, image.Height, bgra);
    }
}
