using System;

namespace Burntime.Data.BurnGfx.Save
{
    public struct GameStateInfo
    {
        public ushort ActiveID2;
        public ushort ActivePlayer;
        public ushort Day;
        public ushort FirstCityMerchantCityId;
        public byte[] Unknown;
        public ushort Difficulty;
    };

    public enum Difficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    public class GameState
    {
        GameStateInfo info;
        public GameStateInfo Info
        {
            get { return info; }
        }

        public int Day
        {
            get { return info.Day; }
            set { info.Day = (ushort)value; }
        }

        public Difficulty Difficulty
        {
            get { return (Difficulty)info.Difficulty; }
            set { info.Difficulty = (ushort)value; }
        }

        public int ActivePlayer
        {
            get { return info.ActivePlayer; }
            set { info.ActivePlayer = (ushort)value; }
        }

        public GameState(GameStateInfo info)
        {
            this.info = info;
        }
    }
}
