using System;
using System.IO;
using Burntime.Game;

namespace Burntime
{
    static class Program
    {
        static public void Run(string pak)
        {
            GameStarter game = new GameStarter();
            game.Run(pak ?? "classic");
        }
    }
}