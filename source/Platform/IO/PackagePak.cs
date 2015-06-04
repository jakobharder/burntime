
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
using System.IO;
using ManagedZLib;

namespace Burntime.Platform.IO
{
    public enum PackageType
    {
        Main
    }

    struct PakHeader
    {
        public int FileCount;
        public int Version;
        public PackageType Type;
    }

    struct PakFileInfo
    {
        public int Position;
        public int Length;
        public string Name;
    }

    class PackagePak : IPackage
    {
        PakHeader header;
        Dictionary<string, PakFileInfo> dicFiles;
        FileStream stream;
        string path;
        string name;
        int basePos;
        string subPath;

        public ICollection<string> Files
        {
            get { return dicFiles.Keys; }
        }

        public string Name
        {
            get { return name; }
        }

        public PackagePak(string name, string path)
            : this(name, path, "")
        { 
        }

        public PackagePak(string name, string path, string subPath)
        {
            if (!path.EndsWith(".pak", StringComparison.InvariantCultureIgnoreCase))
                path += ".pak";

            this.path = path;
            this.name = name;
            this.subPath = subPath.ToLower();

            dicFiles = new Dictionary<string, PakFileInfo>();
            process(path);
        }

        public void Close()
        {
            if (stream != null)
                stream.Close();
        }

        void process(string path)
        {
            try
            {
                stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                BinaryReader reader = new BinaryReader(stream);
                
                header.FileCount = reader.ReadInt32();
                header.Version = reader.ReadInt32();
                header.Type = (PackageType)reader.ReadInt32();

                for (int i = 0; i < header.FileCount; i++)
                {
                    PakFileInfo info = new PakFileInfo();
                    info.Position = reader.ReadInt32();
                    info.Length = reader.ReadInt32();
                    info.Name = reader.ReadString().ToLower();

                    if (info.Name.StartsWith(subPath))
                    {
                        info.Name = info.Name.Substring(subPath.Length);
                        dicFiles.Add(info.Name, info);
                    }
                }

                basePos = (int)stream.Position;
            }
            catch
            {
                return;
            }

        }

        public File GetFile(FilePath filePath, FileOpenMode mode)
        {
            if (mode != FileOpenMode.Read)
                throw new InvalidOperationException();

            // lock to only one thread at a time
            lock (this)
            {
                if (!dicFiles.ContainsKey(filePath.PathWithoutPackage))
                    return null;

                PakFileInfo info = dicFiles[filePath.PathWithoutPackage];

                MemoryStream fileInMemory = new MemoryStream();
                stream.Seek(info.Position + basePos, SeekOrigin.Begin);

                int remaining = info.Length;
                byte[] buf = new byte[1024 * 4];
                while (remaining > 0)
                {
                    int copy = System.Math.Min(buf.Length, remaining);
                    remaining -= copy;

                    stream.Read(buf, 0, copy);
                    fileInMemory.Write(buf, 0, copy);
                }

                fileInMemory.Seek(0, SeekOrigin.Begin);

                return new File(fileInMemory, info.Name);
            }
        }

        public bool ExistsFile(FilePath filePath)
        {
            return dicFiles.ContainsKey(filePath.PathWithoutPackage);
        }

        public bool AddFile(FilePath filePath)
        {
            throw new NotSupportedException();
        }

        public bool RemoveFile(FilePath filePath)
        {
            throw new NotSupportedException();
        }
    }
}
