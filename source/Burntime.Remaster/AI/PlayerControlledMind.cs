using System;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Interaction;
using Burntime.Platform;

namespace Burntime.Remaster.AI
{
    [Serializable]
    class PlayerControlledMind : CharacterMind
    {
        const float FollowerWaitTimeout = 10;

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

                // Group movement waits until the followers reach the destination.
                if (Owner.Player != null && !Owner.Player.SingleMode)
                {
                    foreach (Character character in Owner.GetGroup())
                    {
                        if (!Owner.GetGroup().IsInRange(Owner, character))
                        {
                            everyOneReached = false;
                            break;
                        }
                    }
                }

                // everyone reached or the follower wait timed out
                if (everyOneReached || waitForFellowers >= FollowerWaitTimeout)
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
