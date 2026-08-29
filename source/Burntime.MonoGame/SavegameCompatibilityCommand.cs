using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Burntime.Data.BurnGfx;
using Burntime.Framework;
using Burntime.MonoGame.Resource;
using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Remaster;
using Burntime.Remaster.AI;

namespace Burntime.MonoGame;

internal static class SavegameCompatibilityCommand
{
    public static bool IsRequested(string[] args) =>
        args.Contains("--savegame-test", StringComparer.OrdinalIgnoreCase);

    public static int Run(string[] args)
    {
        try
        {
            (string fixturePath, int turns) = Parse(args);
            string fixtureRoot = Path.GetFullPath(fixturePath);
            if (!Directory.Exists(fixtureRoot))
                throw new ArgumentException($"Save-game fixture folder does not exist: {fixtureRoot}");

            string[] saveFiles = Directory.EnumerateFiles(
                    fixtureRoot, "*", SearchOption.AllDirectories)
                .Where(path => Path.GetExtension(path).Equals(
                    ".sav", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (saveFiles.Length == 0)
                throw new ArgumentException($"No .sav fixtures found below {fixtureRoot}");

            FileSystem.BasePath = AppContext.BaseDirectory;
            PackageManager packageManager = new("game/");
            packageManager.LoadPackages("classic", FileSystem.VFS, null);
            FileSystem.AddPackage("savegame-fixtures", fixtureRoot);

            LoadingCounter loadingCounter = new();
            using HeadlessResourceManager resources = new(loadingCounter);
            BurntimeClassic app = new();
            app.Initialize(resources);
            BurnGfxModule burnGfx = new();
            burnGfx.Initialize(resources);
            app.InitializeHeadless();

            int failures = 0;
            foreach (string saveFile in saveFiles)
            {
                string relativePath = Path.GetRelativePath(fixtureRoot, saveFile)
                    .Replace(Path.DirectorySeparatorChar, '/');
                try
                {
                    HeadlessSimulation.Run(app, new HeadlessSimulationOptions
                    {
                        Turns = turns,
                        Seed = 1,
                        LoadGamePath = $"savegame-fixtures:{relativePath}"
                    });
                    Console.WriteLine($"PASS {relativePath}");
                }
                catch (Exception exception)
                {
                    failures++;
                    Console.Error.WriteLine($"FAIL {relativePath}");
                    Console.Error.WriteLine(exception);
                }
            }

            Console.WriteLine(
                $"Save-game compatibility: {saveFiles.Length - failures} passed, {failures} failed.");
            return failures == 0 ? 0 : 1;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(
                "Usage: Burntime --savegame-test FOLDER [--turns N]");
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Save-game compatibility test failed:");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    static (string FixturePath, int Turns) Parse(string[] args)
    {
        string? fixturePath = null;
        int turns = 1;
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            switch (argument)
            {
                case "--savegame-test":
                    fixturePath = NextValue(args, ref index, argument);
                    break;
                case "--turns":
                    string value = NextValue(args, ref index, argument);
                    if (!int.TryParse(value, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out turns) || turns < 1)
                        throw new ArgumentException("--turns must be a positive integer.");
                    break;
                default:
                    throw new ArgumentException(
                        $"Unknown save-game compatibility option: {argument}");
            }
        }

        if (fixturePath is null)
            throw new ArgumentException("--savegame-test requires a fixture folder.");
        return (fixturePath, turns);
    }

    static string NextValue(string[] args, ref int index, string option)
    {
        if (++index >= args.Length)
            throw new ArgumentException($"Missing value for {option}.");
        return args[index];
    }

    sealed class LoadingCounter : ILoadingCounter
    {
        public void IncreaseLoadingCount() { }
        public void DecreaseLoadingCount() { }
    }
}

internal static class HeadlessFileMounts
{
    public static string MountInputFile(string path, string packageName)
    {
        string fullPath = Path.GetFullPath(path);
        if (!System.IO.File.Exists(fullPath))
            throw new ArgumentException($"Save game does not exist: {fullPath}");
        return MountFile(fullPath, packageName, createDirectory: false);
    }

    public static string MountOutputFile(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);
        // The VFS intentionally creates new files only in its writable user
        // package. Point that package at the requested headless output folder.
        FileSystem.AddPackage("user", directory);
        return $"user:{Path.GetFileName(fullPath)}";
    }

    static string MountFile(string fullPath, string packageName, bool createDirectory)
    {
        string directory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        if (createDirectory)
            Directory.CreateDirectory(directory);
        FileSystem.AddPackage(packageName, directory);
        return $"{packageName}:{Path.GetFileName(fullPath)}";
    }
}
