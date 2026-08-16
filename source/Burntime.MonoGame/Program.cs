
using Burntime;
using System;

#if !(DEBUG)
    AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CustomExceptionHandler.OnThreadException);
#endif

if (Burntime.MonoGame.HeadlessSimulationCommand.IsRequested(args))
{
    Environment.ExitCode = Burntime.MonoGame.HeadlessSimulationCommand.Run(args);
    return;
}

using var game = new Burntime.MonoGame.BurntimeGame();
game.Run();
