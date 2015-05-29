
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
using Burntime.Classic.Logic.Interaction;
using Burntime.Platform;

namespace Burntime.Classic.AI
{
    [Serializable]
    class PlayerControlledMind : CharacterMind
    {
        [NonSerialized]
        InteractionObject interactionObject;
        [NonSerialized]
        float waitForFellowers;

        public override void Process(float elapsed)
        {
            if (interactionObject == null)
                return;

            float interactionDistance = interactionObject.MaximumRange;
            if (interactionDistance == 0)
                interactionDistance = 15;

            float distance = (interactionObject.Position - Owner.Position).Length;

            // if not at destination, then go there and follow
            if (!interactionObject.IsInRange(Owner.Position))
            {
                distance = (interactionObject.Position - Owner.Path.MoveTo).Length;

                // update path only if destination position and own destination are too far away
                if (distance >= interactionDistance - 1)
                    Owner.Path.MoveTo = interactionObject.Position;

                waitForFellowers = 0;
            }
            else
            {
                bool everyOneReached = true;

                // wait until fellows reach destination
                foreach (Character character in Owner.GetGroup())
                {
                    if (!Owner.GetGroup().IsInRange(Owner, character))
                    {
                        everyOneReached = false;
                        break;
                    }
                }

                // everyone reached or waited for 30 seconds
                if (everyOneReached || waitForFellowers >= 30)
                {
                    // do interaction
                    interactionObject.Interact(Owner);

                    // release object
                    interactionObject = null;
                }
                else
                    waitForFellowers += elapsed;
            }
        }

        public override void MoveToObject(InteractionObject obj)
        {
            interactionObject = obj;
        }
    }
}
