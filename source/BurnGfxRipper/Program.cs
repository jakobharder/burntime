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
    public bool Padding = true;

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
}
