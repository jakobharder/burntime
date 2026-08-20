using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static partial class LocalOpportunities
{
    public static void Apply(ClassicAiState state)
    {
        UseLocalWaterSource(state);
        RemoveAdviceItems(state);
        EconomicSupport.ApplySlumpSupport(state);
        RefillConstructionReserve(state);
        ConstructPortableEconomicUpgrade(state);
        EquipEmpire(state);

        ExpansionTask.TryClaimCurrentAsLocalOpportunity(state);

        if (state.Current.Player == state.Player)
        {
            ProvisionGroupFromCampSurplus(state, state.Current);
            MaintainCurrentCamp(state);
            if (TradeTask.ShouldVisitTrader(state))
                FillCityCaravan(state, state.Current);
        }

        ConstructPortableWeapon(state);

        // An affordable settler is the immediate local prerequisite for the
        // planned camp. Reserve that real payment bundle before ordinary city
        // barter can consume it; recruitment remains the turn's strategic action.
        if (!state.ShouldReserveSettlerPayment)
        {
            foreach (Trader trader in TradeTask.EncounteredTraders(state))
            {
                TradeTask.TradeWithTrader(state, trader);
                RefillConstructionReserve(state);
                ConstructPortableEconomicUpgrade(state);
                ConstructPortableWeapon(state);
            }
        }

        // A purchase or construction may satisfy an equipment need immediately.
        EquipEmpire(state);
    }

    static void UseLocalWaterSource(ClassicAiState state)
    {
        Player player = state.Player;
        if (player.Location.Source != null)
            player.Location.Source.Reserve = player.Group.Drink(
                player.Character, player.Location.Source.Reserve);

        foreach (Item item in player.Group.GetEmptyWaterItems())
            player.Location.Source.RefillItem(item);
    }
}
