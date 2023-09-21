using System;
using System.Drawing;
using Burntime.Platform.IO;

namespace Burntime.Common
{
    public struct LanguageInfo
    {
        public Bitmap Icon;
        public String ID;
    }

    public class Language
    {
        LanguageInfo[] languages;
        int defaultLang;
        int current;

        public LanguageInfo[] Languages
        {
            get { return languages; }
        }

        public LanguageInfo Default
        {
            get { return languages[defaultLang]; }
        }

        public LanguageInfo Current
        {
            get { return languages[current]; }
        }

        public String CurrentID
        {
            get { return Current.ID; }
            set
            {
                current = defaultLang;

                for (int i = 0; i < languages.Length; i++)
                {
                    if (value == languages[i].ID)
                        current = i;
                }
            }
        }

        public Language(String directory)
        {
            defaultLang = 0;

            string[] files = FileSystem.GetFileNames(directory, ".png");
            languages = new LanguageInfo[files.Length];
            for (int i = 0; i < languages.Length; i++)
            {
                String lang = System.IO.Path.GetFileNameWithoutExtension(files[i]);
                languages[i].ID = lang.Substring(6).ToUpper();

                File bitmap = FileSystem.GetFile(directory + files[i]);
                languages[i].Icon = new Bitmap(bitmap.Stream);
                bitmap.Close();

                if (languages[i].ID == "EN")
                    defaultLang = i;
            }

            current = defaultLang;
        }
    }
}
