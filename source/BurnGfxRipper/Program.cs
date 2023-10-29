using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using Burntime.Platform.IO;
using Burntime.Data.BurnGfx;
using System.Security.Cryptography;

namespace BurnGfxRipper;

class CommandParameter
{
    public int TextureWidth = 0;
    public bool RatioCorrection = false;
    public bool MegaTexture = false;
    public bool Padding = false;
    public bool Palette = false;

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

        if (arg.StartsWith("-p"))
        {
            Padding = true;
            return true;
        }

        if (arg.StartsWith("--palette"))
        {
            Palette = true;
            return true;
        }

        return false;
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

                if (parameter.Palette)
                {
                    ExportColorTables(path, file);
                    return;
                }

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
                            var exporter = new TileExporter();
                            exporter.Export(file, dir, parameter);
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
                            TextureUtils.Save(bmp, dir + ".png");
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

    static void ExportColorTables(string basePath, string fileName)
    {
        using Bitmap bmp = new(256, 1, PixelFormat.Format24bppRgb);
        for (int i = 0; i < 256; i++)
            bmp.SetPixel(i, 0, Color.FromArgb(BurnGfxData.Instance.DefaultColorTable.GetColor(i).ToInt()));

        bmp.Save(System.IO.Path.Combine(basePath, System.IO.Path.GetFileNameWithoutExtension(fileName) + "_palette.png"));

        using Bitmap bmp2 = new(256, 38, PixelFormat.Format24bppRgb);
        for (int map = 0; map < 38; map++)
            for (int i = 0; i < 256; i++)
                bmp2.SetPixel(i, map, Color.FromArgb(BurnGfxData.Instance.GetMapColorTable(map).GetColor(i).ToInt()));

        bmp2.Save(System.IO.Path.Combine(basePath, "maps_palette.png"));
    }
}
