
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

namespace Burntime.Platform
{
    public class Log
    {
        static StreamWriter file;
        public static bool DebugOut;

        static public void Initialize(String file)
        {
            Log.file = new StreamWriter(file, false);
        }

        static public void Info(String str)
        {
            if (file != null)
            {
                file.WriteLine("[info] " + str);
                file.Flush();
            }
        }

        static public void Warning(String str)
        {
            if (file != null)
            {
                file.WriteLine("[warning] " + str);
                file.Flush();
            }
        }

        static public void Debug(String str)
        {
            if (file != null && DebugOut)
            {
                file.WriteLine("[debug] " + str);
                file.Flush();
            }
        }
    }
}
