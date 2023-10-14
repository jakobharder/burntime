
using Burntime;

#if (DEBUG)

using var game = new Burntime.MonoGame.BurntimeGame();
game.Run();

#else

try
{
    using var game = new Burntime.MonoGl.BurntimeGame();
    game.Run();
}
catch (System.Exception exception)
{
    ErrorMsg.LogException(exception);
}

#endif