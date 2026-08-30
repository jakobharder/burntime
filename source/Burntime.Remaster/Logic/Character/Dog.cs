using System;

namespace Burntime.Remaster.Logic;

[Serializable]
public class Dog : Character
{
    public override int BaseAttackValue => Root.World.Respawn.Object.DogAttack;

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

    public override void Turn()
    {
        // refresh health
        Health = Root.World.Respawn.Object.DogHealth;

        base.Turn();
    }

    private ClassicGame Root => (ClassicGame)Container.Root;
}
