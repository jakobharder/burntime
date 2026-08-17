using System;
using Burntime.Platform.IO;

namespace PakConverter
{
    class Program
    {
        static readonly CursorPosition _cursor = new CursorPosition();

        static void Main(string[] args)
        {
            if (args.Length is < 1 or > 2)
            {
                Console.WriteLine("usage: pakconverter.exe foldername | pakconverter.exe archive.pak [output-folder]");
                Console.ReadKey();
                return;
            }

            if (args[0].EndsWith(".pak", StringComparison.OrdinalIgnoreCase))
            {
                string output = args.Length == 2
                    ? args[1]
                    : System.IO.Path.Combine(System.IO.Path.GetDirectoryName(args[0])!, System.IO.Path.GetFileNameWithoutExtension(args[0]));

                Console.Write("extract " + args[0] + " to " + output + "... ");
                _cursor.Save();
                try
                {
                    FileSystem.ConvertPakToFolder(args[0], output, Feedback);
                }
                catch (Exception e)
                {
                    _cursor.Restore();
                    Console.WriteLine("failed");
                    Console.WriteLine(e.Message);
                    Console.ReadKey();
                    return;
                }
                _cursor.Restore();
                Console.WriteLine("finished");
                return;
            }

            Console.Write("convert " + args[0] + " to " + args[0] + ".pak... ");
            _cursor.Save();
            try
            {
                FileSystem.ConvertFolderToPak(args[0], Feedback);
            }
            catch (Exception e)
            {
                _cursor.Restore();
                Console.WriteLine("failed");
                Console.WriteLine(e.Message);
                Console.ReadKey();
                return;
            }
            _cursor.Restore();
            Console.WriteLine("finished");
        }

        static void Feedback(float percentage)
        {
            _cursor.Restore();
            int p = (int)(percentage * 100);
            Console.Write(p.ToString("D3") + "%");
        }
    }

    class CursorPosition
    {
        int _position = 0;
        bool _supportCursorPosition = true;

        public void Save()
        {
            if (!_supportCursorPosition) return;

            try
            {
                _position = Console.CursorLeft;
            }
            catch
            {
                _supportCursorPosition = false;
            }
        }

        public void Restore()
        {
            if (!_supportCursorPosition) return;

            Console.CursorLeft = _position;
        }
    }
}
