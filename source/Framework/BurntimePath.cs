
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

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

        public bool ShowSelector()
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.Description = "Please locate your Burntime folder. It should contain a folder called BURN_GFX";
            dlg.ShowNewFolderButton = false;
            DialogResult res = dlg.ShowDialog();
            if (DialogResult.OK == res)
                Path = dlg.SelectedPath;

            return res == DialogResult.OK;
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
                return true;
            }

            return false;
        }
    }
}
