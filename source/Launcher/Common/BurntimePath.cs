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
            if (!File.Exists(path + "\\burn.exe"))
                return false;
            if (!File.Exists(path + "\\burn_gfx\\zeis.raw"))
                return false;
            return true;
        }
    }
}
