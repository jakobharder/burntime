
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
    public class PackageSystem
    {
        private sealed class MountInfo
        {
            public string Name;
            public IPackage Package;
            public Dictionary<string, string> FileMap;
            public string DefaultLocalization;

            public MountInfo(string name, IPackage package, string defaultLocalization)
            {
                Name = name;
                Package = package;
                FileMap = new Dictionary<string, string>();
                DefaultLocalization = defaultLocalization;
            }

            public override string ToString()
            {
                return Name;
            }
        }

        private sealed class LinkInfo
        {
            public string FilePath;
            public MountInfo Mount;

            public LinkInfo(string filePath, MountInfo mount)
            {
                FilePath = filePath;
                Mount = mount;
            }

            public override string ToString()
            {
                return Mount.Name + ":" + FilePath;
            }
        }

        string localizationCode;
        List<MountInfo> mounts;
        Dictionary<string, MountInfo> nameMountMap;
        Dictionary<string, LinkInfo> fileMountMap;
        bool localized;

        public string LocalizationCode
        {
            get { return localizationCode; }
            set 
            { 
                localizationCode = value;
                RefreshFileMountMap();
            }
        }

        public bool Localized
        {
            get { return localized; }
            set
            {
                localized = value;
                RefreshFileMountMap();
            }
        }

        // return all mount names
        public string[] Mounts
        {
            get 
            {
                string[] mounts = new string[this.mounts.Count];
                for (int i = 0; i < this.mounts.Count; i++)
                {
                    mounts[i] = this.mounts[i].Name;
                }

                return mounts;
            }
        }

        public PackageSystem()
        {
            mounts = new List<MountInfo>();
            nameMountMap = new Dictionary<string, MountInfo>();
            fileMountMap = new Dictionary<string, LinkInfo>();
            localized = true;
        }

        public bool Mount(string name, IPackage package)
        {
            return Mount(name, package, "en");
        }

        public bool Mount(string name, IPackage package, string defaultLocalization)
        {
            if (package == null)
                return false;

            if (nameMountMap.ContainsKey(name))
                Unmount(name);

            MountInfo mount = new MountInfo(name, package, defaultLocalization);
            mounts.Add(mount);

            RefreshFileMountMap();
            return true;
        }

        public void Unmount(string name)
        {
            for (int i = 0; i < mounts.Count; i++)
            {
                if (mounts[i].Name == name)
                {
                    mounts.RemoveAt(i);
                    i--;
                }
            }

            RefreshFileMountMap();
        }

        public void Unmount(IPackage package)
        {
            for (int i = 0; i < mounts.Count; i++)
            {
                if (mounts[i].Package == package)
                {
                    mounts[i].Package.Close();
                    mounts.RemoveAt(i);
                    i--;
                }
            }

            RefreshFileMountMap();
        }

        // unmount all packages
        public void UnmountAll()
        {
            foreach (MountInfo mount in mounts)
            {
                mount.Package.Close();
            }

            mounts.Clear();
            RefreshFileMountMap();
        }

        // refresh call from outside
        public void Refresh(bool refreshPackages)
        {
            RefreshFileMountMap();
        }

        // check is mount name is already used
        public bool ExistsMount(string name)
        {
            foreach (MountInfo mount in mounts)
            {
                if (mount.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }

            return false;
        }

        // check if package is already mounted
        public bool ExistsPackage(IPackage package)
        {
            foreach (MountInfo mount in mounts)
            {
                if (mount.Package == package)
                    return true;
            }

            return false;
        }

        // get mounted package
        public IPackage GetPackage(string name)
        {
            foreach (MountInfo mount in mounts)
            {
                if (mount.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                    return mount.Package;
            }

            return null;
        }

        // return all files in a specific path
        public string[] GetAllFiles(FilePath path, string extensionFilter)
        {
            if (!path.IsValid)
                return new string[0];

            List<string> list = new List<string>();

            if (!path.PackageSpecified)
            {
                foreach (string str in fileMountMap.Keys)
                {
                    if (str.StartsWith(path.Path, StringComparison.OrdinalIgnoreCase) &&
                        str.EndsWith(extensionFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(str.Substring(path.Path.Length));
                    }
                }

                return list.ToArray();
            }
            else
            {
                if (!nameMountMap.ContainsKey(path.Package))
                    return null;

                MountInfo mount = nameMountMap[path.Package];

                foreach (string str in mount.FileMap.Keys)
                {
                    if (str.StartsWith(path.PathWithoutPackage, StringComparison.OrdinalIgnoreCase) &&
                        str.EndsWith(extensionFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(str.Substring(path.PathWithoutPackage.Length));
                    }
                }

                return list.ToArray();
            }
        }

        // check file existence
        public bool ExistsFile(FilePath path)
        {
            if (!path.IsValid)
                return false;

            if (path.PackageSpecified)
            {
                if (!nameMountMap.ContainsKey(path.Package))
                    return false;

                return nameMountMap[path.Package].FileMap.ContainsKey(path.PathWithoutPackage);
            }

            return fileMountMap.ContainsKey(path.PathWithoutPackage);
        }

        // get file from virtual file system
        public File GetFile(FilePath path, FileOpenMode mode)
        {
            if (!path.IsValid)
                return null;

            // package is specified, look up in package file map
            if (path.PackageSpecified)
            {
                if ((mode & FileOpenMode.NoPackage) == FileOpenMode.NoPackage)
                {
                    PackageFactory factory = new PackageFactory();
                    return factory.OpenFileDirectly(path.Package, path.PathWithoutPackage, mode);
                }
                else
                {
                    if (!nameMountMap.ContainsKey(path.Package))
                        return null;

                    // translate file name from global name to package name
                    if (nameMountMap[path.Package].FileMap.ContainsKey(path.PathWithoutPackage))
                    {
                        string filepath = nameMountMap[path.Package].FileMap[path.PathWithoutPackage];
                        return nameMountMap[path.Package].Package.GetFile(filepath, mode);
                    }
                    // if file not found and in write mode directly access package
                    else if (mode == FileOpenMode.Write)
                    {
                        nameMountMap[path.Package].Package.AddFile(path.PathWithoutPackage);
                        RefreshFileMountMap();
                        return nameMountMap[path.Package].Package.GetFile(path.PathWithoutPackage, mode);
                    }
                }
            }
            // package is not specified, look up in global map
            else
            {
                if ((mode & FileOpenMode.NoPackage) == FileOpenMode.NoPackage)
                    return null;

                if (fileMountMap.ContainsKey(path.PathWithoutPackage))
                {
                    LinkInfo link = fileMountMap[path.PathWithoutPackage];
                    if (link.FilePath == null)
                        return link.Mount.Package.GetFile(path.PathWithoutPackage, mode);
                    return link.Mount.Package.GetFile(link.FilePath, mode);
                }
            }

            return null;
        }

        public bool AddFile(FilePath path)
        {
            // always add new files in user path package
            if (path.PackageSpecified && path.Package != "user")
                return false;

            if (!ExistsMount("user"))
                return false;

            MountInfo mount = nameMountMap["user"];

            // add to package
            if (mount.Package.ExistsFile(path.PathWithoutPackage))
                return false;

            if (!mount.Package.AddFile(path.PathWithoutPackage))
                return false;

            RefreshFileMountMap();

            return true;
        }

        public bool RemoveFile(FilePath path)
        {
            // always remove files in user path package
            if (path.PackageSpecified && path.Package != "user")
                return false;

            if (!ExistsMount("user"))
                return false;

            MountInfo mount = nameMountMap["user"];

            // remove from package
            if (!mount.Package.ExistsFile(path.PathWithoutPackage))
                return false;

            if (!mount.Package.RemoveFile(path.PathWithoutPackage))
                return false;

            RefreshFileMountMap();

            return true;
        }

        // update all virtual file paths
        private void RefreshFileMountMap()
        {
            nameMountMap.Clear();
            fileMountMap.Clear();

            // refresh file list
            foreach (MountInfo mount in mounts)
            {
                Dictionary<string, bool> localizedFilesGlobal = new Dictionary<string, bool>();
                Dictionary<string, bool> localizedFilesMount = new Dictionary<string, bool>();

                // add to mount map
                nameMountMap.Add(mount.Name, mount);

                // refresh package file map
                mount.FileMap.Clear();

                // refresh global file map
                foreach (string str in mount.Package.Files)
                {
                    string localized;
                    string code;

                    // is localization file
                    if (this.localized && IsLocalization(str, out localized, out code))
                    {
                        bool isSpecified = code.Equals(localizationCode, StringComparison.InvariantCultureIgnoreCase);
                        bool isDefault = code.Equals(mount.DefaultLocalization, StringComparison.InvariantCultureIgnoreCase);

                        // skip other localizations
                        if (!isSpecified && !isDefault)
                            continue;

                        // remove if already in map from lower priority package or is unlocalized
                        if (fileMountMap.ContainsKey(localized))
                        {
                            // do not overwrite a localization with a default localization
                            bool overwrite = true;
                            if (isDefault && localizedFilesGlobal.ContainsKey(localized))
                                overwrite = false;

                            if (overwrite)
                            {
                                fileMountMap.Remove(localized);
                                fileMountMap.Add(localized, new LinkInfo(str, mount));

                                if (!localizedFilesGlobal.ContainsKey(localized))
                                    localizedFilesGlobal.Add(localized, isSpecified);
                            }
                        }
                        else
                        {
                            fileMountMap.Add(localized, new LinkInfo(str, mount));
                            if (!localizedFilesGlobal.ContainsKey(localized))
                                localizedFilesGlobal.Add(localized, isSpecified);
                        }

                        // remove if already in map from lower priority package or is unlocalized
                        if (mount.FileMap.ContainsKey(localized))
                        {
                            // do not overwrite a localization with a default localization
                            bool overwrite = true;
                            if (isDefault && localizedFilesMount.ContainsKey(localized))
                                overwrite = false;

                            if (overwrite)
                            {
                                mount.FileMap.Remove(localized);
                                mount.FileMap.Add(localized, str);

                                if (!localizedFilesMount.ContainsKey(localized))
                                    localizedFilesMount.Add(localized, isSpecified);
                            }
                        }
                        else
                        {
                            mount.FileMap.Add(localized, str);
                            if (!localizedFilesMount.ContainsKey(localized))
                                localizedFilesMount.Add(localized, isSpecified);
                        }
                    }                    
                    else
                    {
                        // only if is not already in map as a localization
                        if (!localizedFilesGlobal.ContainsKey(str))
                        {
                            // remove if already in map from lower priority package
                            if (fileMountMap.ContainsKey(str))
                                fileMountMap.Remove(str);
                            fileMountMap.Add(str, new LinkInfo(null, mount));
                        }

                        // only if is not already in map as a localization
                        if (!localizedFilesMount.ContainsKey(str))
                        {
                            // remove if already in map from lower priority package
                            if (mount.FileMap.ContainsKey(str))
                                mount.FileMap.Remove(str);
                            mount.FileMap.Add(str, str);
                        }
                    }
                }
            }
        }

        private bool IsLocalization(string path, out string localized, out string code)
        {
            // check localization pattern lang/EN/*
            if (path.StartsWith("lang/", StringComparison.InvariantCultureIgnoreCase))
            {
                code = path.Substring(5, 2);
                localized = path.Substring(8);
                return true;
            }

            // check localization pattern *-EN.*
            string nameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(path);
            if (nameWithoutExtension.Length > 3 && 
                nameWithoutExtension.Substring(nameWithoutExtension.Length - 3, 1) == "-")
            {
                code = nameWithoutExtension.Substring(nameWithoutExtension.Length - 2);
                int last = path.LastIndexOf("-" + code);
                localized = path.Substring(0, last) + path.Substring(last + 3);
                return true;
            }

            // no localization
            localized = path;
            code = "";

            return false;
        }
    }
}
