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

            if ((Burntime.Platform.Math.Random.Next() % 100) < 3)
            {
                // drop cookies instead at a chance of 3%
                string[] cookies = root.ItemTypes.GetTypesWithClass(new string[] { "cookie" }, new string[0]);
                Location.Items.DropAt(root.ItemTypes.Generate(cookies[Burntime.Platform.Math.Random.Next() % cookies.Length]), Position);
            }

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
