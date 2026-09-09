using Burntime.Framework;
using Burntime.Remaster.Logic.Generation;
using Burntime.Data.BurnGfx.Save;

namespace Burntime.Remaster.Logic.Rules;

internal interface IGameRules
{
    RuleSetId Id { get; }
    GameSettings Settings { get; }

    void InitializeHumanPlayer(Player player, int playerIndex, GameSettings settings,
        Burntime.Data.BurnGfx.Save.SaveGame source);
    void SetStartLocations(ClassicGame game, GameSettings settings,
        Burntime.Data.BurnGfx.Save.SaveGame source);
    void PopulateInitialItems(ClassicGame game, GameSettings settings,
        Burntime.Data.BurnGfx.Save.SaveGame source);
    void InitializeRecruit(Character recruit, Player boss, Item? payment);
    void InitializeTraderInventory(Trader trader);
    void TurnTrader(Trader trader);
    void TurnTraders(System.Collections.Generic.IEnumerable<Trader> traders)
    {
        foreach (Trader trader in traders) trader.Turn();
    }
    void ProcessFoodProduction(Location location, Production.Rate production);
    bool CanCreateItem(ClassicGame game);

    int CalculateBossExperience(Player player, ClassicGame game);
    bool MeetsRecruitmentExperience(Character boss, Character recruit);
    void TurnEmployedCharacter(Character character);
    bool PassesDailyHazardCheck(Character character);
    bool ApplyContinuousHazard(Character character, float elapsed);
    int CalculateDoctorResult(int health, IItemCollection payment);
    int CalculateRestaurantValue(IItemCollection payment);
    int CalculatePubValue(IItemCollection payment);
    bool AcceptTrade(IItemCollection playerOffer, IItemCollection traderOffer, int difficulty);
    int CalculateWaterOutput(int baseOutput, bool handPump, bool industrialPump);
    void DealAttackDamage(Character attacker, Character defender, bool useAmmo);
    int GetExperienceTier(int experience);
    CombatPreview GetCombatPreview(Character character);
    float CalculateStrategicStrength(Character character, bool detailed);
    /// <summary>
    /// Rolls one off-screen attack. A non-positive result means that this
    /// combat model cannot damage the defender with the current matchup.
    /// </summary>
    int RollStrategicDamage(
        ClassicGame game,
        Player attackerOwner,
        Character attacker,
        Character defender);
}
