using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static partial class LocalOpportunities
{
    public static void Apply(ClassicAiState state)
    {
        RecoveryServices.ApplyCityMinimum(state);
        UseLocalWaterSource(state);
        RemoveAdviceItems(state);

        if (state.Current.Player == state.Player)
            ProvisionGroupFromCampSurplus(state, state.Current);
        if (ConsumeAvailableSupplies(state))
            AiTelemetry.Report(state.Player,
                "consumed carried or stored supplies before seeking paid recovery");
        RecoveryServices.UseDoctor(state);

        EconomicSupport.ApplySlumpSupport(state);
        RefillConstructionReserve(state);
        ConstructPortableEconomicUpgrade(state);
        EquipEmpire(state);

        ExpansionTask.TryClaimCurrentAsLocalOpportunity(state);

        if (state.Current.Player == state.Player)
        {
            MaintainCurrentCamp(state);
            if (TradeTask.ShouldVisitTrader(state))
                FillCityCaravan(state, state.Current);
        }

        ConstructPortableWeapon(state);

        // An affordable settler is the immediate local prerequisite for the
        // planned camp. Reserve that real payment bundle before ordinary city
        // barter can consume it; recruitment remains the turn's strategic action.
        if (!state.ShouldReserveSettlerPayment || RecoveryServices.NeedsDoctorPayment(state))
        {
            foreach (Trader trader in TradeTask.EncounteredTraders(state))
            {
                TradeTask.TradeWithTrader(state, trader);
                RefillConstructionReserve(state);
                ConstructPortableEconomicUpgrade(state);
                ConstructPortableWeapon(state);
            }
        }

        // A trader may have supplied the food item required by the local doctor.
        // Resolve that local opportunity before selecting a regional action.
        RecoveryServices.UseDoctor(state);

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
