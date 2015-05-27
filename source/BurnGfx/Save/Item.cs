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

namespace Burntime.Data.BurnGfx.Save
{
    public enum ItemOwnerType
    {
        Pool,
        Dropped,
        Room,
        Character,
        Unknown
    }

    public struct ItemInfo
    {
        public byte SpriteId;
        public byte Value; // Water, ...
        public ushort TitleId;
        public ushort Owner;
        public ushort Damage;
        public ushort XOrRoom;          // Room index or X position
        public ushort YOrNothing;       // Unknown value or Y position

        public int CityId
        {
            get
            {
                int id = Owner - 0x29EC;
                id = (id - id % 0x32) / 0x32 + 1;
                if (id < 0 || id > 36)
                    return 0;
                return id;
            }
            set
            {
                Owner = (ushort)((value - 1) * 0x32 + 0x29EC);
            }
        }
    };

    public class Item
    {
        internal ItemInfo info;
        public ItemInfo Info
        {
            get { return info; }
        }

        public ItemOwnerType OwnerType
        {
            get
            {
                if (info.Owner == 0xffff)
                {
                    return ItemOwnerType.Pool;
                }
                else if (info.CityId != 0)
                {
                    return ItemOwnerType.Room;
                }
                else if ((info.Owner >> 8) >= 0x33)
                {
                    return ItemOwnerType.Character;
                }
                else if ((info.Owner >> 8) == 0)
                {
                    return ItemOwnerType.Dropped;
                }

                return ItemOwnerType.Unknown;
            }
        }

        public int OwnerId
        {
            get { return (info.Owner - 0x33b0) / 46; }
        }

        public int LocationId
        {
            get { return info.CityId - 1; }
        }

        public int DroppedLocationId
        {
            get { return info.Owner - 1; }
        }

        public Burntime.Platform.Vector2 DroppedPosition
        {
            get { return new Burntime.Platform.Vector2(info.XOrRoom, info.YOrNothing); }
        }

        public int RoomId
        {
            get
            {
                if ((info.XOrRoom >> 8) == 0)
                    return info.XOrRoom;
                return (info.XOrRoom >> 8);
            }
        }

        public int TypeId
        {
            get { return info.SpriteId; }
        }

        public int SpriteId
        {
            get { return info.SpriteId; }
        }

        public int FoodValue
        {
            get 
            {
                if (TypeId < 4)
                    return info.Value;
                return 0; 
            }
        }

        public int WaterValue
        {
            get 
            {
                if (TypeId == 4 || TypeId == 6 || TypeId == 8)
                    return info.Value;
                return 0; 
            }
        }

        public Item(ItemInfo info)
        {
            this.info = info;
        }
    }
}
