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
    void Close();

    // not supported by all packages
    bool RemoveFile(FilePath filePath) => false;
    bool ExistsFolder(FilePath folderPath) => false;
    bool RemoveFolder(FilePath folderPath) => false;
    bool MoveFolder(FilePath sourcePath, FilePath targetPath) => false;
}
