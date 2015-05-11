using System;
using Burntime.Platform.IO;

namespace PakConverter
{
    class Program
    {
        static int cursorpos = 0;

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("usage: pakconverter.exe foldername");
                Console.ReadKey();
                return;
            }

            Console.Write("convert " + args[0] + " to " + args[0] + ".pak... ");
            cursorpos = Console.CursorLeft;
            try
            {
                FileSystem.ConvertFolderToPak(args[0], Feedback);
            }
            catch (Exception e)
            {
                Console.CursorLeft = cursorpos;
                Console.WriteLine("failed");
                Console.WriteLine(e.Message);
                Console.ReadKey();
                return;
            }
            
            Console.CursorLeft = cursorpos;
            Console.WriteLine("finished");
        }

        static void Feedback(float percentage)
        {
            Console.CursorLeft = cursorpos;
            int p = (int)(percentage * 100);
            Console.Write(p.ToString("D3") + "%");
        }
    }
}
