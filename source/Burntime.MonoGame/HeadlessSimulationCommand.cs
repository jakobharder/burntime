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

internal static class HeadlessSimulationCommand
{
    public static bool IsRequested(string[] args) => args.Contains("--ai-simulate", StringComparer.OrdinalIgnoreCase);

    public static int Run(string[] args)
    {
        try
        {
            ParsedOptions parsed = Parse(args);
            FileSystem.BasePath = AppContext.BaseDirectory;
            PackageManager packageManager = new("game/");
            packageManager.LoadPackages("classic", FileSystem.VFS, null);

            LoadingCounter loadingCounter = new();
            using HeadlessResourceManager resources = new(loadingCounter);
            BurntimeClassic app = new();
            app.Initialize(resources);

            BurnGfxModule burnGfx = new();
            burnGfx.Initialize(resources);
            app.InitializeHeadless();

            string? loadGamePath = parsed.LoadSavePath is null
                ? null
                : HeadlessFileMounts.MountInputFile(
                    parsed.LoadSavePath, "ai-simulation-input");
            string? saveGamePath = parsed.SaveAtEndPath is null
                ? null
                : HeadlessFileMounts.MountOutputFile(parsed.SaveAtEndPath);

            string report = HeadlessSimulation.Run(app, new HeadlessSimulationOptions
            {
                Turns = parsed.Turns,
                Difficulty = parsed.Difficulty,
                AiDifficulties = parsed.AiDifficulties,
                Seed = parsed.Seed,
                ExtendedGame = parsed.ExtendedGame,
                LoadGamePath = loadGamePath,
                SaveGamePath = saveGamePath
            });

            if (parsed.ReportPath is null)
            {
                Console.Write(report);
            }
            else
            {
                string fullPath = Path.GetFullPath(parsed.ReportPath);
                string? directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                System.IO.File.WriteAllText(fullPath, report);
                Console.WriteLine($"AI simulation report written to {fullPath}");
            }

            return 0;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine("Usage: Burntime --ai-simulate [--turns N] [--difficulty easy|normal|hard] " +
                "[--ai-difficulties easy,normal,hard,hard] [--seed N] [--load-save PATH] " +
                "[--save-at-end PATH] [--report PATH] [--extended]");
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Headless AI simulation failed:");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    static ParsedOptions Parse(string[] args)
    {
        ParsedOptions result = new();

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            switch (argument)
            {
                case "--ai-simulate":
                    break;
                case "--turns":
                    result.Turns = ParsePositiveInt(NextValue(args, ref index, argument), argument);
                    break;
                case "--seed":
                    result.Seed = ParseInt(NextValue(args, ref index, argument), argument);
                    break;
                case "--difficulty":
                    result.Difficulty = ParseDifficulty(NextValue(args, ref index, argument));
                    break;
                case "--ai-difficulties":
                    result.AiDifficulties = ParseAiDifficulties(
                        NextValue(args, ref index, argument));
                    break;
                case "--report":
                    result.ReportPath = NextValue(args, ref index, argument);
                    break;
                case "--load-save":
                    result.LoadSavePath = NextValue(args, ref index, argument);
                    break;
                case "--save-at-end":
                    result.SaveAtEndPath = NextValue(args, ref index, argument);
                    break;
                case "--extended":
                    result.ExtendedGame = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown AI simulation option: {argument}");
            }
        }

        return result;
    }

    static string NextValue(string[] args, ref int index, string option)
    {
        if (++index >= args.Length)
            throw new ArgumentException($"Missing value for {option}.");
        return args[index];
    }

    static int ParsePositiveInt(string value, string option)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) || result < 1)
            throw new ArgumentException($"{option} must be a positive integer.");
        return result;
    }

    static int ParseInt(string value, string option)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            throw new ArgumentException($"{option} must be an integer.");
        return result;
    }

    static int ParseDifficulty(string value) => value.ToLowerInvariant() switch
    {
        "easy" or "0" => 0,
        "normal" or "1" => 1,
        "hard" or "2" => 2,
        _ => throw new ArgumentException("--difficulty must be easy, normal, or hard.")
    };

    static int[] ParseAiDifficulties(string value)
    {
        string[] values = value.Split(',', StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        if (values.Length != 4)
            throw new ArgumentException("--ai-difficulties must contain four comma-separated values.");
        return values.Select(ParseDifficulty).ToArray();
    }

    sealed class ParsedOptions
    {
        public int Turns { get; set; } = 100;
        public int Difficulty { get; set; } = 2;
        public int[]? AiDifficulties { get; set; }
        public int Seed { get; set; } = 1;
        public string? ReportPath { get; set; }
        public string? LoadSavePath { get; set; }
        public string? SaveAtEndPath { get; set; }
        public bool ExtendedGame { get; set; }
    }

    sealed class LoadingCounter : ILoadingCounter
    {
        public void IncreaseLoadingCount() { }
        public void DecreaseLoadingCount() { }
    }
}
