
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
using System.Collections.Generic;
using System.Text;

namespace Burntime.Classic.Logic
{
    [Serializable]
    public class Dog : Character
    {
        protected float timer = 0;

        public override void Die()
        {
            // drop meat
            ClassicGame root = (ClassicGame)Container.Root;

            Location.Items.DropAt(root.ItemTypes.Generate("item_meat"), Position);

            base.Die();
        }

        public override void Revive()
        {
            base.Revive();

            // set full heatlh
            health = 31;
        }

        public override void Update(float elapsed)
        {
            base.Update(elapsed);

            if (IsDead)
                return;

            timer += elapsed;

            if (timer >= 60)
            {
                ClassicGame root = (ClassicGame)Container.Root;

                if ((Burntime.Platform.Math.Random.Next() % 100) == 0)
                {
                    // drop dogshit at a change of 1% per minute
                    Location.Items.DropAt(root.ItemTypes.Generate("item_dogshit"), Position);
                }

                timer = 0;
            }
        }

        public override void Turn()
        {
            // refresh health
            Health = 31;

            base.Turn();
        }
    }
}
