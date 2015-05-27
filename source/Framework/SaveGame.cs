/*
 *  Burntime Framework
 *  Copyright (C) 2009
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 *  contact: 
 *    juern - burntimedeluxe@gmail.com or yn.harada@gmail.com
 * 
 *  authors: 
 *    Juernjakob Harder - 原田ゆあん (yn.harada@gmail.com)
 * 
*/

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
