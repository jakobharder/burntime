using System.Linq;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.Logic.Rules;

internal sealed class AmigaRules : OriginalRules
{
    public override RuleSet Id => RuleSet.Amiga;
    protected override void SetOriginalStartLocations(ClassicGame game, GameSettings settings) =>
        StartLocationPlacement.ApplyRegional(game, settings);

    public override int CalculateBossExperience(Player player, ClassicGame game)
    {
        Location[] camps = game.World.Locations.Where(location => location.Player == player).ToArray();
        int foodOutput = camps.Sum(location => location.GetFoodProductionRate().FoodPerDay);
        int waterOutput = camps.Sum(location => location.Source.Water);
        int employees = player.Group.Count + camps.Sum(location =>
            location.CampNPC.Count(character => character.Player == player && !character.IsDead));
        return RuleFormulas.AmigaBossExperience(foodOutput, waterOutput, employees);
    }

    public override bool MeetsRecruitmentExperience(Character boss, Character recruit) =>
        RuleFormulas.AmigaCanRecruit(boss.Experience, recruit.Experience);

    public override void TurnTrader(Trader trader) => RefreshTraders(new[] { trader });

    public override void TurnTraders(System.Collections.Generic.IEnumerable<Trader> traders)
    {
        Trader[] active = traders.Where(t => !t.IsDead).ToArray();
        RefreshTraders(active);
        foreach (Trader trader in active) trader.RestoreTraderHealth();
    }

    static void RefreshTraders(Trader[] traders)
    {
        if (traders.Length == 0) return;
        ClassicGame game = (ClassicGame)traders[0].Container.Root;
        OriginalItemRecords.Snapshot(game);
        OriginalTraderRefresh.AmigaRemove(traders.Select(t => t.Items).ToArray(), game.World.Day,
            game.ItemTypes.GetOriginalTitleId, Burntime.Platform.Math.Random.Next);
        foreach (Trader trader in traders)
            OriginalTraderRefresh.AmigaRestock(trader.GetAssortment().ToArray(), game.World.Day,
                game.ItemTypes.GetOriginalTitleId,
                type => OriginalItemRecords.AddTraderStock(game, trader, type), game.ItemTypes["item_meat"]);
        foreach (Trader trader in traders) trader.MoveToNextSellLocation();
    }
}
