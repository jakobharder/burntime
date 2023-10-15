using System;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Interaction;
using Burntime.Platform;

namespace Burntime.Remaster.AI
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
