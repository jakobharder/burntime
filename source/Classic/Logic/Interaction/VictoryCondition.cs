
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using Burntime.Framework.States;
using Burntime.Platform;

namespace Burntime.Classic.Logic.Interaction
{
    [Serializable]
    public class VictoryCondition : StateObject
    {
        public bool Process(Player player)
        {
            ClassicGame world = (ClassicGame)container.Root;

            // check all locations
            for (int i = 0; i < world.World.Locations.Count; i++)
            {
                Location l = world.World.Locations[i];

                // check for neighbored city
                bool neighborOfCity = false;
                for (int j = 0; j < l.Neighbors.Count; j++)
                {
                    if (l.Neighbors[j].IsCity)
                    {
                        neighborOfCity = true;
                        break;
                    }
                }

                if (neighborOfCity)
                {
                    // if the location is not possesed by the player, then he hasn't won yet
                    if (l.Player != player)
                        return false;
                }
            }

            // all city neighbors conquered, victory
            return true;
        }
    }
}
