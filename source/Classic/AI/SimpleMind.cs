
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
using Burntime.Classic.Logic;
using Burntime.Platform;

namespace Burntime.Classic.AI
{
    [Serializable]
    class SimpleMind : CharacterMind
    {
        protected float takeSomeRest = 0;
        protected bool first = false;

        public override void Process(float elapsed)
        {
            // we are taking some rest
            if (takeSomeRest > 0)
            {
                takeSomeRest -= elapsed;
                return;
            }

            // reached its goal, make some new decisions
            if (Owner.Position == Owner.Path.MoveTo)
            {
                // rest some time before decision
                if (first)
                {
                    takeSomeRest = 0.5f;
                    first = false;
                }
                // decide to take some rest or go somewhere else 1:9
                else if (Burntime.Platform.Math.Random.Next() % 9 == 0)
                {
                    // rest for about 2 seconds
                    takeSomeRest = 2;
                }
                else
                {
                    // just go somewhere nearby totally random
                    Vector2 maskpos;
                    Vector2 go;

                    go.x = Burntime.Platform.Math.Random.Next() % 75 + 25;
                    go.y = Burntime.Platform.Math.Random.Next() % 75 + 25;

                    go.x *= (Burntime.Platform.Math.Random.Next() % 2) * 2 - 1;
                    go.y *= (Burntime.Platform.Math.Random.Next() % 2) * 2 - 1;

                    maskpos = ((Vector2)Owner.Position + go + (Owner.Location.Map.Mask.Resolution / 2 - 1)) / Owner.Location.Map.Mask.Resolution;

                    // revert direction if goal is possible map border
                    if (!Owner.Location.Map.Mask[maskpos])
                    {
                        go.x *= -1;
                        go.y *= -1;
                    }

                    Owner.Path.MoveTo = Owner.Position + go;

                    first = true;
                }
            }
        }

        public override void RequestToTalk()
        {
            // cancel walking
            Owner.Path.MoveTo = Owner.Position;

            // wait some time
            takeSomeRest = 4;
        }
    }
}
