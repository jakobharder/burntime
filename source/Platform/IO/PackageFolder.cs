
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
using System.IO;

namespace Burntime.Platform.IO
{
    class PackageFolder : IPackage
    {
        Dictionary<String, File> dicFiles;
        String path;
        String name;
        string subPath;

        public ICollection<String> Files
        {
            get { return dicFiles.Keys; }
        }

        public String Name
        {
            get { return name; }
        }

        public PackageFolder(String name, String path, string subPath)
        {
            this.path = path;
            this.name = name;
            this.subPath = subPath;

            dicFiles = new Dictionary<string, File>();
            process("", path + "/" + subPath);
        }

        public void Close()
        {
        }

        void process(String relpath, String path)
        {
            try
            {
                string[] files = Directory.GetFiles(path);

                foreach (string file in files)
                {
                    string name = System.IO.Path.GetFileName(file);

                    // skip files beginning with .
                    if (name.StartsWith("."))
                        continue;

                    dicFiles.Add(relpath + name.ToLower(), null);
                }

                string[] dirs = Directory.GetDirectories(path);
                foreach (string dir in dirs)
                {
                    string name = System.IO.Path.GetFileName(dir);

                    // skip directories beginning with .
                    if (name.StartsWith("."))
                        continue;

                    process(relpath + name.ToLower() + "/", dir);
                }

            }
            catch
            {
                return;
            }

        }

        public File GetFile(FilePath filePath, FileOpenMode mode)
        {
            if ((mode & FileOpenMode.NoPackage) == FileOpenMode.NoPackage)
                throw new InvalidOperationException();

            if (!dicFiles.ContainsKey(filePath.PathWithoutPackage))
                return null;
            return new SystemFile(path + "/" + subPath + filePath.PathWithoutPackage, name + ":" + filePath.PathWithoutPackage, mode == FileOpenMode.Write);
        }

        public bool ExistsFile(FilePath filePath)
        {
            return dicFiles.ContainsKey(filePath.PathWithoutPackage);
        }

        public bool AddFile(FilePath filePath)
        {
            if (dicFiles.ContainsKey(filePath.PathWithoutPackage))
                return false;

            try
            {
                string directory = System.IO.Path.GetDirectoryName(path + "/" + subPath + filePath.PathWithoutPackage);
                if (!System.IO.Directory.Exists(directory))
                    System.IO.Directory.CreateDirectory(directory);
                FileStream stream = new FileStream(path + "/" + subPath + filePath.PathWithoutPackage, FileMode.CreateNew);
                stream.Close();
            }
            catch
            {
                return false;
            }

            dicFiles.Add(filePath.PathWithoutPackage, null);

            return true;
        }

        public bool RemoveFile(FilePath filePath)
        {
            if (!dicFiles.ContainsKey(filePath.PathWithoutPackage))
                return false;

            try
            {
                System.IO.File.Delete(path + "/" + subPath + filePath.PathWithoutPackage);
            }
            catch
            {
                return false;
            }

            dicFiles.Remove(filePath.PathWithoutPackage);

            return true;
        }
    }
}
