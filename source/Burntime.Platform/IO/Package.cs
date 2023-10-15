namespace Burntime.Platform.IO;

public enum FileOpenMode
{
    Read = 0,
    Write = 1,
    NoPackage = 2
}

public interface IPackage
{
    string Name { get; }
    ICollection<string> Files { get; }

    File GetFile(FilePath filePath, FileOpenMode mode);
    bool ExistsFile(FilePath filePath);
    bool AddFile(FilePath filePath);
    bool RemoveFile(FilePath filePath);
    void Close();
}
