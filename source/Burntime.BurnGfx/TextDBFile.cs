using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.IO;

namespace Burntime.Data.BurnGfx
{
    public class TextDBFile
    {
        List<String> data;

        public List<String> Data
        {
            get
            {
                return data;
            }
        }

        public TextDBFile(File file)
        {
            data = new List<string>();

            string line = file.ReadLine();
            while (line is not null)
            {
                data.Add(line.TrimEnd());

                line = file.ReadLine();
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
