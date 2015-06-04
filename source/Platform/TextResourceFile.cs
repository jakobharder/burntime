
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
