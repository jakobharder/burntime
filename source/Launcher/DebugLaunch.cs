using Burntime.Game;

namespace Burntime.Classic
{
    class DebugLaunch
    {
        public void Run()
        {
            GameControl game = new GameControl();
            game.Run("classic");
        }
    }
}
