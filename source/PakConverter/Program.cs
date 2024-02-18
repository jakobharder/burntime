using System;
using Burntime.Platform.IO;

namespace PakConverter
{
    class Program
    {
        static readonly CursorPosition _cursor = new CursorPosition();

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("usage: pakconverter.exe foldername");
                Console.ReadKey();
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
