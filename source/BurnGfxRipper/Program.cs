using System;
using Burntime.Platform.IO;
using Burntime.Data.BurnGfx;

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
        if (args.Length == 2 && args[0] == "--gif")
        {
            new AnimationExporter().ExportSceneGif(args[1]);
            return;
        }

        if (args.Length == 0)
        {
            Console.WriteLine("usage:\nburngfxripper.exe filename\nburngfxripper.exe --gif filename.ani.txt\n\npress key");
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
                String dir = System.IO.Path.Combine(path, file + "_output");

                if (parameter.Palette)
                {
                    ExportColorTables(path, file);
                    return;
                }

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
                            Console.WriteLine("tile extraction is not supported by this portable build");
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

                            using System.IO.MemoryStream mem = new();
                            pac.Render(mem, pac.Size.x * 4);
                            PngWriter.SaveBgra(mem.ToArray(), pac.Size.x, pac.Size.y, dir + ".png");
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
        throw new NotSupportedException("palette extraction is not supported by this portable build");
    }
}
