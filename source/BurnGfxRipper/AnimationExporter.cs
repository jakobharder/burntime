using Burntime.Data.BurnGfx;
using System;
using System.Drawing.Imaging;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;

namespace BurnGfxRipper;

class AnimationExporter
{
    public void Export(string file, string dir, CommandParameter parameter)
    {
        if (!parameter.MegaTexture)
        {
            ExportAsSpriteSheet(file, dir, parameter);
            //ExportAsSeparateFiles(file, dir, parameter);
        }
        else
        {
            ExportAsSingleFile(file, dir, parameter);
        }
    }

    private void ExportAsSeparateFiles(string file, string dir, CommandParameter parameter)
    {
        SpriteLoaderAni ani = new SpriteLoaderAni();
        ani.Process(file);

        System.IO.Directory.CreateDirectory(dir);

        for (int i = 0; i < ani.FrameCount; i++)
        {
            ani.SetFrame(i);

            Bitmap bmp = new Bitmap(ani.FrameSize.x, ani.FrameSize.y, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            BitmapData loc = bmp.LockBits(new Rectangle(0, 0, ani.FrameSize.x, ani.FrameSize.y), ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            System.IO.MemoryStream mem = new System.IO.MemoryStream();
            ani.Render(mem, loc.Stride);

            Marshal.Copy(mem.ToArray(), 0, loc.Scan0, ani.FrameSize.y * loc.Stride);

            bmp.UnlockBits(loc);
            TextureUtils.Save(bmp, dir + "\\" + i + ".png");
        }
    }

    void ExportAsSpriteSheet(string originalFilePath, string dir, CommandParameter parameter)
    {
        SpriteLoaderAni loader = new();
        loader.Process(originalFilePath);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dir));
        string outputFilePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dir), System.IO.Path.GetFileNameWithoutExtension(originalFilePath));

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
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dir));

        ani.SetFrame(0);
        string outputFilePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dir), System.IO.Path.GetFileNameWithoutExtension(originalFilePath));

        var progressive = new SpriteSheet(parameter.TextureWidth, ani.Size.x, ani.Size.y, ani.FrameCount, parameter.Padding ? 1 : 0);
        progressive.Render(ani, false);
        TextureUtils.Save(progressive.Bitmap, outputFilePath + "_progressive.png", parameter);

        var baseImage = new SpriteSheet(parameter.TextureWidth, ani.Size.x, ani.Size.y, ani.FrameCount, parameter.Padding ? 1 : 0);
        baseImage.Render(ani, true);
        TextureUtils.Save(baseImage.Bitmap, outputFilePath + "_base.png", parameter);
        TextureUtils.Save(baseImage.Bitmap, progressive.Bitmap, outputFilePath + "_full.png", parameter);
    }
}

