/*
 *  Burntime Platform
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
 *  authors: 
 *    Juernjakob Harder (yn.harada@gmail.com)
 * 
*/

using System;
using System.Collections.Generic;
using Burntime.Platform.IO;

namespace Burntime.Platform
{
    public class TextResourceFile
    {
        List<String> data;

        public List<String> Data
        {
            get
            {
                return data;
            }
        }

        public TextResourceFile(File File)
        {
            data = new List<string>();

            String line = File.ReadLine();
            while (line != null)
            {
                data.Add(line);//line.Replace("}", ""));

                line = File.ReadLine();
            }
        }

        public String[] GetStrings(int start)
        {
            int last = start + 1;

            for (int i = start; i < data.Count; i++)
            {
                int f = data[i].IndexOf("}#");
                if (f != -1)
                {
                    last = i;
                    break;
                }
            }

            int count = last - start;
            String[] strs = new String[count];
            for (int i = 0; i < count; i++)
            {
                strs[i] = data[i + start].Replace("}", "");
            }

            return strs;
        }
    }
}
