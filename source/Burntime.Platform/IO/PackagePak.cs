namespace Burntime.Platform.IO;

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
        // Archive handles are scoped to indexing and individual resource reads.
    }

    void process(string path)
    {
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using BinaryReader reader = new(stream);
            
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

        try
        {
            if (!dicFiles.ContainsKey(filePath.PathWithoutPackage))
                return null;

            PakFileInfo info = dicFiles[filePath.PathWithoutPackage];

            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            MemoryStream fileInMemory = new MemoryStream();
            stream.Seek(info.Position + basePos, SeekOrigin.Begin);

            int remaining = info.Length;
            byte[] buf = new byte[1024 * 4];
            while (remaining > 0)
            {
                int copy = System.Math.Min(buf.Length, remaining);
                int read = stream.Read(buf, 0, copy);
                if (read == 0)
                {
                    fileInMemory.Dispose();
                    return null;
                }
                remaining -= read;
                fileInMemory.Write(buf, 0, read);
            }

            fileInMemory.Seek(0, SeekOrigin.Begin);

            return new File(fileInMemory, info.Name);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
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
