namespace Burntime.Platform.IO;

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
