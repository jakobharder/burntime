namespace Burntime.Remaster.Logic.Rules;

internal static class HazardRules
{
    internal static bool PassesDailyCheck(Character character) =>
        character.Location?.Danger == null || character.GetDangerRate() <= 0 ||
        ((ClassicGame)character.Container.Root).RuleBook.Settings.IsHazardImmune(
            character.Location.Danger.Type, character.FaceID);

    internal static bool ApplyContinuous(Character character, float elapsed) =>
        character.Player?.Type != PlayerType.Ai && character.ApplyContinuousHazard(elapsed);

    internal static void ApplyDaily(Character character)
    {
        if (character.IsDead)
            return;
        // Active AI parties and human parties that have started travelling avoid
        // the daily check. Stationed employees still need complete protection.
        if (character.IsWithBoss && (character.Player.Type == PlayerType.Ai ||
            character.Player.Type == PlayerType.Human && character.Player.IsTraveling))
            return;
        if (!PassesDailyCheck(character))
            character.Die();
    }
}
