﻿using System.Drawing.Imaging;
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
    int _padding;

    public SpriteSheet(int width, int height)
    {
        Bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        _padding = 0;
    }

    public SpriteSheet(int width, int frameWidth, int frameHeight, int frameCount, int padding = 0)
    {
        _padding = padding;

        int columns = (int)Math.Floor(width / (float)(frameWidth + _padding * 2));
        int rows = (int)Math.Ceiling(frameCount / (float)columns);

        int rowHeight = TextureUtils.MakeMultipleOf(frameHeight, TextureUtils.HEIGHT_PADDING) + _padding * 2;
        int textureHeight = TextureUtils.MakePowerOfTwo(rows * rowHeight);

        Bitmap = new Bitmap(width, textureHeight, PixelFormat.Format32bppArgb);
    }

    public void RenderSingle(MemoryStream memory, int frameWidth, int frameHeight, int frame)
    {
        int columns = (int)Math.Floor(Bitmap.Width / (float)(frameWidth + _padding * 2));

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

    public void Render(ISpriteAnimationProcessor ani, bool nonProgressive, int heightPadding = TextureUtils.HEIGHT_PADDING, int startFrame = 0, int frameCount = 0)
    {
        ani.SetFrame(startFrame);
        frameCount = frameCount == 0 ? (ani.FrameCount - startFrame - 1) : System.Math.Min(frameCount, (ani.FrameCount - startFrame - 1));

        int columns = (int)Math.Floor(Bitmap.Width / (float)(ani.Size.x + _padding * 2));
        int rows = (int)Math.Ceiling(frameCount / (float)columns);

        int rowHeight = TextureUtils.MakeMultipleOf(ani.Size.y, heightPadding) + _padding * 2;
        int columnWidth = ani.FrameSize.x + _padding * 2;

        int frame = startFrame;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns && frame < frameCount; x++)
            {
                ani.SetFrame(frame);

                BitmapData loc = Bitmap.LockBits(new Rectangle(x * columnWidth, y * rowHeight, ani.FrameSize.x, ani.FrameSize.y),
                    ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                System.IO.MemoryStream mem = new();

                if (nonProgressive)
                {
                    for (int baseFrame = startFrame; baseFrame < frame; baseFrame++)
                    {
                        ani.SetFrame(baseFrame);
                        ani.Render(mem, ani.FrameSize.x * 4);
                    }
                }

                ani.SetFrame(frame);
                ani.Render(mem, ani.FrameSize.x * 4);

                byte[] buffer = mem.ToArray();
                for (int py = 0; py < ani.FrameSize.y; py++)
                    Marshal.Copy(buffer,
                        py * ani.FrameSize.x * 4,
                        loc.Scan0 + py * loc.Stride,
                        ani.FrameSize.x * 4);
                Bitmap.UnlockBits(loc);

                frame++;
            }
        }
        
    }
}
