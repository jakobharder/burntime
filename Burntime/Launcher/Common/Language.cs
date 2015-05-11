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
