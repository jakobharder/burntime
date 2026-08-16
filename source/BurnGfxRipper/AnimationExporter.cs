using Burntime.Data.BurnGfx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if WINDOWS
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
#endif

namespace BurnGfxRipper;

class AnimationExporter
{
    public void Export(string file, string dir, CommandParameter parameter)
    {
        ExportAsSeparateFiles(file, dir, parameter);

        // Sprite-sheet modes are retained below for the Windows/System.Drawing build.
        // if (!parameter.MegaTexture) ExportAsSpriteSheet(file, dir, parameter);
        // else ExportAsSingleFile(file, dir, parameter);
    }

    private void ExportAsSeparateFiles(string file, string dir, CommandParameter parameter)
    {
        SpriteLoaderAni ani = new();
        ani.Process(file);
        Directory.CreateDirectory(dir);
        List<GifFrame> gifFrames = new();

        for (int i = 0; i < ani.FrameCount; i++)
        {
            ani.SetFrame(i);
#if WINDOWS
            Bitmap bmp = new(ani.FrameSize.x, ani.FrameSize.y, PixelFormat.Format32bppArgb);
            BitmapData loc = bmp.LockBits(new Rectangle(0, 0, ani.FrameSize.x, ani.FrameSize.y), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            using MemoryStream mem = new();
            ani.Render(mem, loc.Stride);
            byte[] pixels = mem.ToArray();
            Marshal.Copy(pixels, 0, loc.Scan0, ani.FrameSize.y * loc.Stride);
            bmp.UnlockBits(loc);
            TextureUtils.Save(bmp, Path.Combine(dir, i + ".png"));
            gifFrames.Add(new GifFrame(pixels, ani.FrameSize.x, ani.FrameSize.y));
#else
            using MemoryStream mem = new();
            ani.Render(mem, ani.FrameSize.x * 4);
            byte[] pixels = mem.ToArray();
            PngWriter.SaveBgra(pixels, ani.FrameSize.x, ani.FrameSize.y, Path.Combine(dir, i + ".png"));
            gifFrames.Add(new GifFrame(pixels, ani.FrameSize.x, ani.FrameSize.y));
#endif
        }

        GifWriter.Save(gifFrames, dir + ".gif");
    }

#if WINDOWS
    void ExportAsSpriteSheet(string originalFilePath, string dir, CommandParameter parameter)
    {
        SpriteLoaderAni loader = new();
        loader.Process(originalFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(dir));
        string outputFilePath = Path.Combine(Path.GetDirectoryName(dir), Path.GetFileNameWithoutExtension(originalFilePath));

        var animations = Enumerable.Range(0, loader.FrameCount)
            .Select(index => { loader.SetFrame(index); return loader.FrameSize; })
            .GroupBy(size => size)
            .Select(group => new { Size = group.Key, Count = group.Count() });

        int frameCount = 0;
        int animCount = 1;
        foreach (var anim in animations)
        {
            loader.Process($"{originalFilePath}?{frameCount}-{frameCount + anim.Count}");
            loader.SetFrame(0);
            var progressive = new SpriteSheet(loader.Size.x * anim.Count, loader.Size.x, loader.Size.y, anim.Count, 0);
            progressive.Render(loader, nonProgressive: false, heightPadding: 1);
            TextureUtils.Save(progressive.Bitmap, $"{outputFilePath}_{animCount}_p.png", parameter);

            loader.Process($"{originalFilePath}?{frameCount}-{frameCount + anim.Count}?p");
            loader.SetFrame(0);
            var fullImage = new SpriteSheet(loader.Size.x * anim.Count, loader.Size.x, loader.Size.y, anim.Count, 0);
            fullImage.Render(loader, nonProgressive: false, heightPadding: 1);
            TextureUtils.Save(fullImage.Bitmap, $"{outputFilePath}_{animCount}_f.png", parameter);

            frameCount += anim.Count;
            animCount++;
        }
    }

    private void ExportAsSingleFile(string originalFilePath, string dir, CommandParameter parameter)
    {
        SpriteLoaderAni ani = new();
        ani.Process(originalFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(dir));

        ani.SetFrame(0);
        string outputFilePath = Path.Combine(Path.GetDirectoryName(dir), Path.GetFileNameWithoutExtension(originalFilePath));

        var progressive = new SpriteSheet(parameter.TextureWidth, ani.Size.x, ani.Size.y, ani.FrameCount, parameter.Padding ? 1 : 0);
        progressive.Render(ani, false);
        TextureUtils.Save(progressive.Bitmap, outputFilePath + "_progressive.png", parameter);

        var baseImage = new SpriteSheet(parameter.TextureWidth, ani.Size.x, ani.Size.y, ani.FrameCount, parameter.Padding ? 1 : 0);
        baseImage.Render(ani, true);
        TextureUtils.Save(baseImage.Bitmap, outputFilePath + "_base.png", parameter);
        TextureUtils.Save(baseImage.Bitmap, progressive.Bitmap, outputFilePath + "_full.png", parameter);
    }
#endif
}
