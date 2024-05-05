using System;

namespace Burntime.Remaster.Logic;

[Serializable]
public class Mutant : Character
{
    public override int BaseAttackValue => Root.World.Respawn.Object.MutantAttack;

    public override void Die()
    {
        // drop special item
        var item = Root.ItemTypes.GenerateClass(new string[] { "material", "rare", "useless" }, new string[] { "nodrop" }, 0.33f);
        if (item is not null)
            Location?.Items.DropAt(item, Position);

        base.Die();
    }

    public override void Revive()
    {
        base.Revive();

        // set full heatlh
        health = Root.World.Respawn.Object.MutantHealth;
    }

    public override void Turn()
    {
        // refresh health
        Health = Root.World.Respawn.Object.MutantHealth;

        base.Turn();
    }

    private ClassicGame Root => (ClassicGame)Container.Root;
}
