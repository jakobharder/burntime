/*
 *  BurnGfx - Burntime Data IO Library
 *  Copyright (C) 2009
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
 *  contact: 
 *    juern - burntimedeluxe@gmail.com or yn.harada@gmail.com
 * 
 *  authors: 
 *    Juernjakob Harder - 原田ゆあん (yn.harada@gmail.com)
 * 
*/

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
