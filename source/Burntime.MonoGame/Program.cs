
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

if (emulateSteamMachine && emulateSteamDeck)
{
    Console.Error.WriteLine("Use either --steam-machine or --steam-deck, not both.");
    Environment.ExitCode = 2;
    return;
}

using var game = new Burntime.MonoGame.BurntimeGame(
    emulateSteamMachine, emulateSteamDeck, chooseLanguage, linearFiltering);
game.Run();
