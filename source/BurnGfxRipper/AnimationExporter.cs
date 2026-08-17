using Burntime.Data.BurnGfx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using StbImageSharp;
#if WINDOWS
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
#endif

namespace BurnGfxRipper;

class AnimationExporter
{
    private sealed class SceneAnimation
    {
        public int First;
        public int Last;
        public int X;
        public int Y;
        public double Speed = 5;
        public double Interval;
        public double Delay;
        public bool Progressive = true;
        public bool Cumulative;
        public bool Endless = true;
        public double Frame;
        public double Pause;
        public bool RestartAfterPause;
        public List<DecodedFrame> Frames = new();
    }

    private sealed record DecodedFrame(byte[] Pixels, int Width, int Height);

    public void ExportSceneGif(string metadataPath)
    {
        metadataPath = Path.GetFullPath(metadataPath);
        string directory = Path.GetDirectoryName(metadataPath)!;
        string aniName = Path.GetFileNameWithoutExtension(metadataPath);
        if (!aniName.EndsWith(".ani", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("GIF metadata must be named <scene>.ANI.txt.");

        string sceneName = Path.GetFileNameWithoutExtension(aniName);
        DecodedFrame background = ReadPng(Path.Combine(directory, sceneName + ".PAC.png"));
        string frameDirectory = Path.Combine(directory, aniName);
        List<SceneAnimation> animations = ReadMetadata(metadataPath);
        foreach (SceneAnimation animation in animations)
            animation.Frames = ReadFrames(frameDirectory, animation);

        const double tick = 0.1;
        double duration = animations.Any(animation => !animation.Endless)
            ? animations.Where(animation => !animation.Endless).Max(animation => animation.Delay + animation.Frames.Count / animation.Speed)
            : animations.Max(animation => animation.Delay + animation.Frames.Count / animation.Speed + animation.Interval);
        int outputFrameCount = Math.Max(1, (int)Math.Ceiling(duration / tick));

        foreach (SceneAnimation animation in animations)
            animation.Pause = animation.Delay;

        List<GifFrame> outputFrames = new(outputFrameCount);
        for (int index = 0; index < outputFrameCount; index++)
        {
            byte[] scene = (byte[])background.Pixels.Clone();
            foreach (SceneAnimation animation in animations)
            {
                int frameIndex = Math.Clamp((int)Math.Floor(animation.Frame), 0, animation.Frames.Count - 1);
                if (animation.Progressive && frameIndex != 0 && !animation.Cumulative)
                    Composite(scene, background.Width, background.Height, animation.Frames[0], animation.X, animation.Y);
                Composite(scene, background.Width, background.Height, animation.Frames[frameIndex], animation.X, animation.Y);
            }
            outputFrames.Add(new GifFrame(scene, background.Width, background.Height));

            foreach (SceneAnimation animation in animations)
                UpdateAnimation(animation, tick);
        }

        string outputPath = Path.Combine(directory, aniName + ".gif");
        GifWriter.Save(outputFrames, outputPath, frameDelay: 10);
    }

    private static List<SceneAnimation> ReadMetadata(string path)
    {
        List<SceneAnimation> result = new();
        int lineNumber = 0;
        foreach (string sourceLine in File.ReadLines(path))
        {
            lineNumber++;
            string line = sourceLine.Split('#')[0].Trim();
            if (line.Length == 0)
                continue;

            string[] fields = line.Split(default(char[]), StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 2)
                throw new InvalidDataException($"{path}:{lineNumber}: expected '<first>-<last> <x>x<y>'.");

            int[] range = ParsePair(fields[0], '-');
            int[] position = ParsePair(fields[1], 'x');
            SceneAnimation animation = new() { First = range[0], Last = range[1], X = position[0], Y = position[1] };

            foreach (string field in fields.Skip(2))
            {
                string[] setting = field.Split('=', 2);
                if (setting.Length != 2)
                    throw new InvalidDataException($"{path}:{lineNumber}: invalid setting '{field}'.");
                switch (setting[0].ToLowerInvariant())
                {
                    case "speed": animation.Speed = ParseDouble(setting[1]); break;
                    case "interval": animation.Interval = ParseDouble(setting[1]); break;
                    case "delay": animation.Delay = ParseDouble(setting[1]); break;
                    case "progressive": animation.Progressive = bool.Parse(setting[1]); break;
                    case "cumulative": animation.Cumulative = bool.Parse(setting[1]); break;
                    case "endless": animation.Endless = bool.Parse(setting[1]); break;
                    default: throw new InvalidDataException($"{path}:{lineNumber}: unknown setting '{setting[0]}'.");
                }
            }
            if (animation.First > animation.Last || animation.Speed <= 0 || animation.Interval < 0 || animation.Delay < 0)
                throw new InvalidDataException($"{path}:{lineNumber}: invalid animation values.");
            result.Add(animation);
        }

        if (result.Count == 0)
            throw new InvalidDataException($"No animations found in {path}.");
        return result;
    }

    private static int[] ParsePair(string value, char separator)
    {
        string[] pair = value.Split(separator, 2);
        if (pair.Length != 2 || !int.TryParse(pair[0], out int first) || !int.TryParse(pair[1], out int second))
            throw new InvalidDataException($"Invalid pair '{value}'.");
        return [first, second];
    }

    private static double ParseDouble(string value) =>
        double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static List<DecodedFrame> ReadFrames(string directory, SceneAnimation animation)
    {
        List<DecodedFrame> sourceFrames = new();
        for (int index = animation.First; index <= animation.Last; index++)
            sourceFrames.Add(ReadPng(Path.Combine(directory, index + ".png")));

        if (!animation.Cumulative)
            return sourceFrames;

        int width = sourceFrames.Max(frame => frame.Width);
        int height = sourceFrames.Max(frame => frame.Height);
        byte[] cumulative = new byte[width * height * 4];
        List<DecodedFrame> frames = new(sourceFrames.Count);
        foreach (DecodedFrame source in sourceFrames)
        {
            byte[] pixels = (byte[])cumulative.Clone();
            Composite(pixels, width, height, source, 0, 0);
            cumulative = pixels;
            frames.Add(new DecodedFrame(pixels, width, height));
        }
        return frames;
    }

    private static DecodedFrame ReadPng(string path)
    {
        using FileStream stream = File.OpenRead(path);
        ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        byte[] bgra = new byte[image.Data.Length];
        for (int offset = 0; offset < image.Data.Length; offset += 4)
        {
            bgra[offset] = image.Data[offset + 2];
            bgra[offset + 1] = image.Data[offset + 1];
            bgra[offset + 2] = image.Data[offset];
            bgra[offset + 3] = image.Data[offset + 3];
        }
        return new DecodedFrame(bgra, image.Width, image.Height);
    }

    private static void UpdateAnimation(SceneAnimation animation, double elapsed)
    {
        if (animation.Pause > 0)
        {
            animation.Pause -= elapsed;
            if (animation.Pause <= 0 && animation.RestartAfterPause)
            {
                animation.Frame = 0;
                animation.RestartAfterPause = false;
            }
            return;
        }

        animation.Frame += elapsed * animation.Speed;
        if (animation.Endless)
        {
            while (animation.Frame >= animation.Frames.Count)
            {
                if (animation.Interval > 0)
                {
                    animation.Frame = animation.Frames.Count - 0.0001;
                    animation.Pause = animation.Interval;
                    animation.RestartAfterPause = true;
                    break;
                }
                animation.Frame -= animation.Frames.Count;
            }
        }
        else if (animation.Frame >= animation.Frames.Count)
        {
            animation.Frame = animation.Frames.Count - 0.0001;
        }
    }

    private static void Composite(byte[] target, int targetWidth, int targetHeight, DecodedFrame source, int positionX, int positionY)
    {
        for (int y = 0; y < source.Height; y++)
        for (int x = 0; x < source.Width; x++)
        {
            int sourceOffset = (y * source.Width + x) * 4;
            if (source.Pixels[sourceOffset + 3] == 0)
                continue;

            int targetX = positionX + x;
            int targetY = positionY + y;
            if (targetX < 0 || targetX >= targetWidth || targetY < 0 || targetY >= targetHeight)
                continue;

            int targetOffset = (targetY * targetWidth + targetX) * 4;
            Array.Copy(source.Pixels, sourceOffset, target, targetOffset, 4);
        }
    }

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
