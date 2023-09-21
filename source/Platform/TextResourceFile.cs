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
