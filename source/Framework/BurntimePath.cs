/*
 *  Burntime Commons
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

using System;
using System.IO;
using System.Windows.Forms;

namespace Burntime.Common
{
    public class BurntimePath
    {
        String dir;
        String path;
        bool valid;

        public String Path
        {
            get { return path; }
            set { path = value; valid = CheckPath(path); }
        }

        public override string ToString()
        {
            return path;
        }

        public bool IsValid
        {
            get { return valid; }
        }

        public BurntimePath()
        {
            dir = ".";
            FileStream file = new FileStream(dir + "\\path.txt", FileMode.OpenOrCreate);
            StreamReader reader = new StreamReader(file);
            Path = reader.ReadLine();
            reader.Close();
            file.Close();
        }

        public BurntimePath(String systemDir)
        {
            dir = systemDir;
            FileStream file = new FileStream(dir + "\\path.txt", FileMode.OpenOrCreate);
            StreamReader reader = new StreamReader(file);
            Path = reader.ReadLine();
            reader.Close();
            file.Close();
        }

        public void Save()
        {
            StreamWriter writer = new StreamWriter(dir + "\\path.txt");
            writer.Write(path);
            writer.Close();
        }

        public DialogResult ShowSelector()
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.ShowNewFolderButton = false;
            DialogResult res = dlg.ShowDialog();
            if (DialogResult.OK == res)
                Path = dlg.SelectedPath;

            return res;
        }

        bool CheckPath(String path)
        {
            if (!System.IO.Path.IsPathRooted(path))
            {
                path = dir + "\\" + path;
            }

            // check if there is a pak
            if (path.EndsWith(".pak", StringComparison.InvariantCultureIgnoreCase) && System.IO.File.Exists(path + ".pak"))
            {
                return true;
            }

            // otherwise check for folder
            if (System.IO.Directory.Exists(path))
            {
                if (!File.Exists(path + "\\burn.exe"))
                    return false;
                if (!File.Exists(path + "\\burn_gfx\\zeis.raw"))
                    return false;
            }

            return true;
        }
    }
}
