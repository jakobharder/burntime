using System;

namespace Burntime.Remaster.Logic;

[Serializable]
public class Dog : Character
{
    public override int BaseAttackValue => Root.World.Respawn.Object.DogAttack;

    [NonSerialized]
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
        health = Root.World.Respawn.Object.DogHealth;
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        if (IsDead)
            return;

        timer += elapsed;

        if (timer >= 60)
        {
            if ((Burntime.Platform.Math.Random.Next() % 100) == 0 && Root.ItemTypes.Contains("item_dogshit"))
            {
                // drop dogshit at a change of 1% per minute
                Location.Items.DropAt(Root.ItemTypes.Generate("item_dogshit"), Position);
            }

            timer = 0;
        }
    }

    public override void Turn()
    {
        // refresh health
        Health = Root.World.Respawn.Object.DogHealth;

        base.Turn();
    }

    private ClassicGame Root => (ClassicGame)Container.Root;
}
