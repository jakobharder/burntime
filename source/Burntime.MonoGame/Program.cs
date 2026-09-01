
using Burntime;
using System;

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
    nearestPointFiltering, disableShaders, showFps);
game.Run();
