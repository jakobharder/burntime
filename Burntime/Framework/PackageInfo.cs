using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform.IO;

namespace Burntime.Framework
{
    public enum PackageType
    {
        Game,
        Patch,
        Data,
        Language
    }

    public class PackageInfo
    {
        string[] dependencies;
        string[] modules;
        string[] languages;
        string mainModule;
        PackageType type;
        Version version = new Version();
        Version baseVersion = new Version();
        string game;
        string package;
        bool hidden;

        public string Package
        {
            get { return package; }
        }

        public string[] Dependencies
        {
            get { return dependencies; }
        }

        public string MainModule
        {
            get { return mainModule; }
        }

        public string[] Modules
        {
            get { return modules; }
        }

        public string[] Languages
        {
            get { return languages; }
        }

        public Version Version
        {
            get { return version; }
        }

        public Version BaseVersion
        {
            get { return baseVersion; }
        }

        public bool IsHidden
        {
            get { return hidden; }
        }

        public PackageType Type
        {
            get { return type; }
        }

        public string GameName
        {
            get { return game; }
        }

        public PackageInfo(string fileName, PackageSystem vfs)
        {
            package = System.IO.Path.GetFileName(fileName);

            File file;
            // open file from package if available
            if (vfs.ExistsMount(package))
                file = vfs.GetFile(package + ":info.txt", FileOpenMode.Read);
            // open file without loading the package
            else
                file = vfs.GetFile(fileName + ":info.txt", FileOpenMode.NoPackage);
            
            if (file != null)
            {
                ConfigFile config = new ConfigFile();
                config.Open(file);

                dependencies = config[""].GetStrings("dependencies");
                mainModule = config[""].GetString("start");
                modules = config[""].GetStrings("modules");
                languages = config[""].GetStrings("language");
                version = config[""].GetVersion("version");
                baseVersion = config[""].GetVersion("base");
                switch (config[""].Get("type"))
                {
                    case "patch":
                        type = PackageType.Patch;
                        break;
                    case "game":
                        type = PackageType.Game;
                        break;
                    case "language":
                        type = PackageType.Language;
                        break;
                    default:
                        type = PackageType.Data;
                        break;
                }
                game = config[""].GetString("game");
                hidden = config[""].GetBool("hidden");
            }
            else
            {
                dependencies = new string[0];
                mainModule = "";
                modules = new string[0];
                languages = new string[0];
                version = new Version();
                baseVersion = new Version();
                type = PackageType.Data;
                game = "";
                hidden = false;
            }
        }
    }
}
