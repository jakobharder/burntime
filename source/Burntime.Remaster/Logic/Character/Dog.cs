using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Remaster.Logic
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

                if ((Burntime.Platform.Math.Random.Next() % 100) == 0 && root.ItemTypes.Contains("item_dogshit"))
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
