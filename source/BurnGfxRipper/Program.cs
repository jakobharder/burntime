using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using Burntime.Platform.IO;
using Burntime.Data.BurnGfx;

namespace BurnGfxRipper;

class CommandParameter
{
    public int TextureWidth = 0;
    public bool RatioCorrection = false;
    public bool MegaTexture = false;

    public bool HandleArg(string arg)
    {
        if (arg.StartsWith("-m"))
        {
            _ = int.TryParse(arg.AsSpan(2), out TextureWidth);
            MegaTexture = true;
            return true;
        }

        if (arg.StartsWith("-r"))
        {
            RatioCorrection = true;
            return true;
        }

        return false;
    }
}

class AnimationExporter
{
    protected static int MakePowerOfTwo(int nValue)
    {
        if (nValue > 256 && nValue <= 384) return 384;
        if (nValue > 512 && nValue <= 768) return 768;

        nValue--;
        int i;
        for (i = 0; nValue != 0; i++)
            nValue >>= 1;
        return 1 << i;
    }

    public void Export(string file, string dir, CommandParameter parameter)
    {
        if (!parameter.MegaTexture)
        {
            ExportAsSeperateFiles(file, dir, parameter);
        }
        else
        {
            ExportAsSingleFile(file, dir, parameter);
        }
    }

