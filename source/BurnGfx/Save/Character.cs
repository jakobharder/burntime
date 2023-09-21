﻿using System;
using System.Collections.Generic;

namespace Burntime.Data.BurnGfx.Save
{
    public struct CharacterInfo
    {
        public ushort LocationId;
        public byte Water;
        public byte FaceId;
        public byte[] Unknown1;
        public byte Food;
        public ushort NameId;
        public byte CharType;
        public byte[] Unknown2;
        public ushort EmployerId;
        public ushort MoveX;
        public ushort MoveY;
        public ushort MoveCharId;
        public byte[] Unknown3;
        public ushort HireItemId;
        public byte TextId;
        public byte[] Unknown4;
        public byte SpriteId; // >> 4
        public byte[] Unknown5;
        public byte Experience;
        public byte Health;
    };

    public enum CharacterType
    {
        Mercenary,
        Technician,
        Doctor,
        Boss,
        Mutant,
        Trader,
        Dog
    }

    public class Character
    {
        internal CharacterInfo info;
        public CharacterInfo Info
        {
            get { return info; }
        }

        internal ushort ID;
        internal ushort ID2;

        public int NameId
        {
            get { return info.NameId - 1; }
        }

        public int Food
        {
            get { return info.Food; }
            set { info.Food = (byte)value; }
        }

        public int Water
        {
            get { return info.Water; }
            set { info.Water = (byte)value; }
        }

        public int Experience
        {
            get { return info.Experience; }
            set { info.Experience = (byte)value; }
        }

        public int Health
        {
            get { return info.Health; }
            set { info.Health = (byte)value; }
        }

        public int LocationId
        {
            get { return info.LocationId - 1; }
            set { info.LocationId = (ushort)(value + 1); }
        }

        public int EmployerId
        {
            get { return (info.EmployerId - 0x3128) / 62; }
        }

        public CharacterType Type
        {
            get { if (info.CharType == 4 && info.FaceId == 0) return CharacterType.Dog; return (CharacterType)info.CharType; }
        }

        public int SpriteId
        {
            get { return info.SpriteId; }
        }

        List<int> items = new List<int>();
        public List<int> Items
        {
            get { return items; }
        }

        public Character(CharacterInfo info)
        {
            this.info = info;
        }
    }
}
