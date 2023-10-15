using System;
using Burntime.Remaster.Logic;
using Burntime.Platform;

namespace Burntime.Remaster.AI
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
