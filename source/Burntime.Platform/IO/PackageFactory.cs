namespace Burntime.Platform.IO;

public class PackageFactory
{
    // path must be relative to current directory
    public IPackage OpenPackage(string path)
    {
        return OpenPackage(path, "");
    }

    public IPackage? OpenPackage(string path, string subPath)
    {
        IPackage? folderPackage = null;
        IPackage? pakPackage = null;

        if (!subPath.EndsWith("/") && subPath != "")
            subPath += "/";

        if (System.IO.Directory.Exists(path))
            folderPackage = new PackageFolder("", path, subPath);
        if (System.IO.File.Exists(path + ".pak"))
            pakPackage = new PackagePak("", path, subPath);

        
        if (folderPackage is not null && pakPackage is not null)
        {
            var folderVersion = PackageInfo.TryCreate(folderPackage.Name, folderPackage.GetFile("info.txt", FileOpenMode.Read));
            var pakVersion = PackageInfo.TryCreate(pakPackage.Name, pakPackage.GetFile("info.txt", FileOpenMode.Read));
            if (folderVersion?.Version >= pakVersion?.Version)
            {
                // prefer folder only when pak is not higher version
                pakPackage.Close();
                return folderPackage;
            }

            folderPackage.Close();
            return pakPackage;
        }
        else if (folderPackage is not null)
            return folderPackage;
        else if (pakPackage is not null)
            return pakPackage;


        return null;
    }

    public File? OpenFileDirectly(string packagePath, string filePath, FileOpenMode mode)
    {
        File? file = null;

        if ((mode & FileOpenMode.Write) != FileOpenMode.Write)
        {
            var package = OpenPackage(packagePath, "");
            if (package is not null)
            {
                file = package.GetFile(filePath, FileOpenMode.Read);
                package.Close();
            }
        }
        else if ((mode & FileOpenMode.Write) == FileOpenMode.Write ||
            System.IO.File.Exists(packagePath + (packagePath != "" ? "/" : "") + filePath))
        {
            file = new SystemFile(packagePath + (packagePath != "" ? "/" : "") + filePath, packagePath + ":" + filePath, (mode & FileOpenMode.Write) == FileOpenMode.Write);
        }

        return file;
    }

    public bool IsValidPackage(string path)
    {
        // first try as pak packages
        if (System.IO.File.Exists(path + ".pak"))
            return true;

        // then try as folder packages
        else if (System.IO.Directory.Exists(path))
        {
            if (System.IO.File.Exists(path + "/info.txt"))
            {
                return true;
            }
        }

        return false;
    }
}
