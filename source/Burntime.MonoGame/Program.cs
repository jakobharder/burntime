
using Burntime;
using System;

#if !(DEBUG)
    AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CustomExceptionHandler.OnThreadException);
#endif

using var game = new Burntime.MonoGame.BurntimeGame();
game.Run();
