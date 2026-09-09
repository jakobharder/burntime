using System.Linq;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.Logic.Rules;

internal sealed class DosRules : OriginalRules
{
    public override RuleSetId Id => RuleSetId.Dos;
    protected override void SetOriginalStartLocations(ClassicGame game, GameSettings settings) =>
        StartLocationPlacement.ApplyRotatingOriginalGroups(game, settings);

    public override int CalculateBossExperience(Player player, ClassicGame game) =>
        RuleFormulas.DosBossExperience(player.GetOwnedLocationCount(game.World));

    public override bool MeetsRecruitmentExperience(Character boss, Character recruit) =>
        RuleFormulas.DosCanRecruit(boss.Experience, recruit.Experience);

    public override int CalculateWaterOutput(int baseOutput, bool handPump, bool industrialPump) =>
        RuleFormulas.DosWaterOutput(baseOutput, handPump, industrialPump);

    public override void TurnTrader(Trader trader)
    {
        ClassicGame game = (ClassicGame)trader.Container.Root;
        OriginalItemRecords.Snapshot(game);
        OriginalTraderRefresh.Dos(trader.Items, trader.GetAssortment().ToArray(), game.World.Day,
            game.ItemTypes.GetOriginalTitleId, Burntime.Platform.Math.Random.Next,
            type => OriginalItemRecords.AddTraderStock(game, trader, type), game.ItemTypes["item_meat"]);
        trader.MoveToNextSellLocation();
    }
}
