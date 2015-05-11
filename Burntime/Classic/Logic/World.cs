using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Framework.States;
using Burntime.Classic.Maps;
using Burntime.Classic;

namespace Burntime.Classic.Logic
{
    [Serializable]
    public class World : StateObject
    {
        public readonly float TimePerDay = 1.00f;
        public float RoundTime = (BurntimeClassic.Instance.Settings["game"].GetInt("roundtime") > 0) ? BurntimeClassic.Instance.Settings["game"].GetInt("roundtime") : 240; // default 240 seconds
        public int Difficulty;

        public StateLink<Map> map;
        public StateLink<Ways> ways;

        public Map Map
        {
            get { return map; }
            set { map = value; }
        }
        public Ways Ways
        {
            get { return ways; }
            set { ways = value; }
        }

        public StateLink<Interaction.VictoryCondition> VictoryCondition;
        public StateLinkList<Player> Players;
        public StateLinkList<Location> Locations;
        public StateLinkList<Character> AllCharacters;
        public StateLinkList<Trader> Traders;
        public StateLink<CharacterRespawn> Respawn;
        public int ActivePlayer;
        public float Time;
        public int Day;

        protected override void InitInstance(object[] parameter)
        {
            Players = Container.CreateLinkList<Player>();
            Locations = Container.CreateLinkList<Location>();
            AllCharacters = Container.CreateLinkList<Character>();
            Traders = Container.CreateLinkList<Trader>();

            Day = 1;
            Time = TimePerDay;
        }

        public void Update(float elapsed)
        {
            Time -= (TimePerDay / RoundTime) * elapsed; // TimeSpeed
            if (Time < 0)
                Time = 0;
        }

        // logic
        public virtual void Turn()
        {
            Day++;
            Time = TimePerDay;

            TurnTraders(Traders);
            TurnLocations(Locations);

            for (int i = 0; i < Players.Count; i++)
            {
                if (!Players[i].IsDead)
                {
                    if (Players[i].Character.IsDead)
                    {
                        for (int j = 0; j < Locations.Count; j++)
                        {
                            if (Locations[j].Player == Players[i])
                            {
                                List<Character> list = Locations[j].CampNPC;
                                foreach (Character ch in list)
                                    ch.Dismiss();
                            }
                        }

                        for (int j = 1; j < Players[i].Group.Count; j++)
                        {
                            Players[i].Group[j].Dismiss();
                        }

                        Players[i].IsDead = true;
                        Players[i].DiedThisRound = true;
                    }
                }
                else
                    Players[i].DiedThisRound = false;

                if (!Players[i].IsDead)
                {
                    for (int j = 0; j < Players[i].Group.Count; j++)
                        Players[i].Group[j].Turn();
                }
            }

            // handle respawnings
            Respawn.Object.Turn();
        }

        public virtual void TurnLocations(StateLinkList<Location> LocationList)
        {
            for (int i = 0; i < LocationList.Count; i++)
            {
                LocationList[i].Turn();
            }
        }

        public virtual void TurnTraders(StateLinkList<Trader> TraderList)
        {
            for (int i = 0; i < TraderList.Count; i++)
            {
                Trader trader = TraderList[i];
                trader.Turn();
            }
        }
    }
}