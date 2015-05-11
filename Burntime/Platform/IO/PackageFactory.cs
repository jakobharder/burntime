using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Platform.IO
{
    public class PackageFactory
    {
        // path must be relative to current directory
        public IPackage OpenPackage(string path)
        {
            return OpenPackage(path, "");
        }

        public IPackage OpenPackage(string path, string subPath)
        {
            IPackage p = null;

            if (!subPath.EndsWith("/") && subPath != "")
                subPath += "/";

            // first try as folder packages
            if (System.IO.Directory.Exists(path))
                p = new PackageFolder("", path, subPath);

            // then try as pak packages
            else if (System.IO.File.Exists(path + ".pak"))
                p = new PackagePak("", path, subPath);


            return p;
        }

        public File OpenFileDirectly(string packagePath, string filePath, FileOpenMode mode)
        {
            File file = null;

            if ((mode & FileOpenMode.Write) != FileOpenMode.Write && System.IO.File.Exists(packagePath))
            {
                PackagePak pak = new PackagePak("", packagePath);
                file = pak.GetFile(filePath, mode & FileOpenMode.Write);
                pak.Close();
            }
            else if ((mode & FileOpenMode.Write) != FileOpenMode.Write && System.IO.File.Exists(packagePath + ".pak"))
            {
                PackagePak pak = new PackagePak("", packagePath + ".pak");
                file = pak.GetFile(filePath, mode & FileOpenMode.Write);
                pak.Close();
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
}
