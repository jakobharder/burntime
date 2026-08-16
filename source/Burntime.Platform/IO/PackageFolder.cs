namespace Burntime.Platform.IO;

class PackageFolder : IPackage
{
    // Virtual paths are case-insensitive, but the physical path must retain its
    // original casing on case-sensitive file systems such as Linux.
    Dictionary<string, string> dicFiles;
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

        dicFiles = new Dictionary<string, string>();
        ParseFolder("", path + "/" + subPath);
    }

    public void Close()
    {
    }

    void ParseFolder(string relpath, string path)
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

                string relativePath = relpath + name;
                dicFiles.Add(relativePath.ToLowerInvariant(), relativePath);
            }

            string[] dirs = Directory.GetDirectories(path);
            foreach (string dir in dirs)
            {
                string name = System.IO.Path.GetFileName(dir);

                // skip directories beginning with .
                if (name.StartsWith("."))
                    continue;

                ParseFolder(relpath + name + "/", dir);
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

        if (!dicFiles.TryGetValue(filePath.PathWithoutPackage, out string physicalPath))
            return null;
        return new SystemFile(System.IO.Path.Combine(path, subPath, physicalPath), name + ":" + filePath.PathWithoutPackage, mode == FileOpenMode.Write);
    }

    public bool ExistsFile(FilePath filePath)
    {
        return dicFiles.ContainsKey(filePath.PathWithoutPackage);
    }

    public bool ExistsFolder(FilePath filePath)
    {
        return System.IO.Directory.Exists(path + "/" + subPath + filePath.PathWithoutPackage);
    }

    public bool AddFile(FilePath filePath)
    {
        if (dicFiles.ContainsKey(filePath.PathWithoutPackage))
            return false;

        try
        {
            string physicalPath = System.IO.Path.Combine(path, subPath, filePath.PathWithoutPackage);
            string directory = System.IO.Path.GetDirectoryName(physicalPath);
            if (!System.IO.Directory.Exists(directory))
                System.IO.Directory.CreateDirectory(directory);
            FileStream stream = new FileStream(physicalPath, FileMode.CreateNew);
            stream.Close();
        }
        catch
        {
            return false;
        }

        dicFiles.Add(filePath.PathWithoutPackage, filePath.PathWithoutPackage);

        return true;
    }

    public bool RemoveFile(FilePath filePath)
    {
        if (!dicFiles.ContainsKey(filePath.PathWithoutPackage))
            return false;

        try
        {
            System.IO.File.Delete(System.IO.Path.Combine(path, subPath, dicFiles[filePath.PathWithoutPackage]));
        }
        catch
        {
            return false;
        }

        dicFiles.Remove(filePath.PathWithoutPackage);

        return true;
    }

    public bool RemoveFolder(FilePath filePath)
    {
        try
        {
            System.IO.Directory.Delete(path + "/" + subPath + filePath.PathWithoutPackage, true);
        }
        catch
        {
            return false;
        }

        dicFiles.Clear();
        ParseFolder("", path + "/" + subPath);

        return true;
    }

    public bool MoveFolder(FilePath sourcePath, FilePath targetPath)
    {
        try
        {
            Directory.Move(path + "/" + subPath + sourcePath.PathWithoutPackage, path + "/" + subPath + targetPath.PathWithoutPackage);
        }
        catch
        {
            return false;
        }

        dicFiles.Clear();
        ParseFolder("", path + "/" + subPath);

        return true;
    }

}
