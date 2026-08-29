
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

using var game = new Burntime.MonoGame.BurntimeGame();
game.Run();
