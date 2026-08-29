using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class Construction
{
    internal static void ConstructPortableEconomicUpgrade(ClassicAiState state)
    {
        string[] wanted = Trading.UsefulConstructionOpportunities(state)
            .Where(opportunity => opportunity.Result is
                "item_trap" or "item_rat_trap" or "item_protective_suit")
            .OrderByDescending(opportunity => opportunity.EconomicValue)
            .Select(opportunity => opportunity.Result)
            .Distinct()
            .ToArray();
        if (wanted.Length == 0)
            return;

        List<IItemCollection> sources = state.Player.Group
            .Select(character => (IItemCollection)character.Items)
            .ToList();
        Item result = state.RootGame.Constructions.TryConstructAny(
            sources, state.Reserve, state.RootGame, wanted);
        if (result == null)
            return;

        CampManagement.StoreInReserveOrAtLocation(state, result);
        AiTelemetry.Report(state.Player,
            $"assembled {result.ID} from shared construction materials");
    }

    internal static void RefillConstructionReserve(ClassicAiState state)
    {
        List<(IItemCollection Owner, Item Item)> available = new();
        if (state.Current.Player == state.Player)
        {
            available.AddRange(state.Current.Rooms
                .SelectMany(room => room.Items.Select(item => ((IItemCollection)room.Items, item))));
            available.AddRange(state.Current.CampNPC
                .Where(character => character.Player == state.Player)
                .SelectMany(character => character.Items
                    .Where(item => character.Weapon != item && character.Protection != item)
                    .Select(item => ((IItemCollection)character.Items, item))));
        }
        available.AddRange(state.Player.Group
            .SelectMany(character => character.Items
                .Where(item => character.Weapon != item && character.Protection != item)
                .Select(item => ((IItemCollection)character.Items, item))));

        List<string> reserved = new();
        foreach (string itemId in AiItemPool.ConstructionMaterialIds)
        {
            if (state.Reserve.GetConstructionMaterialCount(itemId) > 0)
                continue;

            (IItemCollection Owner, Item Item) candidate = available
                .FirstOrDefault(entry => entry.Item.ID == itemId);
            if (candidate.Item == null || !state.Reserve.TryReserveConstructionMaterial(candidate.Item))
                continue;

            candidate.Owner.Remove(candidate.Item);
            available.Remove(candidate);
            reserved.Add(itemId);
        }

        if (reserved.Count > 0)
            AiTelemetry.Report(state.Player,
                $"reserved construction materials: {string.Join(", ", reserved)}");
    }

}
