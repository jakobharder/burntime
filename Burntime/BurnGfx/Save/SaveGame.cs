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
using System.IO;

using Burntime.Platform;
using Burntime.Platform.IO;

namespace Burntime.Data.BurnGfx.Save
{
    public class SaveGame
    {
        byte[] BURN;
        public ushort CharIdOffset;
        public byte[] Unknown2;
        GameState state;
        public byte[] Unknown3;
        Location[] locations;
        Player[] player;
        public byte[] Unknown4;
        Character[] characters;
        public byte[] Unknown5;
        Item[] items;

        private static SaveGame gamDat;

        static public SaveGame GamDat
        {
            get
            {
                if (gamDat == null)
                {
                    gamDat = new SaveGame();
                    gamDat.Open();
                }

                return gamDat;
            }
        }

        // access
        public GameState State
        {
            get { return state; }
        }

        public Location[] Locations
        {
            get { return locations; }
        }

        public Player[] Player
        {
            get { return player; }
        }

        public Character[] Characters
        {
            get { return characters; }
        }

        public Item[] Items
        {
            get { return items; }
        }

        public bool Open()
        {
            Burntime.Platform.IO.File file = FileSystem.GetFile("burntime:gam.dat");
            if (file == null)
                return false;

            return Open(file.Stream);
        }

