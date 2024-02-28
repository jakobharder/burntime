using Burntime.Platform.IO;

namespace Burntime.Platform;

public enum GamePackageType
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
    GamePackageType type;
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

    public GamePackageType Type
    {
        get { return type; }
    }

    public string GameName
    {
        get { return game; }
    }

    static public PackageInfo? TryCreate(string packageName, IO.File? file)
    {
        if (file is null)
            return null;

        ConfigFile config = new();
        config.Open(file);

        PackageInfo info = new()
        {
            package = packageName,
            dependencies = config[""].GetStrings("dependencies"),
            mainModule = config[""].GetString("start"),
            modules = config[""].GetStrings("modules"),
            languages = config[""].GetStrings("language"),
            version = config[""].GetVersion("version"),
            baseVersion = config[""].GetVersion("base")
        };
        switch (config[""].Get("type"))
        {
            case "patch":
                info.type = GamePackageType.Patch;
                break;
            case "game":
                info.type = GamePackageType.Game;
                break;
            case "language":
                info.type = GamePackageType.Language;
                break;
            default:
                info.type = GamePackageType.Data;
                break;
        }
        info.game = config[""].GetString("game");
        info.hidden = config[""].GetBool("hidden");
        return info;
    }

    static public PackageInfo? TryCreate(string inFileName, PackageSystem inVFS)
    {
        string packageName = System.IO.Path.GetFileName(inFileName);

        Burntime.Platform.IO.File file;
        // open file from package if available
        if (inVFS.ExistsMount(packageName))
            file = inVFS.GetFile(packageName + ":info.txt", FileOpenMode.Read);
        // open file without loading the package
        else
            file = inVFS.GetFile(inFileName + ":info.txt", FileOpenMode.NoPackage);

        return TryCreate(packageName, file);
    }

    private PackageInfo()
    {
    }
}
