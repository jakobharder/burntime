using System.IO;
using Burntime.Platform.IO;

namespace Burntime.Framework
{
    public class SaveGame
    {
        string version;
        string game;
        Burntime.Platform.IO.File file;
        bool isValid;

        public Stream Stream
        {
            get { return file.Stream; }
        }

        public string Version
        {
            get { return version; }
        }

        public string Game
        {
            get { return game; }
        }

        public bool IsValid
        {
            get { return isValid; }
        }

        public SaveGame(string filename)
        {
            file = FileSystem.GetFile(filename);
            if (file == null)
            {
                isValid = false;
                return;
            }

            BinaryReader reader = new BinaryReader(file);

            try
            {
                game = reader.ReadString();
                version = reader.ReadString();
            }
            catch
            {
                isValid = false;
                return;
            }

            isValid = true;
        }

        public SaveGame(string filename, string game, string version)
        {
            if (FileSystem.ExistsFile(filename))
                FileSystem.RemoveFile(filename);

            file = FileSystem.CreateFile(filename);
            if (file == null)
            {
                isValid = false;
                return;
            }

            BinaryWriter writer = new BinaryWriter(file);

            writer.Write(game);
            writer.Write(version);

            isValid = true;
        }

        public void Close()
        {
            if (file == null)
                return;

            file.Close();
        }
    }
}
