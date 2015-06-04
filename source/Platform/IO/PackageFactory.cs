
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
    public class PackageFactory
    {
        // path must be relative to current directory
        public IPackage OpenPackage(string path)
        {
            return OpenPackage(path, "");
        }

        public IPackage OpenPackage(string path, string subPath)
        {
            IPackage p = null;

            if (!subPath.EndsWith("/") && subPath != "")
                subPath += "/";

            // first try as folder packages
            if (System.IO.Directory.Exists(path))
                p = new PackageFolder("", path, subPath);

            // then try as pak packages
            else if (System.IO.File.Exists(path + ".pak"))
                p = new PackagePak("", path, subPath);


            return p;
        }

        public File OpenFileDirectly(string packagePath, string filePath, FileOpenMode mode)
        {
            File file = null;

            if ((mode & FileOpenMode.Write) != FileOpenMode.Write && System.IO.File.Exists(packagePath))
            {
                PackagePak pak = new PackagePak("", packagePath);
                file = pak.GetFile(filePath, mode & FileOpenMode.Write);
                pak.Close();
            }
            else if ((mode & FileOpenMode.Write) != FileOpenMode.Write && System.IO.File.Exists(packagePath + ".pak"))
            {
                PackagePak pak = new PackagePak("", packagePath + ".pak");
                file = pak.GetFile(filePath, mode & FileOpenMode.Write);
                pak.Close();
            }
            else if ((mode & FileOpenMode.Write) == FileOpenMode.Write ||
                System.IO.File.Exists(packagePath + (packagePath != "" ? "/" : "") + filePath))
            {
                file = new SystemFile(packagePath + (packagePath != "" ? "/" : "") + filePath, packagePath + ":" + filePath, (mode & FileOpenMode.Write) == FileOpenMode.Write);
            }

            return file;
        }

        public bool IsValidPackage(string path)
        {
            // first try as pak packages
            if (System.IO.File.Exists(path + ".pak"))
                return true;

            // then try as folder packages
            else if (System.IO.Directory.Exists(path))
            {
                if (System.IO.File.Exists(path + "/info.txt"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
