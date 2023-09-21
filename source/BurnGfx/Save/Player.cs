using System;
using System.Collections.Generic;

namespace Burntime.Data.BurnGfx.Save
{
    public struct PlayerInfo
    {
        public ushort FlagId;
        public ushort TravelDays;
        public ushort TravelCityId;
        public byte[] Unknown1;
        public byte Color;
        public ushort CharacterId;
        public byte[] Unknown2;
        public ushort Controller;
        public byte[] Unknown3;
        public ushort IconId;
        public byte[] Name;
        public byte[] Unknown4;
        public ushort DayTime;
        public ushort PlayerId;
        public byte LocationId;
        public byte[] Unknown5;
        public byte FromCityId;
        public byte[] Unknown6;
    };

    public class Player
    {
        internal PlayerInfo info;
        public PlayerInfo Info
        {
            get { return info; }
        }

        internal String name;
        public String Name
        {
            get { return name; }
            set { throw new NotImplementedException(); }
        }

        public bool IsHumanPlayer
        {
            get { return info.Controller == 0; }
            set { info.Controller = (ushort)(value ? 1 : 0); }
        }

        public int LocationId
        {
            get { return info.LocationId - 1; }
            set { info.LocationId = (byte)(value + 1); }
        }

        List<int> group = new List<int>();
        public List<int> Group
        {
            get { return group; }
        }

        internal Character character;
        public Character Character
        {
            get { return character; }
        }

        public Player(PlayerInfo info)
        {
            this.info = info;
        }
    }
}
