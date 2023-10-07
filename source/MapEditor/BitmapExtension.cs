using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MapEditor;

internal static class BitmapExtensions
{
    public static void Save8Bit(this Bitmap bitmap, string filePath, Bitmap paletteReference)
    {
        var output = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format8bppIndexed);

        var colors = (paletteReference ?? bitmap).ScanPalette();

        var palette = output.Palette;
        foreach (var color in colors)
            palette.Entries[color.Value] = color.Key;
        output.Palette = palette;

        BitmapData data = output.LockBits(new Rectangle(0, 0, output.Width, output.Height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);

        byte[] bytes = new byte[data.Height * data.Stride];

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var c = bitmap.GetPixel(x, y);
                if (colors.TryGetValue(c, out byte rgb))
                    bytes[y * data.Stride + x] = rgb;
            }
        }

        Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        output.UnlockBits(data);

        output.Save(filePath);
    }

    public static Image EnsureProperScale(this Image image, int tileWidth, int tileHeight)
    {
        int expectedWidth = tileWidth * 64;
        int expectedHeight = tileHeight * 76;

        if (image.Width == expectedWidth && image.Height == expectedHeight) return image;

        Bitmap scaled = new(expectedWidth, expectedHeight, PixelFormat.Format24bppRgb);
        Graphics g = Graphics.FromImage(scaled);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        g.DrawImage(image, new Rectangle(0, 0, expectedWidth, expectedHeight), new Rectangle(0, 0, image.Width, image.Height), GraphicsUnit.Pixel);

        image.Dispose();
        return scaled;
    }

    public static Image ExtractTile(this Image image, int x, int y, int width, int height)
    {
        var tileImage = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        Graphics g = Graphics.FromImage(tileImage);
        g.DrawImage(image, new Rectangle(0, 0, width, height),
            new Rectangle(x * width, y * height, width, height), GraphicsUnit.Pixel);
        return tileImage;
    }

    private static Dictionary<Color, byte> ScanPalette(this Bitmap bitmap)
    {
        Dictionary<Color, byte> colors = new();

        int colorIndex = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width && colorIndex < 256; x++)
            {
                Color c = bitmap.GetPixel(x, y);
                if (!colors.ContainsKey(c))
                {
                    colors.Add(c, (byte)colorIndex);
                    colorIndex++;
                }
            }
        }

        return colors;
    }
}
