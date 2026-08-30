using System;

namespace Burntime.Remaster.Logic;

[Serializable]
public class Mutant : Character
{
    public override int BaseAttackValue => Root.World.Respawn.Object.MutantAttack;

    public override void Die()
    {
        var dropSettings = Root.World.Respawn.Object;
        var item = Root.ItemTypes.GenerateClass(dropSettings.MutantDropType, new string[] { "nodrop" }, dropSettings.MutantDropChance);
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
