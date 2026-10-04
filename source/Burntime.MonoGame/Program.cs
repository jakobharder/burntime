
using Burntime;
using System;

Burntime.Platform.Graphics.CharacterGraphicsOptions.Configure(args);

if (args.Contains("--store-capture"))
{
    Environment.ExitCode = Burntime.MonoGame.VisualTestRunner.RunStoreCapture(args);
    return;
}

if (args.Contains("--visual-test"))
{
    Environment.ExitCode = Burntime.MonoGame.VisualTestRunner.Run(args);
    return;
}

if (Burntime.MonoGame.LocationSoakRunner.IsRequested(args))
{
    Environment.ExitCode = Burntime.MonoGame.LocationSoakRunner.Run(args);
    return;
}

if (Burntime.MonoGame.UiSoakRunner.IsRequested(args))
{
    Environment.ExitCode = Burntime.MonoGame.UiSoakRunner.Run(args);
    return;
}

#if !(DEBUG)
    AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CustomExceptionHandler.OnThreadException);
#endif

if (Burntime.MonoGame.HeadlessSimulationCommand.IsRequested(args) ||
    Burntime.MonoGame.SavegameCompatibilityCommand.IsRequested(args))
{
    Environment.ExitCode = Burntime.MonoGame.SavegameCompatibilityCommand.IsRequested(args)
        ? Burntime.MonoGame.SavegameCompatibilityCommand.Run(args)
        : Burntime.MonoGame.HeadlessSimulationCommand.Run(args);
    return;
}

bool emulateSteamMachine = args.Contains("--steam-machine", StringComparer.OrdinalIgnoreCase);
bool emulateSteamDeck = args.Contains("--steam-deck", StringComparer.OrdinalIgnoreCase);
bool chooseLanguage = args.Contains("--choose-language", StringComparer.OrdinalIgnoreCase);
bool linearFiltering = args.Contains("--linear", StringComparer.OrdinalIgnoreCase);
bool nearestPointFiltering = args.Contains("--nearest-point", StringComparer.OrdinalIgnoreCase);
bool disableShaders = args.Contains("--no-shader", StringComparer.OrdinalIgnoreCase);
bool showFps = args.Contains("--fps", StringComparer.OrdinalIgnoreCase);

if (!TryParseWindowSize(args, out Burntime.Platform.Vector2? windowSize,
    out string? windowSizeError))
{
    Console.Error.WriteLine(windowSizeError);
    Environment.ExitCode = 2;
    return;
}

if (emulateSteamMachine && emulateSteamDeck)
{
    Console.Error.WriteLine("Use either --steam-machine or --steam-deck, not both.");
    Environment.ExitCode = 2;
    return;
}

if ((linearFiltering ? 1 : 0) + (nearestPointFiltering ? 1 : 0) +
    (disableShaders ? 1 : 0) > 1)
{
    Console.Error.WriteLine(
        "Use only one of --linear, --nearest-point, or --no-shader.");
    Environment.ExitCode = 2;
    return;
}

using var game = new Burntime.MonoGame.BurntimeGame(
    emulateSteamMachine, emulateSteamDeck, chooseLanguage, linearFiltering,
    nearestPointFiltering, disableShaders, showFps, windowSize);
game.Run();

static bool TryParseWindowSize(string[] arguments, out Burntime.Platform.Vector2? size,
    out string? error)
{
    size = null;
    error = null;
    for (int index = 0; index < arguments.Length; index++)
    {
        string argument = arguments[index];
        string? value = null;
        if (argument.Equals("--window-size", StringComparison.OrdinalIgnoreCase))
        {
            if (++index >= arguments.Length)
            {
                error = "--window-size requires WIDTHxHEIGHT, for example 2560x1440.";
                return false;
            }
            value = arguments[index];
        }
        else if (argument.StartsWith("--window-size=", StringComparison.OrdinalIgnoreCase))
        {
            value = argument["--window-size=".Length..];
        }

        if (value is null)
            continue;
        if (size.HasValue)
        {
            error = "Specify --window-size only once.";
            return false;
        }

        string[] dimensions = value.Split('x', 'X');
        if (dimensions.Length != 2 ||
            !int.TryParse(dimensions[0], out int width) ||
            !int.TryParse(dimensions[1], out int height) ||
            width <= 0 || height <= 0)
        {
            error = $"Invalid window size '{value}'. Use WIDTHxHEIGHT, for example 2560x1440.";
            return false;
        }
        size = new Burntime.Platform.Vector2(width, height);
    }
    return true;
}
