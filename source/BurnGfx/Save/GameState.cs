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
