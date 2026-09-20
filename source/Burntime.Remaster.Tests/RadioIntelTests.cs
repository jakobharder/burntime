using System.Collections.Generic;
using Burntime.Remaster.Logic.Generation;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.Tests;

using static Program;

static class RadioIntelTests
{
    internal static IEnumerable<Case<int>> RadioCases()
    {
        yield return Int("no defenders", 0, () => RadioIntel.ThreatLevel(0));
        yield return Int("low defense", 1, () => RadioIntel.ThreatLevel(25));
        yield return Int("moderate defense", 2, () => RadioIntel.ThreatLevel(26));
        yield return Int("high defense", 3, () => RadioIntel.ThreatLevel(75));
        yield return Int("extreme defense", 4, () => RadioIntel.ThreatLevel(100));
        yield return Int("defense rating is capped", 4, () => RadioIntel.ThreatLevel(500));
        yield return Int("radio availability follows carrier, target and rules", 0, () =>
        {
            var (_, player, _, manager) = EncounterPlayers(RuleSet.Extended);
            var target = manager.Create<Location>();
            player.Character.Items.Add(TestItem(manager, "item_two_way_radio",
                functions: ItemFunction.RemoteIntel));
            Equal(true, RadioIntel.IsAvailable(player, target),
                "carried extended radio reports remote camp");
            target.Player = player;
            Equal(false, RadioIntel.IsAvailable(player, target),
                "owned camp keeps normal info");
            target.Player = null;
            target.IsCity = true;
            Equal(false, RadioIntel.IsAvailable(player, target),
                "cities do not receive camp reports");
            var (_, classicPlayer, _, classicManager) = EncounterPlayers(RuleSet.Classic);
            var classicTarget = classicManager.Create<Location>();
            classicPlayer.Character.Items.Add(TestItem(classicManager, "item_two_way_radio"));
            Equal(false, RadioIntel.IsAvailable(classicPlayer, classicTarget),
                "classic radio remains unchanged");
            return 0;
        });
    }
}
