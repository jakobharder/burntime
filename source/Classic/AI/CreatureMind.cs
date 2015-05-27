using System;
using Burntime.Classic.Logic;
using Burntime.Platform;

namespace Burntime.Classic.AI
{
    [Serializable]
    class CreatureMind : CharacterMind
    {
        protected float takeSomeRest = 0;
        protected bool first = false;

        [NonSerialized]
        protected float waitAfterAttack = 0;
        [NonSerialized]
        protected float tryToAttack = 0;
        [NonSerialized]
        protected Character attack;

        public override void Process(float elapsed)
        {
            // we are taking some rest
            if (takeSomeRest > 0)
            {
                takeSomeRest -= elapsed;
                return;
            }

            bool runAway = false;

            // attack player controlled character
            if (attack != null)
            {
                if (!attack.IsDead && (attack.Position - Owner.Position).Length < 20)
                {
                    Owner.Attack(attack);
                    attack = null;
                    runAway = true;
                }
                else
                {
                    // time out attack
                    tryToAttack += elapsed;
                    if (tryToAttack > 3)
                        attack = null;
                }
            }

            if (waitAfterAttack >= 0)
            {
                // wait for next possible attack
                waitAfterAttack -= elapsed;
            }
            else
            {
                Player player = (Player)container.Root.CurrentPlayer;
                Character sel = player.SelectedCharacter;

                if (player.Location == Owner.Location && (sel.Position - Owner.Position).Length < 200)
                {
                    // attack with a chance of 33%
                    if (Burntime.Platform.Math.Random.Next() % 3 == 0)
                    {
                        tryToAttack = 0;
                        attack = sel;
                        Owner.Path.MoveTo = sel.Position;
                    }

                    // wait 3 ~ 7 seconds for next possible attack
                    waitAfterAttack = 3 + Burntime.Platform.Math.Random.Next() % 5;
                }
            }

            // reached its goal, make some new decisions
            if (Owner.Position == Owner.Path.MoveTo || runAway)
            {
                // rest some time before decision
                if (first && !runAway)
                {
                    takeSomeRest = 0.5f;
                    first = false;
                }
                // decide to take some rest or go somewhere else 1:9
                else if (!runAway && Burntime.Platform.Math.Random.Next() % 9 == 0)
                {
                    // rest for about 2 seconds
                    takeSomeRest = 2;
                }
                else
                {
                    // just go somewhere nearby totally random
                    Vector2 go;
                    go.x = Burntime.Platform.Math.Random.Next() % 75 + 25;
                    go.y = Burntime.Platform.Math.Random.Next() % 75 + 25;

                    go.x *= (Burntime.Platform.Math.Random.Next() % 2) * 2 - 1;
                    go.y *= (Burntime.Platform.Math.Random.Next() % 2) * 2 - 1;

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
