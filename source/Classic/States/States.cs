﻿using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Classic.Logic;
using Burntime.Platform.Resource;
using Burntime.Classic.Logic.Interaction;

namespace Burntime.Classic
{
    [Serializable]
    public class ClassicGame : WorldState
    {
        StateLink<ClassicWorld> world;
        public ClassicWorld World
        {
            get { return world; }
            set { world = value; }
        }

        StateLinkList<Production> productions;
        public StateLinkList<Production> Productions
        {
            get { return productions; }
            set { productions = value; }
        }

        StateLink<ItemTypes> itemTypes;
        public ItemTypes ItemTypes
        {
            get { return itemTypes; }
            set { itemTypes = value; }
        }

        public override StateObject CurrentLocation
        {
            get { return World.ActiveLocationObj; }
        }

        public override StateObject CurrentPlayer
        {
            get { return World.ActivePlayerObj; }
        }

        public override PlayerState[] Player
        {
            get 
            {
                List<PlayerState> list = new List<PlayerState>();

                for (int i = 0; i < World.Players.Count; i++)
                    list.Add(World.Players[i]);

                return list.ToArray();
            }
        }

        public override int CurrentPlayerIndex
        {
            get { return World.ActivePlayer; }
        }

        DataID<Constructions> constructions;
        public Constructions Constructions
        {
            get { return constructions; }
            set { constructions = value; }
        }

        protected override void InitInstance(object[] parameter)
        {
            productions = container.CreateLinkList<Production>();
        }

        public override void Turn()
        {
            World.Turn();

            base.Turn();
        }

        [NonSerialized]
        public bool MainMapView;

        public override PlayerState CheckWinner()
        {
            foreach (PlayerState player in Player)
            {
                if (World.VictoryCondition.Object.Process((Player)player))
                    return player;
            }

            return null;
        }
    }

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
