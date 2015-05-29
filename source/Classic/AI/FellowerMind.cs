
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
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform;

namespace Burntime.Classic.AI
{
    [Serializable]
    class FellowerMind : CharacterMind
    {
        protected StateLink<Character> leader;
        public Character Leader
        {
            get { return leader; }
            set { leader = value; }
        }

        protected override void InitInstance(object[] parameter)
        {
            if (parameter == null || parameter.Length < 2 || !(parameter[1] is Character))
                throw new InvalidStateObjectConstruction(this);

            leader = parameter[1] as Character;

            base.InitInstance(parameter);
        }

        public override void Process(float elapsed)
        {
            if (Leader.Player.SingleMode)
                return;

            float distance = (Leader.Position - Owner.Position).Length;
            
            // if too far from leader, then follow
            if (distance > 150)
            {
                distance = (Leader.Position - Owner.Path.MoveTo).Length;

                // update path only if leader position and own destination are too far away
                if (distance > 140)
                {
                    double r = (Burntime.Platform.Math.Random.Next() % 360) * System.Math.PI / 180;
                    Vector2 margin;
                    margin.x = (int)(System.Math.Sin(r) * 14);
                    margin.y = (int)(System.Math.Cos(r) * 14);

                    Owner.Path.MoveTo = Leader.Position;// +margin;
                }
            }
            else if (distance > 30)
            {
                distance = (Leader.Position - Owner.Path.MoveTo).Length;

                // update path only if leader position and own destination are too far away
                if (distance > 20)
                {
                    double r = (Burntime.Platform.Math.Random.Next() % 360) * System.Math.PI / 180;
                    Vector2 margin;
                    margin.x = (int)(System.Math.Sin(r) * 14);
                    margin.y = (int)(System.Math.Cos(r) * 14);

                    Owner.Path.MoveTo = Leader.Position;// +margin;
                }
            }
            else if (distance < 15)
            {
                distance = (Leader.Position - Owner.Path.MoveTo).Length;

                // update path only if leader position and own destination are too close
                if (distance < 10)
                {
                    double r = (Burntime.Platform.Math.Random.Next() % 360) * System.Math.PI / 180;
                    Vector2 margin;
                    margin.x = (int)(System.Math.Sin(r) * 14);
                    margin.y = (int)(System.Math.Cos(r) * 14);

                    Owner.Path.MoveTo = Leader.Position + margin;
                }
            }
        }
    }
}
