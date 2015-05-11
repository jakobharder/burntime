using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Framework;
using Burntime.Platform.IO;

namespace Burntime.Launcher
{
    class GamePackage : IDisposable
    {
        string title;
        PackageSystem vfs;
        string temporaryPath;
        Version version;
        string name;

        ConfigFile packageInfo;
        ConfigFile launcherInfo;
        ConfigFile userSettings;

        // game title for tab
        public string Title
        {
            get { return title; }
        }

        public string DataPath
        {
            get { return temporaryPath; }
        }

        public IPackage Package
        {
            get { return vfs.GetPackage("launcher"); }
        }

        public Version Version
        {
            get { return version; }
        }

        // package name
        public string Name
        {
            get { return name; }
        }

        // user settings
        public ConfigFile UserSettings
        {
            get { return userSettings; }
        }

        public GamePackage(PackageInfo info)
        {
            name = info.Package;
            string path = "game/" + info.Package;

            PackageManager pakman = new PackageManager("game/");

            vfs = new PackageSystem();

            // load package without language and extras
            pakman.LoadPackages(info.Package, vfs, null, false);

            // get package info
            packageInfo = new ConfigFile();
            packageInfo.Open(vfs.GetFile("info.txt", FileOpenMode.Read));

            // load language packages
            pakman.LoadLanguage(info, vfs, "");

            if (name == Program.EnginePackage)
            {
                vfs.Mount("user", FileSystem.GetUserPackage("Burntime"));
            }
            else
            {
                vfs.Mount("user", FileSystem.GetUserPackage("Burntime/" + name));
            }

            launcherInfo = new ConfigFile();
            launcherInfo.Open(vfs.GetFile("launcher/info.txt", FileOpenMode.Read));

            title = launcherInfo[""].Get("title");
            version = packageInfo[""].GetVersion("version");

            ReadSettings();

            DownloadPageToTemporary(false);
        }

        public void RefreshData()
        {
            vfs.LocalizationCode = Program.VFS.LocalizationCode;

            DownloadPageToTemporary(true);
        }

        public void ReadSettings()
        {
            userSettings = new ConfigFile();

            File file = vfs.GetFile("settings.txt", FileOpenMode.Read);
            if (file != null)
                userSettings.Open(file);
        }

        public void SaveSettings()
        {
            File file = vfs.GetFile("user:settings.txt", FileOpenMode.Write);
            if (file != null)
                userSettings.Save(file);
        }

        public void Dispose()
        {
            // remove temporary data
            //RemoveTemporary();
        }

        void DownloadPageToTemporary(bool refresh)
        {
            temporaryPath = System.IO.Path.GetFullPath(Program.TemporaryPath + "/" + name);
            if (!System.IO.Directory.Exists(temporaryPath))
                System.IO.Directory.CreateDirectory(temporaryPath);

            // download launcher files
            foreach (string filename in vfs.GetAllFiles("launcher/", ""))
            {
                if (filename.StartsWith("."))
                    continue;

                File file = vfs.GetFile("launcher/" + filename, FileOpenMode.Read);
                System.IO.FileStream stream = new System.IO.FileStream(temporaryPath + "/" + filename, System.IO.FileMode.Create);

                byte[] buf = new byte[2048];
                int remaining = file.Length;

                while (remaining > 0)
                {
                    int read = file.Read(buf, System.Math.Min(buf.Length, remaining));
                    stream.Write(buf, 0, read);
                    remaining -= read;
                }

                stream.Close();
                file.Close();
            }
        }

        //void RemoveTemporary()
        //{
        //    System.IO.Directory.Delete(temporaryPath, true);
        //}
    }
}
