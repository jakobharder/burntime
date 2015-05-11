using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Framework;
using Burntime.Framework.States;

namespace Burntime.Classic
{
    [Serializable]
    class ClassicGame : StateObject
    {
        StateLink world;
        public ClassicWorld World
        {
            get { return world.Get(container) as ClassicWorld; }
            set { world = value; }
        }
    }

    [Serializable]
    class ClassicWorld : World
    {
        public Player ActivePlayerObj
        {
            get { if (ActivePlayer == -1) return null; else return Players[ActivePlayer]; }
        }

        public Location ActiveLocationObj
        {
            get { if (ActivePlayer == 1) return null; else return Players[ActivePlayer].Location; }
        }
    }
}
