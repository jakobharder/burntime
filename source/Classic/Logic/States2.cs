using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Framework.States;
using Burntime.Data.BurnGfx;

namespace Burntime.Classic
{
    [Serializable]
    public class World : StateObject
    {
        public Map Map;
        public Ways Ways;
        public StateLinkList<Player> Players;
        public StateLinkList<Location> Locations;
        public StateLinkList<Character> AllCharacters;
        public int ActivePlayer;
    }

    [Serializable]
    public class Map : StateObject
    {
        public DataID<MapData> MapData;
    }

    [Serializable]
    public class Ways : StateObject
    {
        public DataID<WayData> WayData;
    }

    public enum CharClass
    {
        Mercenary,
        Technician,
        Doctor,
        Boss,
        None,
        Trader,
    }

    [Serializable]
    public class Character : StateObject
    {
        public int FaceID;
        public Vector2 Position;
        public PathFinding.PathState Path;
        public DataID<Platform.Graphics.Sprite> Body;
        public StateLinkList<Item> Items;
        public CharClass Class;

        public int Health;
        public int Experience;
        public int Food;
        public int Water;
    }

    [Serializable]
    public class Fog : StateObject
    {
    }

    [Serializable]
    public class Player : StateObject
    {
        public String Name;
        public int FaceID
        {
            get { return Character.FaceID; }
        }
        public PixelColor Color;
        
        StateLink city;
        public Location Location
        {
            get { return city.Get(container) as Location; }
            set { city = value; }
        }

        public int IconID;

        StateLinkList<Fog> Fogs;
        StateLinkList<Group> Group;

        StateLink character;
        public Character Character
        {
            get { return character.Get(container) as Character; }
            set { character = value; }
        }


        public bool IsDead
        {
            get { return false; }
        }
    }

    [Serializable]
    public class Group : StateObject
    {
        StateLink Leader;
        StateLinkList<Character> Characters;
    }

    [Serializable]
    public class Item : StateObject
    {
        public int SpriteID;
        public int TitleID;
    }

    [Serializable]
    public class Location : StateObject
    {
        public int Id;
        public static implicit operator int(Location Right)
        {
            return Right.Id;
        }

        public int WaterCapacity;
        public int WaterSource;
        public int WaterReserve;

        public int Production;
        public int[] AvailableProducts;

        public int Danger;
        public int DangerValue;

        public bool IsCity;
        public Vector2 EntryPoint;

        public Map Map;
        public StateLinkList<Room> Rooms;
        public StateLinkList<Character> Characters;
        public StateLinkList<Item> Items;
        //StateList CampCharacters;
    }

    [Serializable]
    public class Room : StateObject
    {
        public StateLinkList<Item> Items;
        public bool IsWaterSource;
    }
}
