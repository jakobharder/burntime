using System;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster
{
    [Serializable]
    public class ClassicWorld : World
    {
        public Player ActivePlayerObj
        {
            get { if (ActivePlayer == -1) return null; else return Players[ActivePlayer]; }
        }

        public Location ActiveLocationObj
        {
            get { if (ActivePlayer == -1) return null; else return Players[ActivePlayer].Location; }
        }

        [NonSerialized]
        public Character SelectedCharacter;

        [NonSerialized]
        Trader activeTraderObj;
        public Trader ActiveTraderObj
        {
            get { return activeTraderObj; }
            set { activeTraderObj = value; }
        }
    }
}
