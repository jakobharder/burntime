using System.Drawing.Imaging;
using System.Drawing;

namespace BurnGfxRipper;

internal static class TextureUtils
{
    public const int HEIGHT_PADDING = 16;

    public static int MakePowerOfTwo(int value)
    {
        value--;
        int i;
        for (i = 0; value != 0; i++)
            value >>= 1;
        return 1 << i;
    }

    public static int MakeMultipleOf(int value, int unit = 128)
    {
        return (((value - 1) / unit) + 1) * unit;
    }

    public static void Save(Bitmap bmp, string name, CommandParameter parameter = null)
    {
        if (parameter is null || !parameter.RatioCorrection)
        {
            bmp.Save(name);
        }
        else
        {
            Bitmap bmp2 = new Bitmap(bmp.Width * 2, bmp.Height * 2, PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bmp2);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            g.DrawImage(bmp, new Rectangle(0, 0, bmp2.Width, bmp2.Height), -0.5f, -0.5f, bmp.Width, bmp.Height, GraphicsUnit.Pixel);

            bmp2.Save(name);
        }
    }

    public static void Save(Bitmap baseImage, Bitmap progressiveImage, string name, CommandParameter parameter = null)
    {
        Bitmap resultImage = new(baseImage.Width, baseImage.Height, PixelFormat.Format32bppArgb);
        Graphics g = Graphics.FromImage(resultImage);
        g.DrawImage(baseImage, new Rectangle(0, 0, resultImage.Width, resultImage.Height), 0, 0, baseImage.Width, baseImage.Height, GraphicsUnit.Pixel);
        g.DrawImage(progressiveImage, new Rectangle(0, 0, resultImage.Width, resultImage.Height), 0, 0, baseImage.Width, baseImage.Height, GraphicsUnit.Pixel);

        resultImage.Save(name);
    }
}
