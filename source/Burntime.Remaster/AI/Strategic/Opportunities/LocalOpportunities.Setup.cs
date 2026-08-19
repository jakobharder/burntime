using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static partial class LocalOpportunities
{
    internal static void ConstructPortableEconomicUpgrade(ClassicAiState state)
    {
        string[] wanted = TradeTask.UsefulConstructionOpportunities(state)
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
            sources, state.Pool, state.RootGame, wanted);
        if (result == null)
            return;

        state.Pool.Insert(result);
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
            if (state.Pool.GetConstructionMaterialCount(itemId) > 0)
                continue;

            (IItemCollection Owner, Item Item) candidate = available
                .FirstOrDefault(entry => entry.Item.ID == itemId);
            if (candidate.Item == null || !state.Pool.TryReserveConstructionMaterial(candidate.Item))
                continue;

            candidate.Owner.Remove(candidate.Item);
            available.Remove(candidate);
            reserved.Add(itemId);
        }

        if (reserved.Count > 0)
            AiTelemetry.Report(state.Player,
                $"reserved construction materials: {string.Join(", ", reserved)}");
    }

    internal static void RemoveAdviceItems(ClassicAiState state)
    {
        IEnumerable<IItemCollection> inventories = state.Player.Group
            .Select(character => (IItemCollection)character.Items)
            .Concat(state.RootGame.World.Locations
                .Where(location => location.Player == state.Player)
                .SelectMany(location => location.Rooms.Select(room => (IItemCollection)room.Items)
                    .Concat(location.CampNPC
                        .Where(character => character.Player == state.Player)
                        .Select(character => (IItemCollection)character.Items))));
        foreach (IItemCollection inventory in inventories)
        {
            foreach (Item advice in inventory.Where(item => item.ID == "item_advice").ToArray())
                inventory.Remove(advice);
        }
    }

    internal static void EquipEmpire(ClassicAiState state)
    {
        Player player = state.Player;
        var camps = state.RootGame.World.Locations.Where(location => location.Player == player).ToArray();
        Character[] group = player.Group.Where(character => !character.IsDead).ToArray();
        NormalizeWeaponLimits(state, group, isCamp: false);
        NormalizeCarriedProtection(state, group);
        foreach (Location camp in camps)
            NormalizeWeaponLimits(state,
                camp.CampNPC.Where(npc => npc.Player == player && !npc.IsDead).ToArray(), isCamp: true);

        var frontierGuards = camps.Where(location => ReinforcementTask.IsThreatened(state, location))
            .SelectMany(location => location.CampNPC.Where(npc => npc.Player == player && !npc.IsDead))
            .ToArray();
        var rearGuards = camps.Where(location => !ReinforcementTask.IsThreatened(state, location))
            .SelectMany(location => location.CampNPC.Where(npc => npc.Player == player && !npc.IsDead))
            .ToArray();

        // Keep one weapon on every traveller before spending weapons on camp upgrades.
        // This standing reserve is maintained even when no attack is being prepared.
        foreach (Character traveller in group)
            EquipWeapon(state, traveller, group, isCamp: false,
                upgradeWeakWeapon: false, traveller == player.Character ? "leader" : "follower");
        TransferRearCampWeapons(state, group, group);
        foreach (Character guard in frontierGuards)
            EquipWeapon(state, guard,
                guard.Location.CampNPC.Where(npc => npc.Player == player && !npc.IsDead).ToArray(),
                isCamp: true, upgradeWeakWeapon: true, "frontier guard");
        if (state.HasAttackPlan)
        {
            foreach (Character traveller in group)
                EquipWeapon(state, traveller, group, isCamp: false,
                    upgradeWeakWeapon: true, traveller == player.Character ? "leader" : "follower");
        }
        StockCurrentCampWeaponReserve(state);
        foreach (Location camp in camps.Where(location => location.Danger != null))
        {
            foreach (Character guard in camp.CampNPC.Where(npc => npc.Player == player && !npc.IsDead))
                EquipDangerProtection(state, guard, camp);
        }

        // The AI pool is shared empire-wide. Put its best compatible production
        // tools to work immediately instead of leaving them in hidden stock.
        Location[] productionPriority = camps
            .OrderByDescending(location => LocalOpportunities.ShouldPreferProductionAtCamp(state, location))
            .ThenBy(location => location.GetFoodProductionRate().FoodPerDay)
            .ToArray();
        // Two passes let a better trap displace a lower tier and immediately
        // cascade that reclaimed tool into an earlier compatible weak camp.
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (Location camp in productionPriority)
            {
                // Captured camps can already contain a compatible tool. Production
                // selection belongs to the previous owner, so refresh it even when
                // nothing new needs to be taken from the shared pool.
                camp.AutoSelectFoodProduction(onlyIfStarving: false);
                InstallProductionFromPool(state, camp);
                TradeTask.CollectRedundantProductionTools(state, camp);
                camp.AutoSelectFoodProduction(onlyIfStarving: false);
            }
        }

        CarryStandingWaterContainers(state, group);
        foreach (Character npc in frontierGuards.Concat(rearGuards).Distinct())
        {
            if (TradeTask.HasWaterContainer(npc) || npc.Items.IsFull || !state.Pool.HasWaterContainer())
                continue;
            Item container = state.Pool.GetBestWaterContainer();
            npc.Items.Add(container);
            AiTelemetry.Report(player, $"equipped {npc.Name} with {container.ID}");
        }

        CarryStrategicProtection(state);
    }

    internal static void CarryStandingWaterContainers(ClassicAiState state, IReadOnlyCollection<Character> group)
    {
        while (TradeTask.PortableWaterSupply(state) < TradeTask.DesiredPortableWaterCapacity(state) &&
            state.Pool.HasWaterContainer())
        {
            Character carrier = group
                .Where(character => !character.Items.IsFull)
                .OrderBy(character => character.Items
                    .Where(item => AiItemPool.IsWaterContainer(item.Type))
                    .Sum(item => AiItemPool.WaterContainerCapacity(item.Type)))
                .FirstOrDefault();
            if (carrier == null)
                break;
            Item container = state.Pool.GetBestWaterContainer();
            carrier.Items.Add(container);
            AiTelemetry.Report(state.Player,
                $"added standing water reserve {container.ID} to {carrier.Name}");
        }
    }

    internal static void TransferRearCampWeapons(
        ClassicAiState state,
        IEnumerable<Character> travellers,
        IReadOnlyCollection<Character> group)
    {
        Location camp = state.Current;
        if (camp.Player != state.Player || ReinforcementTask.IsThreatened(state, camp) ||
            state.WasRecentlyContested(camp))
            return;

        foreach (Character traveller in travellers.Where(character =>
            (character.Items.FindBestWeapon()?.DamageValue ?? 0) == 0))
        {
            var stored = camp.Rooms
                .SelectMany(room => room.Items.Select(item => new { Room = room, Item = item }))
                .Where(entry => IsMeleeWeapon(entry.Item) &&
                    WeaponAllowed(state, group, traveller, isCamp: false, entry.Item.Type))
                .OrderBy(entry => entry.Item.DamageValue)
                .FirstOrDefault();
            if (stored != null)
            {
                stored.Room.Items.Remove(stored.Item);
                state.Pool.Insert(stored.Item);
                AiTelemetry.Report(state.Player,
                    $"withdrew reserve {stored.Item.ID} from {camp.Title} for the travelling group");
                EquipWeapon(state, traveller, group, isCamp: false,
                    upgradeWeakWeapon: false,
                    traveller == state.Player.Character ? "leader" : "follower");
                continue;
            }

            Character guard = camp.CampNPC
                .Where(character => character.Player == state.Player && !character.IsDead)
                .Where(character => character.Items.FindBestWeapon() is Item weapon &&
                    !AiItemPool.IsFirearm(weapon.Type) &&
                    WeaponAllowed(state, group, traveller, isCamp: false, weapon.Type))
                .OrderBy(character => character.Items.FindBestWeapon()!.DamageValue)
                .FirstOrDefault();
            if (guard == null)
                return;

            Item transferred = guard.Items.FindBestWeapon();
            if (guard.Weapon == transferred)
                guard.Weapon = null;
            guard.Items.Remove(transferred);
            state.Pool.Insert(transferred);
            AiTelemetry.Report(state.Player,
                $"transferred {transferred.ID} from rear guard {guard.Name} at {camp.Title} " +
                $"to the travelling group");
            EquipWeapon(state, traveller, group, isCamp: false,
                upgradeWeakWeapon: false,
                traveller == state.Player.Character ? "leader" : "follower");
        }
    }

    internal static void StockCurrentCampWeaponReserve(ClassicAiState state)
    {
        Location camp = state.Current;
        if (camp.Player != state.Player)
            return;

        while (CampStoredWeaponCount(camp) < LocalOpportunities.CampWeaponReserve && state.Pool.HasWeapon() &&
            camp.Rooms.Any(room => !room.Items.IsFull))
        {
            Item weapon = state.Pool.GetBestWeapon(type => !AiItemPool.IsFirearm(type));
            if (weapon == null)
                return;
            camp.StoreItemRandom(weapon);
            AiTelemetry.Report(state.Player,
                $"stored reserve weapon {weapon.ID} at {camp.Title}");
        }
    }

    internal static bool IsMeleeWeapon(Item item) =>
        item.DamageValue > 0 && !AiItemPool.IsFirearm(item.Type);

    internal static int CampStoredWeaponCount(Location camp) => camp.Rooms
        .SelectMany(room => room.Items)
        .Count(IsMeleeWeapon);

    internal static HashSet<Item> CampStoredWeaponReserveItems(Location camp) => camp.Rooms
        .SelectMany(room => room.Items)
        .Where(IsMeleeWeapon)
        .OrderByDescending(item => item.DamageValue)
        .Take(LocalOpportunities.CampWeaponReserve)
        .ToHashSet();

    internal static void EquipWeapon(
        ClassicAiState state,
        Character character,
        IReadOnlyCollection<Character> unit,
        bool isCamp,
        bool upgradeWeakWeapon,
        string role)
    {
        Item current = character.Items.FindBestWeapon();
        int currentDamage = current?.DamageValue ?? 0;
        int desiredMinimum = upgradeWeakWeapon && currentDamage < 33 ? currentDamage : currentDamage > 0 ? int.MaxValue : 0;
        bool reserveProductionTool = ExpansionTask.ShouldReserveProductionTool(state);
        bool allowProductionTool = currentDamage == 0 || !reserveProductionTool;
        bool Allowed(ItemType type) => WeaponAllowed(state, unit, character, isCamp, type);
        if (desiredMinimum == int.MaxValue ||
            !state.Pool.HasBetterWeapon(desiredMinimum, allowProductionTool, Allowed))
        {
            if (current != null)
                character.Weapon = current;
            return;
        }

        Item weapon = state.Pool.GetBestWeapon(Allowed, desiredMinimum, allowProductionTool);
        if (weapon == null)
            return;

        if (character.Items.IsFull && current != null)
        {
            character.Items.Remove(current);
            state.Pool.Insert(current);
        }
        else if (character.Items.IsFull)
        {
            Item replaceable = character.Items
                .Where(item => TradeTask.CanSell(state, item))
                .OrderBy(item => CargoRetentionValue(state, item))
                .ThenBy(item => item.TradeValue)
                .FirstOrDefault();
            if (replaceable == null)
            {
                state.Pool.Insert(weapon);
                return;
            }
            character.Items.Remove(replaceable);
            state.Current.Items.Add(replaceable);
            AiTelemetry.Report(state.Player,
                $"dropped lower-value cargo {replaceable.ID} so {character.Name} can carry a weapon");
        }
        if (!character.Items.Add(weapon))
        {
            state.Pool.Insert(weapon);
            return;
        }

        character.Weapon = weapon;
        string location = character.IsStationed ? $" at {character.Location.Title}" : "";
        AiTelemetry.Report(state.Player, $"equipped {role} {character.Name}{location} with {weapon.ID}");
    }

    internal static void NormalizeWeaponLimits(
        ClassicAiState state,
        IReadOnlyCollection<Character> unit,
        bool isCamp)
    {
        int firearmLimit = isCamp && state.RootGame.World.Difficulty == 2 ? 1 : 0;
        int pitchforkLimit = state.RootGame.World.Difficulty == 0 ? 0 : 1;
        int firearms = 0;
        int pitchforks = 0;

        foreach (Character character in unit.OrderByDescending(member => member.Experience))
        {
            foreach (Item weapon in character.Items.Where(item =>
                AiItemPool.IsFirearm(item.Type) || item.ID == "item_pitchfork").ToArray())
            {
                bool allowed = AiItemPool.IsFirearm(weapon.Type)
                    ? firearms++ < firearmLimit
                    : pitchforks++ < pitchforkLimit;
                if (allowed)
                    continue;

                if (character.Weapon == weapon)
                    character.Weapon = null;
                character.Items.Remove(weapon);
                state.Pool.Insert(weapon);
                AiTelemetry.Report(state.Player,
                    $"reserved restricted weapon {weapon.ID} carried by {character.Name}");
            }
            character.Weapon = character.Items.FindBestWeapon(character.Weapon);
        }
    }

    internal static bool WeaponAllowed(
        ClassicAiState state,
        IReadOnlyCollection<Character> unit,
        Character recipient,
        bool isCamp,
        ItemType type)
    {
        if (AiItemPool.IsFirearm(type))
        {
            if (!isCamp || state.RootGame.World.Difficulty < 2)
                return false;
            return unit.Where(character => character != recipient)
                .SelectMany(character => character.Items)
                .Count(item => AiItemPool.IsFirearm(item.Type)) < 1;
        }
        if (type.ID != "item_pitchfork")
            return true;
        if (state.RootGame.World.Difficulty == 0)
            return false;
        return unit.Where(character => character != recipient)
            .SelectMany(character => character.Items)
            .Count(item => item.ID == "item_pitchfork") < 1;
    }

    internal static void CarryStrategicProtection(ClassicAiState state)
    {
        int desired = System.Math.Min(state.Player.Group.Count, TradeTask.DesiredProtectionReserve(state));
        while (state.Player.Group.SelectMany(character => character.Items)
                   .Count(item => AiItemPool.IsHazardProtection(item.Type)) < desired &&
               state.Pool.ProtectionCount > 0)
        {
            Character carrier = state.Player.Group.FirstOrDefault(character =>
                !character.Items.IsFull && !character.Items.Any(item =>
                    AiItemPool.IsHazardProtection(item.Type)));
            if (carrier == null)
                return;
            Item protection = state.Pool.GetBestGeneralProtection();
            if (protection == null)
                return;
            carrier.Items.Add(protection);
            carrier.Protection = protection;
            AiTelemetry.Report(state.Player,
                $"carried {protection.ID} on {carrier.Name} as strategic hazard protection");
        }
    }

    internal static void NormalizeCarriedProtection(ClassicAiState state, IEnumerable<Character> group)
    {
        foreach (Character character in group)
        {
            Item[] protection = character.Items
                .Where(item => AiItemPool.IsHazardProtection(item.Type))
                .OrderByDescending(item => item == character.Protection)
                .ThenByDescending(item => item.DefenseValue)
                .ToArray();
            foreach (Item excess in protection.Skip(1))
            {
                character.Items.Remove(excess);
                state.Pool.Insert(excess);
                AiTelemetry.Report(state.Player,
                    $"redistributed excess hazard protection {excess.ID} from {character.Name}");
            }
            if (protection.Length > 0)
                character.Protection = protection[0];
        }
    }

    internal static void EquipDangerProtection(ClassicAiState state, Character guard, Location camp)
    {
        if (guard.Items.FindBestProtection(null, camp.Danger.Type) != null || guard.Items.IsFull)
            return;

        Item protection = camp.Danger.Type == "radiation"
            ? state.Pool.GetProtectionSuit()
            : state.Pool.GetGasMask();
        if (protection == null)
            return;

        guard.Items.Add(protection);
        guard.Protection = protection;
        AiTelemetry.Report(state.Player,
            $"equipped {guard.Name} at {camp.Title} against {camp.Danger.Type} with {protection.ID}");
    }

}
