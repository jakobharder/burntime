
#region GNU General Public License - Burntime
/*
 *  Burntime
 *  Copyright (C) 2008-2011 Jakob Harder
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
*/
#endregion

using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Platform.Graphics;
using Burntime.Framework.States;
using Burntime.Data.BurnGfx;

namespace Burntime.Classic.Logic
{

    public enum PlayerType
    {
        Human,
        Ai
    }

    [Serializable]
    public class Player : PlayerState, IUpdateable, ITurnable
    {
        protected bool singleMode;
        protected bool onMainMap;
        protected StateLink<Character> selectedCharacter;
        protected int index;
        protected int baseExperience;
        protected PlayerType type;
        protected bool isDead = false;

        // ui state
        protected Vector2 mapScrollPosition;
        protected Vector2 locationScrollPosition;
        protected bool refreshScrollPosition;
        protected bool refreshMapScrollPosition;
        protected bool infoMode;
        protected bool fightMode;

        public override string Name
        {
            get { return Character.Name; }
            set { Character.Name = value; }
        }

        public override bool IsDead
        {
            get { return isDead; }
            set { isDead = value; }
        }

        public override bool IsTraveling
        {
            get { return remainingTravelDays > 0; }
        }

        public override int Index
        {
            get { return index; }
        }

        public bool SingleMode
        {
            get { return singleMode; }
            set { singleMode = value; }
        }

        public bool OnMainMap
        {
            get { return onMainMap; }
            set { onMainMap = value; }
        }

        public Character SelectedCharacter
        {
            get { return selectedCharacter; }
            set { selectedCharacter = value; }
        }

        public Vector2 MapScrollPosition
        {
            get { return mapScrollPosition; }
            set 
            { 
                mapScrollPosition = value;
                refreshMapScrollPosition = false;
            }
        }

        public Vector2 LocationScrollPosition
        {
            get { return locationScrollPosition; }
            set 
            { 
                locationScrollPosition = value;
                refreshScrollPosition = false;
            }
        }

        public bool RefreshScrollPosition
        {
            get { return refreshScrollPosition; }
            set { refreshScrollPosition = value; }
        }

        public bool RefreshMapScrollPosition
        {
            get { return refreshMapScrollPosition; }
            set { refreshMapScrollPosition = value; }
        }

        public bool InfoMode
        {
            get { return infoMode; }
            set { infoMode = value; }
        }

        public bool FightMode
        {
            get { return fightMode; }
            set { fightMode = value; }
        }

        // experience without taking camps into account
        public int BaseExperience
        {
            get { return baseExperience; }
            set { baseExperience = value; }
        }

        public PlayerType Type
        {
            get { return type; }
            set { type = value; }
        }

        public bool DiedThisRound = false;

        public PixelColor Color;
        public PixelColor ColorDark;
        public int BodyColorSet;
        public int IconID;
        StateLink<Character> character;
        StateLink<Location> city;
        public StateLinkList<Fog> Fogs;
        StateLink<Group> group;

        protected int remainingTravelDays = 0;
        protected int travelDays = 0;

        protected StateLink<Location> destination;
        protected StateLink<Location> previousLocation;

        protected DataID<Sprite> flag;
        public DataID<Sprite> Flag
        {
            get { return flag; }
            set { flag = value; }
        }

        protected override void InitInstance(object[] parameter)
        {
            group = container.Create<Group>();
            Group.RangeFilterValue = 50;
            refreshScrollPosition = true;
            refreshMapScrollPosition = true;
            index = (int)parameter[0];
        }

        // attribute accessor
        public Group Group
        {
            get { return group; }
            set { group = value; }
        }

        public Character Character
        {
            get { return character; }
            set
            {
                if (character != null)
                    Group.Remove(character);
                character = value;
                if (character != null)
                    Group.Insert(0, character);
                selectedCharacter = character;
            }
        }

        public int FaceID
        {
            get { return Character.FaceID; }
        }

        public Location Location
        {
            get { return city; }
            set { city = value; }
        }

        public Location Destination
        {
            get { return destination; }
        }

        public int RemainingDays
        {
            get { return remainingTravelDays; }
        }

        public int TravelDays
        {
            get { return travelDays; }
        }

        // logic
        public virtual void Update(float elapsed)
        {
            if (IsDead)
                return;

            if (Character.IsDead && Character.DeadAnimationFinished)
            {
                IsDead = true;
                DiedThisRound = true;
                return;
            }

            for (int i = 0; i < Group.Count; i++)
            {
                if (Group[i].Position == -Vector2.One)
                    Group[i].Position = new Vector2(Location.EntryPoint);

                Group[i].Update(elapsed);
            }
        }

        public override void Turn()
        {
            Character.Path.MoveTo = Character.Position;
            RecalculateExperience();

            if (remainingTravelDays > 0)
            {
                remainingTravelDays--;

                if (remainingTravelDays == 0)
                {
                    Location = destination;
                    destination = null;
                }
            }
        }

        #region public travel methods
        /// <summary>
        /// Set player to travel to destination.
        /// </summary>
        /// <param name="destination">destination location</param>
        public void Travel(Location destination)
        {
            SelectedCharacter = Character;
            SingleMode = false;
            RefreshScrollPosition = true;
            RefreshMapScrollPosition = true;

            foreach (Character chr in Group)
                chr.Position = destination.EntryPoint;

            for (int i = 0; i < Location.Neighbors.Count; i++)
            {
                if (Location.Neighbors[i] == destination)
                {
                    remainingTravelDays = Location.WayLengths[i];
                    travelDays = remainingTravelDays;
                    break;
                }
            }

            // set previous location
            previousLocation = this.Location;

            // set destination
            this.destination = destination;
        }

        /// <summary>
        /// Check wether route to destination is not blocked.
        /// </summary>
        /// <param name="destination">destination location</param>
        /// <returns>true if not blocked</returns>
        public bool CanTravel(Location from, Location destination)
        {
            if (from.Player != null && from.Player != this)
            {
                // only if destination = previous location
                if (previousLocation == null)
                    return false;

                if (destination != previousLocation.Object)
                    return false;
            }

            return true;
        }
        #endregion

        private void RecalculateExperience()
        {
            int count = 0;

            ClassicGame game = container.Root as ClassicGame;
            foreach (Location location in game.World.Locations)
            {
                if (location.Player == this)
                    count++;
            }

            Character.Experience = count * 3 + BaseExperience;

            // limit experience to 99%
            if (Character.Experience > 99)
                Character.Experience = 99;
        }
    }
}