    private void ExportAsSeperateFiles(string file, string dir, CommandParameter parameter)
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
            Program.Save(bmp, dir + "\\" + i + ".png");
        }
    }

    private void ExportAsSingleFile(string file, string dir, CommandParameter parameter)
    {
        SpriteLoaderAni ani = new SpriteLoaderAni();
        ani.Process(file);

        System.IO.Directory.CreateDirectory(System.IO.Path.GetFileName(dir));

        ani.SetFrame(0);
        int columns = (int)Math.Floor(parameter.TextureWidth / (float)ani.Size.x);
        int rows = (int)Math.Ceiling(ani.FrameCount / (float)columns);

        int textureHeight = MakePowerOfTwo(rows * ani.Size.y);

        var bmp = new Bitmap(parameter.TextureWidth, textureHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        int i = 0;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns && i < ani.FrameCount; x++)
            {
                ani.SetFrame(i);

                BitmapData loc = bmp.LockBits(new Rectangle(x * ani.FrameSize.x, y * ani.FrameSize.y, ani.FrameSize.x, ani.FrameSize.y), 
                    ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                System.IO.MemoryStream mem = new System.IO.MemoryStream();
                ani.Render(mem, ani.FrameSize.x * 4);

                byte[] buffer = mem.ToArray();

                for (int py = 0; py < ani.FrameSize.y; py++)
                    Marshal.Copy(buffer, 
                        py * ani.FrameSize.x * 4, 
                        loc.Scan0 + py * loc.Stride, 
                        ani.FrameSize.x * 4);

                bmp.UnlockBits(loc);

                i++;
            }
        }

        Program.Save(bmp, dir + "_progressive.png", parameter, ani.Size.y, rows);

        i = 0;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns && i < ani.FrameCount; x++)
            {
                ani.SetFrame(0);

                BitmapData loc = bmp.LockBits(new Rectangle(x * ani.FrameSize.x, y * ani.FrameSize.y, ani.FrameSize.x, ani.FrameSize.y),
                    ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                System.IO.MemoryStream mem = new System.IO.MemoryStream();
                ani.Render(mem, ani.FrameSize.x * 4);

                byte[] buffer = mem.ToArray();

                for (int py = 0; py < ani.FrameSize.y; py++)
                    Marshal.Copy(buffer,
                        py * ani.FrameSize.x * 4,
                        loc.Scan0 + py * loc.Stride,
                        ani.FrameSize.x * 4);

                bmp.UnlockBits(loc);

                i++;
            }
        }
        Program.Save(bmp, dir + "_base.png", parameter, ani.Size.y, rows);
    }
}

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("usage:\nburngfxripper.exe filename\n\npress key");
            Console.ReadKey();
            return;
        }

        //try
        {
            var parameter = new CommandParameter();

            foreach (string arg in args)
            {
                if (parameter.HandleArg(arg)) continue;

                String path = System.IO.Path.GetDirectoryName(arg);
                FileSystem.AddPackage("burntime", path);

                String ext = System.IO.Path.GetExtension(arg).ToLower();
                String file = System.IO.Path.GetFileName(arg);
                String dir = path + "\\" + file + "_output";

                bool stretch = true;

                switch (ext)
                {
                    case ".raw":
                    case ".ani":
                        if (file.StartsWith("mat_", StringComparison.InvariantCultureIgnoreCase))
                        {
                            Console.WriteLine("use the map editor to export maps");
                            Console.WriteLine("press key...");
                            Console.ReadKey();
                        }
                        else if (file.StartsWith("zei_", StringComparison.InvariantCultureIgnoreCase))
                        {
                            System.IO.Directory.CreateDirectory(dir);

                            int[] maplist = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18,
                            19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 80,
                            81, 82, 83, 84, 89 };

                            File zei = FileSystem.GetFile(file);

                            bool[] done = new bool[63];
                            
                            foreach (int i in maplist)
                            {
                                File f = FileSystem.GetFile("mat_" + i.ToString("D3") + ".raw");
                                Map map = new Map(f);
                                f.Close();
                                foreach (ushort tileid in map.data)
                                {
                                    int set = tileid & 0xff;
                                    int id = tileid >> 8;

                                    if (!file.Equals("zei_" + set.ToString("D3") + ".raw", StringComparison.InvariantCultureIgnoreCase))
                                        continue;
                                    if (done[id])
                                        continue;

                                    done[id] = true;

                                    zei.Seek(id * (16 + 32 * 32), SeekPosition.Begin);
                                    Tile tile = new Tile(zei, i);

                                    Bitmap bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                                    BitmapData loc = bmp.LockBits(new Rectangle(0, 0, 32, 32), ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                                    System.IO.MemoryStream mem = new System.IO.MemoryStream();
                                    tile.Render(mem, loc.Stride);

                                    Marshal.Copy(mem.ToArray(), 0, loc.Scan0, 32 * loc.Stride);

                                    bmp.UnlockBits(loc);

                                    //Save(bmp, dir + "\\" + set.ToString("D3") + "_" + id.ToString("D2") + ".png", stretch);

                                    if (tile.Mask != 0)
                                    {
                                        bool[] bmask = new bool[16];

                                        bool mask = false;
                                        bool nomask = false;

                                        int value = tile.Mask;
                                        for (int k = 0; k < 16; k++, value >>= 1)
                                        {
                                            bmask[15 - k] = (value & 1) == 1;
                                            mask |= bmask[15 - k];
                                            nomask |= !bmask[15 - k];
                                        }

                                        if (mask)
                                        {

                                            String maskFile = dir + "\\" + set.ToString("D3") + "_" + id.ToString("D2") + ".txt";
                                            System.IO.TextWriter writer = new System.IO.StreamWriter(maskFile);

                                            for (int y = 0; y < 4; y++)
                                            {
                                                for (int x = 0; x < 4; x++)
                                                {
                                                    writer.Write(bmask[y * 4 + x] ? "1" : "0");
                                                }
                                                writer.WriteLine();
                                            }

                                            writer.Close();
                                        }
                                    }
                                }

                            }
                        }
                        else
                        {

                            var exporter = new AnimationExporter();
                            exporter.Export(file, dir, parameter);
                        }
                        break;
                    case ".pac":
                        {
                            SpriteLoaderPac pac = new SpriteLoaderPac();
                            pac.Process(file);

                            Bitmap bmp = new Bitmap(pac.Size.x, pac.Size.y, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            BitmapData loc = bmp.LockBits(new Rectangle(0, 0, pac.Size.x, pac.Size.y), ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            System.IO.MemoryStream mem = new System.IO.MemoryStream();
                            pac.Render(mem, loc.Stride);

                            Marshal.Copy(mem.ToArray(), 0, loc.Scan0, pac.Size.y * loc.Stride);

                            bmp.UnlockBits(loc);
                            Save(bmp, dir + ".png");
                        }
                        break;
                }
            }
        }
        //catch (Exception e)
        //{
        //    Console.WriteLine("msg: " + e.Message);
        //    Console.WriteLine("source: " + e.Source);
        //    Console.WriteLine(e.StackTrace);
        //    Console.WriteLine("press key...");
        //    Console.ReadKey();
        //}
    }

    public static void Save(Bitmap bmp, string name, CommandParameter parameter = null, int rowHeight = 0, int rows = 0)
    {
        if (parameter is null || !parameter.RatioCorrection || rowHeight == 0 || rows == 0)
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
}
