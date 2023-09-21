namespace Burntime.Platform.IO;

public class FileSystem
{
    // package system 
    static PackageSystem vfs = new PackageSystem();
    //// collection of pak files/folders
    //static Dictionary<string, IPackage> dicPackages = new Dictionary<string, IPackage>();

    //// collection of all virtual files
    //static Dictionary<string, string> dicFiles = new Dictionary<string, string>();
    //// collection of files overwritten by user files
    //static Dictionary<string, string> dicUserFiles = new Dictionary<string, string>();

    // base path
    static string basePath = "";

    //// current default localization code
    //static string localizationCode = "";

    static public PackageSystem VFS
    {
        get { return vfs; }
    }

    // base path
    static public string BasePath
    {
        get { return basePath; }
        set { basePath = value ?? ""; }
    }

    // use localization
    static public bool UseLocalization
    {
        get { return vfs.Localized; }
        set { vfs.Localized = value; }
    }

    // current default localization code
    static public string LocalizationCode
    {
        get { return vfs.LocalizationCode; }
        set { vfs.LocalizationCode = value ?? ""; }
    }

    // resolve path to full unique virtual path
    static public string GetUniqueName(string name, string package)
    {
        string[] tokens = name.Split(new char[] { ':' });
        if (tokens.Length == 1)
        {
            if (!vfs.ExistsFile(tokens[0].ToLower()))
            {
                return package + ":" + name;
            }
            else
                return name;
        }

        return name;
    }

    // add virtual folder
    static public void AddPackage(string name, string path)
    {
        IPackage package = OpenPackage(path);
        if (package == null)
        {
            Log.Warning("Cannot find package: " + path.ToLower());
            return;
        }

        Log.Info("Add file system package: " + name.ToLower());

        vfs.Mount(name, package);
    }

    static public void AddPackage(string name, IPackage package)
    {
        vfs.Mount(name, package);
    }

    // open package from folder or .pak file
    static public IPackage OpenPackage(string path)
    {
        return OpenPackage(path, "");
    }

    // open package from folder or .pak file
    static public IPackage OpenPackage(string path, string subPath)
    {
        PackageFactory factory = new PackageFactory();

        return factory.OpenPackage(GetBasedPath(path), subPath);
    }

    // remove user package
    static void RemoveUserPackage()
    {
        vfs.Unmount("user");
    }

    static public IPackage GetUserPackage(string gameName)
    {
        //string userPath = (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX) ? 
        //    Environment.GetEnvironmentVariable("HOME") : Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");

        string userPath;

        // for windows vista and above
        if (Environment.OSVersion.Version.Major >= 6)
            userPath = Environment.GetEnvironmentVariable("USERPROFILE") + "/Saved games";
        // for windows xp
        else
            userPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/My games";

        // create user folder
        if (!System.IO.Directory.Exists(userPath))
            System.IO.Directory.CreateDirectory(userPath);
        if (!System.IO.Directory.Exists(userPath + "/" + gameName))
            System.IO.Directory.CreateDirectory(userPath + "/" + gameName);

        return OpenPackage(userPath + "/" + gameName);
    }

    // add user folder
    static public void SetUserFolder(string gameName)
    {
        // remove previous user folder
        vfs.Unmount("user");

        // add new user folder
        vfs.Mount("user", GetUserPackage(gameName));
    }

    // get all filenames in specified virtual location
    static public string[] GetFileNames(FilePath path, string filter)
    {
        return vfs.GetAllFiles(path, filter);
    }

    //// search for localized files and return most preferable path
    //static public FilePath GetLocalizedFilePath(FilePath path)
    //{
    //    if (!path.IsValid)
    //        return path;

    //    string package = "";
    //    string parameter = "";
    //    if (path.PackageSpecified)
    //        package += path.Package + ":";
    //    if (path.HasParameter)
    //        parameter += path.Parameter;

    //    if (ExistsFile(package + "lang/" + localizationCode + "/" + path.PathWithoutPackage, false))
    //        return package + "lang/" + localizationCode + "/" + path.PathWithoutPackage + parameter;
    //    if (ExistsFile(package + path.Folder + "/" + localizationCode + path.FileName, false))
    //        return package + path.Folder + "/" + localizationCode + path.FileName + parameter;
    //    if (ExistsFile(package + path.Folder + path.FileNameWithoutExtension + "-" + localizationCode + "." + path.Extension, false))
    //        return package + path.Folder + path.FileNameWithoutExtension + "-" + localizationCode + "." + path.Extension + parameter;
    //    if (ExistsFile(path, false))
    //        return path;
        
    //    return path;
    //}

    // check file existance (also check for possible localizations)
    static public bool ExistsFile(FilePath path)
    {
        return vfs.ExistsFile(path);
    }

    //// check file existance
    //static public bool ExistsFile(FilePath path, bool allowLocalization)
    //{
    //    if (!path.IsValid)
    //        return false;

    //    if (allowLocalization && localizationCode.Length > 0)
    //    {
    //        string package = "";
    //        if (path.PackageSpecified)
    //            package += path.Package + ":";
    //        if (ExistsFile(package + "lang/" + localizationCode + "/" + path.PathWithoutPackage, false))
    //            return true;
    //        if (ExistsFile(package + path.Folder + "/" + localizationCode + path.FileName, false))
    //            return true;
    //        if (ExistsFile(package + path.Folder + path.FileNameWithoutExtension + "-" + localizationCode + "." + path.Extension, false))
    //            return true;
    //        return ExistsFile(path, false);
    //    }
    //    else
    //    {
    //        IPackage package;
    //        if (path.PackageSpecified)
    //        {
    //            if (!dicPackages.ContainsKey(path.Package.ToLower()))
    //                return false;
    //            package = dicPackages[path.Package.ToLower()];
    //        }
    //        else
    //        {
    //            if (!dicFiles.ContainsKey(path.Path.ToLower()))
    //                return false;
    //            package = dicPackages[dicFiles[path.Path.ToLower()]];
    //        }

