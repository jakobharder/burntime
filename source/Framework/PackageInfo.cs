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

        static public PackageInfo TryCreate(string inFileName, PackageSystem inVFS)
        {
            string package = System.IO.Path.GetFileName(inFileName);

            File file;
            // open file from package if available
            if (inVFS.ExistsMount(package))
                file = inVFS.GetFile(package + ":info.txt", FileOpenMode.Read);
            // open file without loading the package
            else
                file = inVFS.GetFile(inFileName + ":info.txt", FileOpenMode.NoPackage);

            if (file != null)
            {
                ConfigFile config = new ConfigFile();
                config.Open(file);

                PackageInfo info = new PackageInfo();
                info.package = package;
                info.dependencies = config[""].GetStrings("dependencies");
                info.mainModule = config[""].GetString("start");
                info.modules = config[""].GetStrings("modules");
                info.languages = config[""].GetStrings("language");
                info.version = config[""].GetVersion("version");
                info.baseVersion = config[""].GetVersion("base");
                switch (config[""].Get("type"))
                {
                    case "patch":
                        info.type = PackageType.Patch;
                        break;
                    case "game":
                        info.type = PackageType.Game;
                        break;
                    case "language":
                        info.type = PackageType.Language;
                        break;
                    default:
                        info.type = PackageType.Data;
                        break;
                }
                info.game = config[""].GetString("game");
                info.hidden = config[""].GetBool("hidden");
                return info;
            }

            return null;
        }

        private PackageInfo()
        {
        }
    }
}
