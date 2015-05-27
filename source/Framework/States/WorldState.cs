/*
 *  Burntime Framework
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

namespace Burntime.Framework.States
{
    [Serializable]
    public abstract class WorldState : StateObject, ITurnable
    {
        public virtual void Turn()
        {
            foreach (PlayerState player in Player)
            {
                player.isFinished = false;
            }
        }

        public abstract StateObject CurrentPlayer { get; }
        public abstract StateObject CurrentLocation { get; }

        public abstract PlayerState[] Player { get; }
        public abstract int CurrentPlayerIndex { get; }

        public abstract PlayerState CheckWinner();
    }
}