        public bool Open(Stream file)
        {
            if (file == null)
                return false;

            const int LocationCount = 37;
            const int PlayerCount = 4;
            const int CharacterCount = 309;
            const int ItemCount = 1199;
            const int FirstTraderId = 21;

            BinaryReader reader = new BinaryReader(file);
            
            BURN = reader.ReadBytes(4);

            CharIdOffset = reader.ReadUInt16(); // player-char link
            Unknown2 = reader.ReadBytes(2);                                 // unknown 2 bytes

            // read global game info
            GameStateInfo gsi = new GameStateInfo();
            gsi.ActiveID2               = reader.ReadUInt16();
            gsi.ActivePlayer            = reader.ReadUInt16();
            gsi.Day                     = reader.ReadUInt16();
            gsi.FirstCityMerchantCityId = reader.ReadUInt16();
            gsi.Unknown                 = reader.ReadBytes(4);              // unknown 4 bytes
            gsi.Difficulty              = reader.ReadUInt16();
            state = new GameState(gsi);

            // read location info
            int traderindex = FirstTraderId;
            locations = new Location[LocationCount];
            for (int i = 0; i < LocationCount; i++)
            {
                LocationInfo info = new LocationInfo();
                info.TitleId         = reader.ReadUInt16();
                info.Owner           = reader.ReadUInt16();
                info.Unknown1        = reader.ReadBytes(8);                  // unknown 8 bytes
                info.Water           = reader.ReadUInt16();
                info.WaterCapacity   = reader.ReadUInt16();
                info.WaterSource     = reader.ReadUInt16();

                info.EntryPointX     = reader.ReadUInt16();
                info.EntryPointY     = reader.ReadUInt16();

                info.CityFlag        = reader.ReadUInt16();
                info.Production1     = reader.ReadByte();
                info.Production2     = reader.ReadByte();
                info.Production3     = reader.ReadByte();
                info.Production4     = reader.ReadByte();
                info.Producing       = reader.ReadUInt16();
                info.Unknown2        = reader.ReadBytes(2);                  // unknown 2 bytes

                info.Neighbour1      = reader.ReadByte();
                info.Neighbour2      = reader.ReadByte();
                info.Neighbour3      = reader.ReadByte();
                info.Neighbour4      = reader.ReadByte();

                info.WayTime1        = reader.ReadByte();
                info.WayTime2        = reader.ReadByte();
                info.WayTime3        = reader.ReadByte();
                info.WayTime4        = reader.ReadByte();

                info.Way1            = reader.ReadByte();
                info.Way2            = reader.ReadByte();
                info.Way3            = reader.ReadByte();
                info.Way4            = reader.ReadByte();

                info.Unknown3        = reader.ReadBytes(1);                  // unknown 1 byte
                info.Danger          = reader.ReadByte();
                info.DangerAmount    = reader.ReadUInt16();
                info.LifeReduction   = reader.ReadUInt16();

                locations[i] = new Location(info);

                if (locations[i].IsCity)
                    locations[i].traderId = traderindex++;
            }

            Unknown3 = reader.ReadBytes(2);                                 // unknown 2 bytes

            // read player info
            player = new Player[PlayerCount];
            for (int i = 0; i < PlayerCount; i++)
            {
                PlayerInfo info = new PlayerInfo();

                info.FlagId        = reader.ReadUInt16();
                info.TravelDays    = reader.ReadUInt16();
                info.TravelCityId  = reader.ReadUInt16();
                info.Unknown1      = reader.ReadBytes(3);                   // unknown 3 bytes
                info.Color         = reader.ReadByte();

                info.CharacterId   = reader.ReadUInt16();
                info.Unknown2      = reader.ReadBytes(2);                   // unknown 2 bytes
                info.Controller    = reader.ReadUInt16();
                info.Unknown3      = reader.ReadBytes(6);                   // unknown 6 bytes
                info.IconId        = reader.ReadUInt16();

                info.Name          = reader.ReadBytes(14);

                info.Unknown4      = reader.ReadBytes(4);                   // unknown 4 bytes
                info.DayTime       = reader.ReadUInt16();
                info.PlayerId      = reader.ReadUInt16();
                info.LocationId        = reader.ReadByte();
                info.Unknown5      = reader.ReadBytes(11);                  // unknown 11 bytes
                info.FromCityId    = reader.ReadByte();
                info.Unknown6      = reader.ReadBytes(3);                   // unknown 3 bytes

                player[i] = new Player(info);

                player[i].name = "";
                for (int j = 0; j < 14; j++)
                    player[i].name += (char)info.Name[j];
                if (player[i].name.IndexOf("}") > 0)
                    player[i].name = player[i].name.Substring(0, player[i].name.IndexOf("}"));

                if (player[i].name.StartsWith("\0"))
                    player[i].name = "";
            }

            Unknown4 = reader.ReadBytes(0x190);                             // unknown 400 bytes

            // read character info
            characters = new Character[CharacterCount];
            for (int i = 0; i < CharacterCount; i++)
            {
                CharacterInfo info = new CharacterInfo();

                info.LocationId     = reader.ReadUInt16();
                info.Water          = reader.ReadByte();
                info.FaceId         = reader.ReadByte();
                info.Unknown1       = reader.ReadBytes(1);                  // unknown 1 byte
                info.Food           = reader.ReadByte();
                info.NameId         = reader.ReadUInt16();
                info.CharType       = reader.ReadByte();
                info.Unknown2       = reader.ReadBytes(1);                  // unknown 1 byte    // hire > 40
                info.EmployerId     = reader.ReadUInt16();      // hire > 49
                info.MoveX          = reader.ReadUInt16();
                info.MoveY          = reader.ReadUInt16();
                info.MoveCharId     = reader.ReadUInt16();
                info.Unknown3       = reader.ReadBytes(18);                 // unknown 18 bytes
                info.HireItemId     = reader.ReadUInt16();
                info.TextId         = reader.ReadByte();
                info.Unknown4       = reader.ReadBytes(1);                  // unknown 1 byte
                info.SpriteId       = reader.ReadByte();
                info.Unknown5       = reader.ReadBytes(3);                  // unknown 3 bytes
                info.Experience     = reader.ReadByte();
                info.Health         = reader.ReadByte();

                characters[i] = new Character(info);

                characters[i].ID = ((ushort)(i * 46 + 0x33b0));
                characters[i].ID2 = ((ushort)(i * 62 + 0x3128));

                if (characters[i].LocationId == -1 && i >= 4)
                {
                    player[characters[i].EmployerId].Group.Add(i);
                }
            }

            for (int i = 0; i < 4; i++)
                player[i].character = characters[i];

            Unknown5 = reader.ReadBytes(0xE);                               // unknown 14 bytes

            // read item info
            items = new Item[ItemCount];
            for (int i = 0; i < ItemCount; i++)
            {
                ItemInfo info = new ItemInfo();

                info.SpriteId   = reader.ReadByte();
                info.Value      = reader.ReadByte(); // Water, ...
                info.TitleId    = reader.ReadUInt16();
                info.Owner      = reader.ReadUInt16();
                info.Damage     = reader.ReadUInt16();
                info.XOrRoom    = reader.ReadUInt16(); // room index / map X position
                info.YOrNothing = reader.ReadUInt16(); // unused / map Y position

                items[i] = new Item(info);

                if (items[i].OwnerType == ItemOwnerType.Character)
                    characters[items[i].OwnerId].Items.Add(i);
            }

            return true;
        }

