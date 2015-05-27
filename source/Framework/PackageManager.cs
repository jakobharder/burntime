using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform.IO;

namespace Burntime.Framework
{
    public class PackageManager
    {
        string basePath;
        PackageInfo[] packageInfos;
        string[] modules;
        List<string> languages;
        bool hasFolderPackages;

        // package infos for all packages in game/
        public PackageInfo[] PackageInfos
        {
            get { return packageInfos; }
        }

        public string[] Languages
        {
            get { return languages.ToArray(); }
        }

        // returns true if there are non-pak packages in game/
        public bool HasFolderPackages
        {
            get { return hasFolderPackages; }
        }

        public PackageManager(string basePath)
        {
            this.basePath = FileSystem.BasePath + basePath;
            languages = new List<string>();

            ReadPackageInfos();
            modules = new string[0];
        }

        public void LoadPackages(string game, PackageSystem vfs, AssemblyControl assembly)
        {
            Add(game, vfs, assembly, true);
        }

        public void LoadPackages(string game, PackageSystem vfs, AssemblyControl assembly, bool loadExtras)
        {
            Add(game, vfs, assembly, loadExtras);
        }

        public PackageInfo GetInfo(string package)
        {
            foreach (PackageInfo info in PackageInfos)
            {
                if (info.Package == package)
                    return info;
            }

            return null;
        }

        private void Add(String[] Pakets, PackageSystem vfs, AssemblyControl assembly, bool loadExtras)
        {
            foreach (String pak in Pakets)
                Add(pak, vfs, assembly, loadExtras);
        }

        private void Add(string package, PackageSystem vfs, AssemblyControl assembly, bool loadExtras)
        {
            if (package.ToLower() == "burntime")
            {
                Burntime.Common.BurntimePath path = new Burntime.Common.BurntimePath(FileSystem.BasePath + "system/");
                string burntimePackage = path.Path;
                if (burntimePackage.EndsWith(".pak"))
                    burntimePackage = burntimePackage.Substring(0, burntimePackage.Length - 4);
                
                // make absolute path to avoid using basePath from FileSystem
                IPackage burntime = FileSystem.OpenPackage(System.IO.Path.GetFullPath(FileSystem.BasePath + "system/" + burntimePackage), "BURN_GFX/");
                vfs.Mount(package, burntime);
                return;
            }

            PackageInfo info = new PackageInfo(basePath + package, vfs);

            if (info.Type == PackageType.Game)
            {
                Add(info.Dependencies, vfs, assembly, loadExtras);

                if (!vfs.ExistsMount(package))
                {
                    PackageFactory factory = new PackageFactory();
                    IPackage p = factory.OpenPackage(basePath + package);
                    vfs.Mount(package, p);
                }

                // add language codes to available language list
                foreach (string lang in info.Languages)
                {
                    if (!languages.Contains(lang))
                        languages.Add(lang);
                }

                ApplyPatches(info, vfs);

                if (loadExtras)
                {
                    LoadLanguage(info, vfs, "");
                    LoadExtras(info, vfs);
                }

                if (assembly != null)
                    assembly.Load(info.Modules, package);
            }
        }

        private string[] GetAllPaks()
        {
            PackageFactory factory = new PackageFactory();
            string[] folder = System.IO.Directory.GetDirectories(basePath);

            // add all package folder
            List<string> paks = new List<string>();
            foreach (string str in folder)
            {
                if (factory.IsValidPackage(str))
                {
                    paks.Add(System.IO.Path.GetFileName(str));

                    // set folder packages to true
                    hasFolderPackages = true;
                }
            }

            // add all .paks
            string[] files = System.IO.Directory.GetFiles(basePath, "*.pak");
            foreach (string str in files)
            {
                // cut extension
                string name = str.Substring(0, str.Length - 4);

                if (factory.IsValidPackage(name))
                {
                    name = System.IO.Path.GetFileName(name);

                    // do not add if folder package with same name is already added
                    if (!paks.Contains(name))
                        paks.Add(name);
                }
            }

            return paks.ToArray();
        }

        private void ReadPackageInfos()
        {
            string[] paks = GetAllPaks();
            List<PackageInfo> list = new List<PackageInfo>();
            foreach (string str in paks)
            {
                PackageInfo info = new PackageInfo(basePath + str, FileSystem.VFS);
                list.Add(info);
            }

            packageInfos = list.ToArray();
        }

        void ApplyPatches(PackageInfo package, PackageSystem vfs)
        {
            Version currentVersion = package.Version;
            Version nextVersion = currentVersion;

            List<PackageInfo> applyList = new List<PackageInfo>();

            // search latest patch version
            PackageInfo best = null;

            do
            {
                best = null;

                foreach (PackageInfo info in PackageInfos)
                {
                    if (info.GameName == package.Package)
                    {
                        if (info.Type == PackageType.Patch)
                        {
                            if (info.BaseVersion == currentVersion && info.Version > nextVersion)
                            {
                                best = info;
                                nextVersion = info.Version;
                            }
                        }
                    }
                }

                if (best != null)
                {
                    currentVersion = nextVersion;
                    applyList.Add(best);
                }
            } while (best != null);

            // apply patches
            foreach (PackageInfo info in applyList)
            {
                if (!vfs.ExistsMount(info.Package))
                {
                    PackageFactory factory = new PackageFactory();
                    IPackage p = factory.OpenPackage(basePath + info.Package);
                    vfs.Mount(info.Package, p);
                }
            }
        }

        void LoadExtras(PackageInfo game, PackageSystem vfs)
        {

        }

        public void LoadLanguage(PackageInfo game, PackageSystem vfs, string subPath)
        {
            // load languages
            foreach (PackageInfo info in PackageInfos)
            {
                if (info.GameName == game.GameName)
                {
                    if (info.Type == PackageType.Language)
                    {
                        if (!vfs.ExistsMount(info.Package))
                        {
                            PackageFactory factory = new PackageFactory();
                            IPackage package = factory.OpenPackage(basePath + info.Package, subPath);
                            vfs.Mount(info.Package, package);

                            ApplyPatches(info, vfs);
                        }

                        // add language codes to available language list
                        foreach (string lang in info.Languages)
                        {
                            if (!languages.Contains(lang))
                                languages.Add(lang);
                        }
                    }
                }
            }
        }
    }
}
