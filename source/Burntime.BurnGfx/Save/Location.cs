using System;

namespace Burntime.Data.BurnGfx.Save
{
    public struct LocationInfo
    {
        public ushort TitleId;
        public ushort Owner;
        public byte[] Unknown1;
        public ushort Water;
        public ushort WaterCapacity;
        public ushort WaterSource;
        public ushort EntryPointX;
        public ushort EntryPointY;
        public ushort CityFlag;
        public byte Production1;
        public byte Production2;
        public byte Production3;
        public byte Production4;
        public ushort Producing;
        public byte[] Unknown2;

        public byte Neighbour1;
        public byte Neighbour2;
        public byte Neighbour3;
        public byte Neighbour4;

        public byte WayTime1;
        public byte WayTime2;
        public byte WayTime3;
        public byte WayTime4;

        public byte Way1;
        public byte Way2;
        public byte Way3;
        public byte Way4;

        public byte[] Unknown3;
        public byte Danger;
        public ushort DangerAmount;
        public ushort LifeReduction;
    };

    public class Location
    {
        LocationInfo info;
        public LocationInfo Info
        {
            get { return info; }
        }

        public int TitleId
        {
            get { return info.TitleId; }
        }

        public bool IsCity
        {
            get { return info.CityFlag != 0; }
        }

        internal int traderId = -1;
        public int TraderId
        {
            get { return traderId; }
        }

        public static readonly int MaxNeighborCount = 4;

        public int[] Neighbors
        {
            get { return new int[] { info.Neighbour1 - 1, info.Neighbour2 - 1, info.Neighbour3 - 1, info.Neighbour4 - 1 }; }
        }

        public int[] Ways
        {
            get { return new int[] { info.Way1 - 1, info.Way2 - 1, info.Way3 - 1, info.Way4 - 1 }; }
        }

        public int[] WayLengths
        {
            get { return new int[] { info.WayTime1, info.WayTime2, info.WayTime3, info.WayTime4 }; }
        }

        public int Water
        {
            get { return info.Water; }
        }

        public int WaterCapacity
        {
            get { return info.WaterCapacity; }
        }

        public int WaterSource
        {
            get { return info.WaterSource; }
        }

        public int Danger
        {
            get { return info.Danger; }
        }

        public int DangerAmount
        {
            get { return info.DangerAmount; }
        }

        public Burntime.Platform.Vector2 EntryPoint
        {
            get { return new Burntime.Platform.Vector2(info.EntryPointX, info.EntryPointY); }
        }

        public int Producing
        {
            get { return (info.Producing == 0) ? -1 : info.Producing - 51; }
        }

        public int[] Production
        {
            get { return new int[] { (info.Production1 == 1) ? 0 : -1, (info.Production2 == 1) ? 1 : -1, (info.Production3 == 1) ? 2 : -1, (info.Production4 == 1) ? 3 : -1 }; }
        }

        public Location(LocationInfo info)
        {
            this.info = info;
        }
    }
}
