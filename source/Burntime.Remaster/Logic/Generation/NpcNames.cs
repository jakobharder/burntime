using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;

namespace Burntime.Remaster.Logic.Generation;

internal static class NpcNames
{
    // npc_names.txt line numbers are saved resource IDs. Append new entries
    // in matching EN/DE order; never insert, delete or reorder existing names.
    internal static bool IsNamedNpc(Character character) =>
        character.Class is CharClass.Mercenary or CharClass.Technician or CharClass.Doctor or CharClass.Trader;

    // Different original IDs also share translations: Mark in English, Dave
    // in both languages, and Pete in German. Reserve the same identities in
    // either language so changing language cannot introduce a duplicate.
    static string IdentityKey(string id) => id switch
    {
        "burn?287" => "burn?196", // Mark, original name IDs 288 and 197 (trader).
        "burn?304" => "burn?277", // Dave, original name IDs 305 and 278.
        "burn?314" => "burn?278", // Pete, original name IDs 315 and 279.
        _ => id
    };

    internal static void EnsureUnique(ClassicGame game, IResourceManager resources)
    {
        if (game.Rules != RuleSet.Extended || !FileSystem.ExistsFile("npc_names.txt"))
            return;

        // Include recruits and dead NPCs: revival exchanges identities between
        // dead characters, so each identity must remain reserved off the map too.
        Character[] characters = game.World.AllCharacters.Where(IsNamedNpc)
            .OrderBy(character => character.Class == CharClass.Trader ? 0 : 1).ToArray();
        var reservedIds = characters.Select(character => character.NameId).ToHashSet();
        var reservedNames = characters.Select(character => resources.GetString(character.NameId).Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenIds = new HashSet<string>();
        int next = 0;

        foreach (Character character in characters)
        {
            bool newIdentity = seenIds.Add(IdentityKey(character.NameId));
            bool newName = seen.Add(resources.GetString(character.NameId).Trim());
            if (newIdentity && newName)
                continue;

            while (true)
            {
                string id = $"npc_names?{next++}";
                string name = resources.GetString(id).Trim();
                if (name.Length == 0)
                {
                    Log.Warning("Not enough unique NPC names; preserving remaining identities.");
                    return;
                }
                if (reservedIds.Contains(id) || reservedNames.Contains(name))
                    continue;

                character.NameId = id;
                reservedIds.Add(id);
                reservedNames.Add(name);
                seen.Add(name);
                seenIds.Add(id);
                break;
            }
        }
    }
}
