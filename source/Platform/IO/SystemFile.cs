
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
using System.Text;

namespace Burntime.Platform.IO
{
    public class SystemFile : File
    {
        String path;
        public override bool HasFullPath { get { return true; } }
        public override String FullPath { get { return path; } }

        String name;
        String package;
        public override bool HasName { get { return true; } }
        public override String Name { get { return name; } }
        public override String PackageName { get { return package; } }
        public override String FullName { get { return package + ":" + name; } }

        public SystemFile(String Path, String Name, bool WriteAccess)
        {
            stream = new System.IO.FileStream(Path, System.IO.FileMode.OpenOrCreate, WriteAccess ? System.IO.FileAccess.ReadWrite : System.IO.FileAccess.Read);
            path = System.IO.Path.GetFullPath(Path);

            String[] token = Name.Split(new Char[] { ':' });
            name = token[1];
            package = token[0];
        }
    }
}
