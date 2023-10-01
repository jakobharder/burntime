using Burntime.Core;
using Burntime.Platform.IO;

namespace Burntime.Framework;

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
        this.basePath = System.IO.Path.Combine(FileSystem.BasePath, basePath);
        languages = new List<string>();

        ReadPackageInfos();
        modules = new string[0];
    }

    public bool LoadPackages(string game, PackageSystem vfs, IAssemblyControl assembly)
    {
        return Add(game, vfs, assembly, true);
    }

    public bool LoadPackages(string game, PackageSystem vfs, IAssemblyControl assembly, bool loadExtras)
    {
        return Add(game, vfs, assembly, loadExtras);
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

    private void Add(String[] Pakets, PackageSystem vfs, IAssemblyControl assembly, bool loadExtras)
    {
        foreach (String pak in Pakets)
            Add(pak, vfs, assembly, loadExtras);
    }

    private bool Add(string package, PackageSystem vfs, IAssemblyControl assembly, bool loadExtras)
    {
        if (package.ToLower() == "burntime")
        {
            //Burntime.Common.BurntimePath path = new Burntime.Common.BurntimePath(FileSystem.BasePath + "system/");
            //while (!path.IsValid)
            //{
            //    if (!path.ShowSelector())
            //        Environment.Exit(0);
            //    if (path.IsValid)
            //        path.Save();
            //}

            //string burntimePackage = path.Path;
            string burntimePackage = "..\\game\\burntime.pak";
            if (burntimePackage.EndsWith(".pak"))
                burntimePackage = burntimePackage.Substring(0, burntimePackage.Length - 4);

            // make absolute path to avoid using basePath from FileSystem
            string absolutePath = burntimePackage;
            if (!System.IO.Path.IsPathRooted(absolutePath))
                absolutePath = System.IO.Path.GetFullPath(FileSystem.BasePath + "system/" + absolutePath);
            IPackage burntime = FileSystem.OpenPackage(absolutePath, "BURN_GFX/");
            if (burntime == null)
            {
                // something went wrong
                throw new Exception("BURN_GFX folder was not found. Please make sure to set the correct path in system/path.txt to where the BURN_GFX and BURN.EXE are!");
            }

            vfs.Mount(package, burntime);
            return true;
        }

        PackageInfo info = PackageInfo.TryCreate(basePath + package, vfs);
        if (info == null)
            return false;

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

        return true;
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
            PackageInfo info = PackageInfo.TryCreate(basePath + str, FileSystem.VFS);
            if (info != null)
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
