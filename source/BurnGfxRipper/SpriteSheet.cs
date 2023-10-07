using System.Drawing.Imaging;
using System.Drawing;
using Burntime.Platform.Resource;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.IO;
using System.Data.Common;

namespace BurnGfxRipper;

internal class SpriteSheet
{
    public Bitmap Bitmap { get; init; }

    public SpriteSheet(int width, int height)
    {
        Bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
    }

    public SpriteSheet(int width, int frameWidth, int frameHeight, int frameCount)
    {
        int columns = (int)Math.Floor(width / (float)frameWidth);
        int rows = (int)Math.Ceiling(frameCount / (float)columns);

        int rowHeight = TextureUtils.MakeMultipleOf(frameHeight, TextureUtils.HEIGHT_PADDING);
        int textureHeight = TextureUtils.MakePowerOfTwo(rows * rowHeight);

        Bitmap = new Bitmap(width, textureHeight, PixelFormat.Format32bppArgb);
    }

    public void RenderSingle(MemoryStream memory, int frameWidth, int frameHeight, int frame)
    {
        int columns = (int)Math.Floor(Bitmap.Width / (float)frameWidth);

        int x = frame % columns;
        int y = (frame - x) / columns;

        int rowHeight = TextureUtils.MakeMultipleOf(frameHeight, TextureUtils.HEIGHT_PADDING);

        BitmapData loc = Bitmap.LockBits(new Rectangle(x * frameWidth, y * rowHeight, frameWidth, frameHeight),
                    ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

        byte[] buffer = memory.ToArray();

        for (int py = 0; py < frameHeight; py++)
            Marshal.Copy(buffer,
                py * frameWidth * 4,
                loc.Scan0 + py * loc.Stride,
                frameWidth * 4);

        Bitmap.UnlockBits(loc);
    }

    public void Render(ISpriteAnimationProcessor ani, bool baseImage)
    {
        ani.SetFrame(0);
        int columns = (int)Math.Floor(Bitmap.Width / (float)ani.Size.x);
        int rows = (int)Math.Ceiling(ani.FrameCount / (float)columns);

        int rowHeight = TextureUtils.MakeMultipleOf(ani.Size.y, TextureUtils.HEIGHT_PADDING);

        int i = 0;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns && i < ani.FrameCount; x++)
            {
                ani.SetFrame(baseImage ? 0 : i);

                BitmapData loc = Bitmap.LockBits(new Rectangle(x * ani.FrameSize.x, y * rowHeight, ani.FrameSize.x, ani.FrameSize.y),
                    ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                System.IO.MemoryStream mem = new();
                ani.Render(mem, ani.FrameSize.x * 4);

                byte[] buffer = mem.ToArray();

                for (int py = 0; py < ani.FrameSize.y; py++)
                    Marshal.Copy(buffer,
                        py * ani.FrameSize.x * 4,
                        loc.Scan0 + py * loc.Stride,
                        ani.FrameSize.x * 4);

                Bitmap.UnlockBits(loc);

                i++;
            }
        }
        
    }
}
