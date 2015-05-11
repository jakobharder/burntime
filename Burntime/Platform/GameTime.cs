/*
 *  Burntime Platform
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
 *  authors: 
 *    Juernjakob Harder (yn.harada@gmail.com)
 * 
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;

namespace Burntime.Platform
{
    public class GameTime
    {
        public float Elapsed = 0;
        long TimeStamp;

        public GameTime()
        {
            Reset();
        }

        public void Reset()
        {
            Elapsed = 0;
            TimeStamp = Stopwatch.GetTimestamp();
        }

        public void Refresh(int MinimumElapsed)
        {
            long last = TimeStamp;

            TimeStamp = Stopwatch.GetTimestamp();
            Elapsed = ((TimeStamp - last) / (float)Stopwatch.Frequency);
            if (Elapsed < MinimumElapsed)
            {
                Thread.Sleep(MinimumElapsed - 1 - (int)Elapsed);
                TimeStamp = Stopwatch.GetTimestamp();
                Elapsed = ((TimeStamp - last) / (float)Stopwatch.Frequency);
            }
        }
    }

}