        public bool Save(Stream file)
        {
            if (file == null)
                return false;

            BinaryWriter writer = new BinaryWriter(file);

            writer.Write(BURN);

            writer.Write(CharIdOffset);
            writer.Write(Unknown2);

            // write global game info
            writer.Write(state.Info.ActiveID2);
            writer.Write(state.Info.ActivePlayer);
            writer.Write(state.Info.Day);
            writer.Write(state.Info.FirstCityMerchantCityId);
            writer.Write(state.Info.Unknown);
            writer.Write(state.Info.Difficulty);

            // write location info
            for (int i = 0; i < locations.Length; i++)
            {
                LocationInfo info = locations[i].Info;
                
                writer.Write(info.TitleId);
                writer.Write(info.Owner);
                writer.Write(info.Unknown1);
                writer.Write(info.Water);
                writer.Write(info.WaterCapacity);
                writer.Write(info.WaterSource);

                writer.Write(info.EntryPointX);
                writer.Write(info.EntryPointY);

                writer.Write(info.CityFlag);
                writer.Write(info.Production1);
                writer.Write(info.Production2);
                writer.Write(info.Production3);
                writer.Write(info.Production4);
                writer.Write(info.Producing);
                writer.Write(info.Unknown2);

                writer.Write(info.Neighbour1);
                writer.Write(info.Neighbour2);
                writer.Write(info.Neighbour3);
                writer.Write(info.Neighbour4);

                writer.Write(info.WayTime1);
                writer.Write(info.WayTime2);
                writer.Write(info.WayTime3);
                writer.Write(info.WayTime4);

                writer.Write(info.Way1);
                writer.Write(info.Way2);
                writer.Write(info.Way3);
                writer.Write(info.Way4);

                writer.Write(info.Unknown3);
                writer.Write(info.Danger);
                writer.Write(info.DangerAmount);
                writer.Write(info.LifeReduction);
            }

            writer.Write(Unknown3);

            // write player info
            for (int i = 0; i < player.Length; i++)
            {
                PlayerInfo info = player[i].Info;

                writer.Write(info.FlagId);
                writer.Write(info.TravelDays);
                writer.Write(info.TravelCityId);
                writer.Write(info.Unknown1);
                writer.Write(info.Color);

                writer.Write(info.CharacterId);
                writer.Write(info.Unknown2);
                writer.Write(info.Controller);
                writer.Write(info.Unknown3);
                writer.Write(info.IconId);

                writer.Write(info.Name);

                writer.Write(info.Unknown4);
                writer.Write(info.DayTime);
                writer.Write(info.PlayerId);
                writer.Write(info.LocationId);
                writer.Write(info.Unknown5);
                writer.Write(info.FromCityId);
                writer.Write(info.Unknown6);
            }

            writer.Write(Unknown4);

            // write character info
            for (int i = 0; i < characters.Length; i++)
            {
                CharacterInfo info = characters[i].Info;

                writer.Write(info.LocationId);
                writer.Write(info.Water);
                writer.Write(info.FaceId);
                writer.Write(info.Unknown1);
                writer.Write(info.Food);
                writer.Write(info.NameId);
                writer.Write(info.CharType);
                writer.Write(info.Unknown2);
                writer.Write(info.EmployerId);
                writer.Write(info.MoveX);
                writer.Write(info.MoveY);
                writer.Write(info.MoveCharId);
                writer.Write(info.Unknown3);
                writer.Write(info.HireItemId);
                writer.Write(info.TextId);
                writer.Write(info.Unknown4);
                writer.Write(info.SpriteId);
                writer.Write(info.Unknown5);
                writer.Write(info.Experience);
                writer.Write(info.Health);
            }

            writer.Write(Unknown5);

            // write item info
            for (int i = 0; i < items.Length; i++)
            {
                ItemInfo info = items[i].Info;
                
                writer.Write(info.SpriteId);
                writer.Write(info.Value);
                writer.Write(info.TitleId);
                writer.Write(info.Owner);
                writer.Write(info.Damage);
                writer.Write(info.XOrRoom);
                writer.Write(info.YOrNothing);
            }

            return true;
        }

        public void RemoveItem(Character character, int index)
        {
            if (character.Items.Count <= index)
                return;

            items[character.Items[index]].info.Owner = (ushort)0xffff;
            character.Items.RemoveAt(index);
        }

        public bool AddItem(Character character, int itemType)
        {
            int index = -1;

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].TypeId == itemType)
                    index = i;

                if (items[i].OwnerType == ItemOwnerType.Pool && items[i].TypeId == itemType)
                {
                    items[i].info.Owner = character.ID;
                    character.Items.Add(i);
                    return true;
                }
            }

            if (index == -1)
                return false;

            for (int i = items.Length - 1; i >= 0; i--)
            {
                if (items[i].OwnerType == ItemOwnerType.Pool)
                {
                    items[i].info = items[index].info;

                    items[i].info.Owner = character.ID;
                    character.Items.Add(i);
                    return true;
                }
            }

            return false;
        }

        public void SetLocation(Player player, int location)
        {
            if (player.LocationId == location)
                return;

            player.LocationId = location;
            player.Character.info.MoveX = locations[location].Info.EntryPointX;
            player.Character.info.MoveY = locations[location].Info.EntryPointY;

            for (int i = 0; i < player.Group.Count; i++)
            {
                characters[player.Group[i]].info.MoveX = locations[location].Info.EntryPointX;
                characters[player.Group[i]].info.MoveY = locations[location].Info.EntryPointY;
            }
        }
    }
}