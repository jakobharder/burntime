
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
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
