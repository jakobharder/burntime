using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Classic.Logic
{
    [Serializable]
    public class Mutant : Character
    {
        public override void Die()
        {
            // drop special item
            ClassicGame root = (ClassicGame)Container.Root;

            base.Die();
        }

        public override void Revive()
        {
            base.Revive();

            // set full heatlh
            health = 31;
        }

        public override void Turn()
        {
            // refresh health
            Health = 31;

            base.Turn();
        }
    }
}
