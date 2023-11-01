using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Platform.Graphics;
using Burntime.Framework.States;
using Burntime.Data.BurnGfx;
using System.Linq;

namespace Burntime.Remaster.Logic
{

    public enum PlayerType
    {
        Human,
        Ai
    }

    [Serializable]
    [DebuggerDisplay("{Name}{Index} at {Location.Title}")]
    public class Player : PlayerState, IUpdateable, ITurnable
    {
        protected bool onMainMap;
        
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

        public bool OnMainMap
        {
            get { return onMainMap; }
            set { onMainMap = value; }
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
        StateLink<Location> city;
        public StateLinkList<Fog> Fogs;


        protected int remainingTravelDays = 0;
        protected int travelDays = 0;

        protected StateLink<Location> destination;
        protected StateLink<Location> previousLocation;

        protected DataID<ISprite> flag;
        public DataID<ISprite> Flag
        {
            get { return flag; }
            set { flag = value; }
        }

        // character, group, selections
        protected bool singleMode;
        public bool SingleMode
        {
            get { return singleMode; }
            //set { singleMode = value; }
        }

        StateLink<Character> character;
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

        StateLink<Group> group;
        public Group Group
        {
            get { return group; }
            set { group = value; }
        }

        protected StateLink<Character> selectedCharacter;
        public Character SelectedCharacter
        {
            get { return selectedCharacter; }
        }
        protected StateLink<Group> selectedGroup;

        public bool SelectCharacter(Character targetCharacter)
        {
            if (SelectedCharacter == targetCharacter)
                return false;

            // select single character
            selectedCharacter = targetCharacter;
            targetCharacter.Mind = container.Create<AI.PlayerControlledMind>(new object[] { targetCharacter });
            singleMode = true;
            return true;
        }

        public void SelectGroup(ICharacterCollection targetGroup)
        {
            // for now assume the player's character is the leader
            selectedCharacter = Character;
            foreach (Character member in targetGroup)
            {
                if (member != Character)
                    member.Mind = container.Create<AI.FellowerMind>(new object[] { member, SelectedCharacter });
            }
            singleMode = targetGroup.Count == 1;
        }

        protected override void InitInstance(object[] parameter)
        {
            group = container.Create<Group>();
            Group.RangeFilterValue = 50;
            refreshScrollPosition = true;
            refreshMapScrollPosition = true;
            index = (int)parameter[0];
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
            SelectGroup(Group);
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

        public int GetTravelDays(Location from, Location destination)
        {
            if (from.Player != null && from.Player != this)
            {
                // only if destination = previous location
                if (previousLocation == null)
                    return 0;

                if (destination != previousLocation.Object)
                    return 0;
            }

            for (int i = 0; i < Location.Neighbors.Count; i++)
            {
                if (Location.Neighbors[i] == destination)
                {
                    return Location.WayLengths[i];
                }
            }

            return 0;
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

        public int GetOwnedLocationCount(ClassicWorld world)
        {
            return world.Locations.OfType<Location>().Where(l => l.Player == this).Count();
        }

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