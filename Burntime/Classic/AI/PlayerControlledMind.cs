
#region GNU General Public License - Burntime
/*
 *  Burntime
 *  Copyright (C) 2008-2011 Jakob Harder
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
