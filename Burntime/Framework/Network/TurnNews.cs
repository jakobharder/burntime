using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Framework.States;

namespace Burntime.Framework.Network
{
    public interface ITurnNews
    {
    }

    public class DeathNews : ITurnNews
    {
        public string Name;

        public DeathNews(string name)
        {
            Name = name;
        }
    }

    public class VictoryNews : ITurnNews
    {
        public int Player;
        public string Name;

        public VictoryNews(PlayerState player)
        {
            Player = player.Index;
            Name = player.Name;
        }
    }
}