    //        return package.ExistsFile(path);
    //    }
    //}

    // return virtual file (readonly)
    static public File GetFile(FilePath path)
    {
        return GetFile(path, FileOpenMode.Read);
    }

    // return virtual file
    static public File GetFile(FilePath path, FileOpenMode mode)
    {
        if (!path.IsValid)
        {
            Log.Warning("File path invalid: " + path);
            return null;
        }

        if ((mode & FileOpenMode.NoPackage) == FileOpenMode.NoPackage)
        {
            PackageFactory factory = new PackageFactory();
            return factory.OpenFileDirectly(GetBasedPath(path.Package), path.PathWithoutPackage, mode);
        }
        else
        {
            if (mode == FileOpenMode.Write)
            {
                // if we need write access then always use the user package
                path.Package = "user";
            }

            if (!path.PackageSpecified)
            {
                File file = vfs.GetFile(path, mode);
                if (file == null)
                    Log.Warning("File not found: " + path);

                return file;
            }
            else
            {
                return vfs.GetFile(path, mode);
            }
        }

            //    string[] token = path.Package.Split(new char[] { '\\', '/' });
            //if (!dicPackages.ContainsKey(token[token.Length - 1].ToLower()))
            //{
            //    if (PackagePak.IsPak(basePath + path.Package))
            //    {
            //        PackagePak pak = new PackagePak("", basePath + path.Package);
            //        File file = pak.GetFile(path.PathWithoutPackage, false);
            //        pak.Close();

            //        return file;
            //    }
            //    else
            //    {
            //        SystemFile file = new SystemFile(basePath + path.Package + "/" + path.PathWithoutPackage, path, false);
            //        return file;
            //    }
            //}

            //IPackage p = dicPackages[token[token.Length - 1].ToLower()];
            //return p.GetFile(path, WriteAccess);
    }

    // create virtual file
    static public File CreateFile(FilePath path)
    {
        // default package to user path
        path.Package = "user";

        if (!FileSystem.ExistsFile(path))
            FileSystem.AddFile(path);
        return FileSystem.GetFile(path, FileOpenMode.Write);
    }

    // add virtual file
    static public bool AddFile(FilePath path)
    {
        return vfs.AddFile(path);
    }

    // remove virtual file
    static public bool RemoveFile(FilePath path)
    {
        return vfs.RemoveFile(path);
    }

    // check if virtual folder is loaded
    static public bool IsPackageLoaded(string package)
    {
        return vfs.ExistsMount(package);
    }

    // unload all virtual folders
    static public void Clear()
    {
        vfs.UnmountAll();
    }

    public delegate void ConvertFeedback(float percentage);

    static public void ConvertFolderToPak(string path, ConvertFeedback feedback)
    {
        if (feedback != null)
            feedback(0);

        IPackage package = new PackageFolder("test", path, "");

        string outputPath = path + ".pak";

        System.IO.FileStream stream = new System.IO.FileStream(outputPath, System.IO.FileMode.Create);
        stream.SetLength(0);
        System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream);

        // calculate positions
        int position = 0;
        List<PakFileInfo> infos = new List<PakFileInfo>();
        foreach (string fileName in package.Files)
        {
            // exclude pdb files and files starting with .
            if (fileName.StartsWith(".") || fileName.EndsWith(".pdb", StringComparison.InvariantCultureIgnoreCase))
                continue;

            File file = package.GetFile(fileName, FileOpenMode.Read);
            PakFileInfo info = new PakFileInfo();
            info.Name = fileName;
            info.Position = position;
            info.Length = (int)file.Stream.Length;

            position += info.Length;
            file.Close();

            infos.Add(info);
        }

        PakHeader header = new PakHeader();
        header.FileCount = infos.Count;
        header.Version = 00010; //0.00.10
        header.Type = PackageType.Main;

        // write header
        writer.Write(header.FileCount);
        writer.Write(header.Version);
        writer.Write((int)header.Type);

        foreach (PakFileInfo info in infos)
        {
            writer.Write(info.Position);
            writer.Write(info.Length);
            writer.Write(info.Name);
        }

        int written = 0;

        // write files
        foreach (PakFileInfo info in infos)
        {
            File file = package.GetFile(info.Name, FileOpenMode.Read);

            int remaining = (int)file.Stream.Length;

            byte[] buf = new byte[1024 * 1024 * 4];
            while (remaining > 0)
            {
                int copy = System.Math.Min(buf.Length, remaining);
                file.Stream.Read(buf, 0, copy);
                stream.Write(buf, 0, copy);
                remaining -= copy;
            }

            written += (int)file.Stream.Length;
            file.Close();
            if (feedback != null)
                feedback(written / (float)position);
        }

        // finish
        writer.Flush();
        writer.Close();
        stream.Close();
    }

    // if path is not absolute then make it relative to base path
    static private string GetBasedPath(string path)
    {
        if (!System.IO.Path.IsPathRooted(path))
            return basePath + path;
        return path;
    }

    // for debug
    static private string MountsString
    {
        get
        {
            string str = "";
            foreach (string mount in vfs.Mounts)
                str += mount + " ";
            return str.Trim();
        }
    }
}
