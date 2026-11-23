using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;
using Module = Burntime.Framework.Module;

namespace Burntime.Remaster.Tests;

using static Program;

static class NpcNameTests
{
    internal static IEnumerable<Case<int>> Cases()
    {
        foreach (string language in new[] { "en", "de" })
        {
            yield return Int($"{language}: original duplicates, trader priority and stable identities", 0,
                () => WithResources(language, resources =>
                {
                    var (game, manager) = CreateGame(resources);
                    // GAM.DAT reuses 179..220 after reaching 331. Put the
                    // duplicates first to check that traders still take priority.
                    var duplicates = Enumerable.Range(179, 42)
                        .Select(id => AddNpc(game, manager, id, CharClass.Mercenary)).ToArray();
                    var originals = Enumerable.Range(179, 153)
                        .Select(id => AddNpc(game, manager, id,
                            id <= 200 ? CharClass.Trader : CharClass.Doctor)).ToArray();
                    duplicates[0].Health = 0; // Off-map/dead identities are reserved too.
                    NpcNames.EnsureUnique(game, resources);
                    Equal(45, game.World.AllCharacters.Count(c => c.NameId.StartsWith("npc_names?")), "42 reused IDs plus three translation aliases");
                    Equal(true, originals.Take(22).All(c => c.NameId.StartsWith("burn?")), "all traders retain names");
                    AssertUnique(game);
                    string[] identities = game.World.AllCharacters.Select(c => c.NameId).ToArray();
                    NpcNames.EnsureUnique(game, resources);
                    Equal(true, identities.SequenceEqual(game.World.AllCharacters.Select(c => c.NameId)), "idempotent allocation");
                    return 0;
                }));

            foreach (int difficulty in new[] { 0, 1, 2 })
            yield return Int($"{language}, difficulty {difficulty}: Extended revival and save roundtrip preserve unique names", 0,
                () => WithResources(language, resources =>
                {
                    var (game, manager) = CreateGame(resources);
                    var location = manager.Create<Location>();
                    location.Id = 9;
                    game.World.Locations.Add(location);
                    var city = manager.Create<Location>();
                    city.Id = 10;
                    city.IsCity = true;
                    game.World.Locations.Add(city);
                    game.World.Respawn = manager.Create<CharacterRespawn>(new object[] { 1, 1, 1, 1 });
                    var settings = new GameSettings("classic:rules/extended/game.txt");
                    settings.SetDifficulty(difficulty);
                    game.World.Respawn.Object.ApplySettings(settings);
                    Equal(RespawnMethod.LocationCycle, game.World.Respawn.Object.SpawnMethod, "actual Extended scheduling");
                    int dueDay = settings.Respawn.NPC * 10 - location.Id;
                    var trader = AddNpc(game, manager, 181, CharClass.Trader);
                    var fighter = AddNpc(game, manager, 181, CharClass.Mercenary);
                    var doctor = AddNpc(game, manager, 181, CharClass.Doctor);
                    foreach (var npc in new[] { fighter, doctor })
                    {
                        npc.Location = location;
                        npc.Path = manager.Create<Burntime.Remaster.PathFinding.SimplePath>();
                    }
                    NpcNames.EnsureUnique(game, resources);
                    string fighterName = fighter.NameId, doctorName = doctor.NameId;
                    fighter.Die();
                    doctor.Die();
                    // Exercise the existing revival path, including the dead donor
                    // exchange and removal from the pending respawn collection.
                    var instance = typeof(Module).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static)!;
                    object? previous = instance.GetValue(null);
                    var app = (BurntimeClassic)RuntimeHelpers.GetUninitializedObject(typeof(BurntimeClassic));
                    typeof(Module).GetField("<ResourceManager>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
                        .SetValue(app, resources);
                    instance.SetValue(null, app);
                    try
                    {
                        game.World.Day = dueDay - 1;
                        game.World.Respawn.Object.Turn();
                        Equal(true, fighter.IsDead && doctor.IsDead, "identities remain pending before location cycle");
                        game.World.Day = dueDay;
                        game.World.Respawn.Object.Turn();
                        Equal(false, fighter.IsDead, "fighter revived");
                        Equal(city, fighter.Location, "Extended city replenishment uses the renamed character");
                        Equal(true, doctor.IsDead, "last donor stays pending under existing revival rules");
                        Equal(doctorName, fighter.NameId, "fighter receives dead donor's new name");
                        Equal(fighterName, doctor.NameId, "donor retains reserved identity");
                        Equal("burn?180", trader.NameId, "merchant identity unchanged");
                        AssertUnique(game);
                        using var stream = new MemoryStream();
                        manager.Synchronize(false);
                        manager.Save(stream);
                        var loaded = new StateManager(resources);
                        loaded.Load(stream);
                        var savedGame = (ClassicGame)loaded.Root;
                        // The load path restores nonserialized scheduling settings.
                        savedGame.World.Respawn.Object.ApplySettings(settings);
                        Equal(RespawnMethod.LocationCycle, savedGame.World.Respawn.Object.SpawnMethod, "Extended scheduling restored after load");
                        AssertUnique(savedGame);
                        var revived = savedGame.World.AllCharacters.First(c => c.Class == CharClass.Mercenary);
                        Equal(doctorName, revived.NameId, "localized resource ID survives saving");
                        revived.Die();
                        savedGame.World.Day = dueDay + settings.Respawn.NPC;
                        savedGame.World.Respawn.Object.Turn();
                        AssertUnique(savedGame);
                        Equal(false, savedGame.World.AllCharacters.First(c => c.Class == CharClass.Doctor).IsDead,
                            "pending doctor revives after load with another donor");
                    }
                    finally { instance.SetValue(null, previous); }
                    return 0;
                }));
        }

        foreach (RuleSet rules in new[] { RuleSet.Classic, RuleSet.Dos, RuleSet.Amiga })
            yield return Int($"{rules}: original naming unchanged", 0, () => WithResources("en", resources =>
            {
                var (game, manager) = CreateGame(resources);
                game.SetRules(rules);
                var fighter = AddNpc(game, manager, 181, CharClass.Mercenary);
                AddNpc(game, manager, 181, CharClass.Trader);
                NpcNames.EnsureUnique(game, resources);
                Equal("burn?180", fighter.NameId, "classic fighter name");
                return 0;
            }));

        yield return Int("localized pools retain saved identities and spare capacity", 0, () =>
        {
            int? count = null;
            foreach (string language in new[] { "en", "de" })
                WithResources(language, resources =>
                {
                    string[] names = ((NameResources)(object)resources).Names["npc_names"];
                    Equal(true, names.Length >= 64, "pool has at least 19 spare names");
                    if (count != null) Equal(count.Value, names.Length, "matching language pool lengths");
                    count = names.Length;
                    string expected = language == "en"
                        ? "dcfd9e49a56d92ffdaf70988ce90aa7df48d9e8cc89ad70290ab58e2f93274f3"
                        : "829c4b87f6f4f449a7f1830e61f9ddb13b331e8dc9875c986dd759217690a39a";
                    string prefix = string.Join('\n', names.Take(64));
                    Equal(expected, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(prefix))).ToLowerInvariant(),
                        "established 64 saved identities retain their indices");
                    Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count(), "unique localized names");
                    Equal(false, names.Any(name => string.IsNullOrWhiteSpace(name)), "no blank names");
                    Equal(false, names.Intersect(((NameResources)(object)resources).Names["burn"], StringComparer.OrdinalIgnoreCase).Any(),
                        "no collisions with original text");
                    string[] additions = System.IO.File.ReadAllLines(Path.Combine(ResourceRoot(),
                        language == "de" ? "classic_de/lang/de/newburn.txt" : "classic/lang/en/newburn.txt"));
                    Equal(false, names.Intersect(additions, StringComparer.OrdinalIgnoreCase).Any(),
                        "no collisions with map-added resident names");
                    return 0;
                });
            return 0;
        });

        yield return Int("switching language retains unique identities", 0, () =>
        {
            string[]? identities = null;
            foreach (string language in new[] { "en", "de" })
                WithResources(language, resources =>
                {
                    var (game, manager) = CreateGame(resources);
                    foreach (int id in Enumerable.Range(179, 153))
                        AddNpc(game, manager, id, id <= 200 ? CharClass.Trader : CharClass.Doctor);
                    foreach (int id in Enumerable.Range(179, 42))
                        AddNpc(game, manager, id, CharClass.Mercenary);
                    NpcNames.EnsureUnique(game, resources);
                    string[] actual = game.World.AllCharacters.Select(c => c.NameId).ToArray();
                    if (identities != null)
                        Equal(true, identities.SequenceEqual(actual), "same assignments regardless of language");
                    identities = actual;
                    AssertUnique(game);
                    return 0;
                });
            return 0;
        });

        foreach (int capacity in new[] { 0, 1, 64 })
            yield return Int($"pool capacity {capacity}: exhaustion preserves valid identities", 0, () => WithResources("en", resources =>
            {
                var proxy = (NameResources)(object)resources;
                proxy.Names["npc_names"] = proxy.Names["npc_names"].Take(capacity).ToArray();
                var (game, manager) = CreateGame(resources);
                var trader = AddNpc(game, manager, 181, CharClass.Trader);
                var fighters = Enumerable.Range(0, 70).Select(_ => AddNpc(game, manager, 181, CharClass.Mercenary)).ToArray();
                NpcNames.EnsureUnique(game, resources);
                Equal("burn?180", trader.NameId, "trader unaffected by exhaustion");
                Equal(capacity, fighters.Count(c => c.NameId.StartsWith("npc_names?")), "available names allocated once");
                Equal(true, fighters.Skip(capacity).All(c => c.NameId == "burn?180"), "remaining identities preserved");
                Equal(false, game.World.AllCharacters.Any(c => string.IsNullOrWhiteSpace(c.Name)), "no blank names");
                return 0;
            }));

        yield return Int("existing new-pool identities remain reserved", 0, () => WithResources("en", resources =>
        {
            var (game, manager) = CreateGame(resources);
            var existing = AddNpc(game, manager, 182, CharClass.Doctor);
            existing.NameId = "npc_names?0";
            AddNpc(game, manager, 181, CharClass.Trader);
            var duplicate = AddNpc(game, manager, 181, CharClass.Mercenary);
            NpcNames.EnsureUnique(game, resources);
            Equal("npc_names?0", existing.NameId, "reserved identity retained");
            Equal("npc_names?1", duplicate.NameId, "allocator skips reserved name");
            AssertUnique(game);
            return 0;
        }));

        yield return Int("real localized resources and map-added residents remain unique", 0, () =>
        {
            string root = ResourceRoot();
            string previousLanguage = FileSystem.LocalizationCode;
            bool previousLocalization = FileSystem.UseLocalization;
            FileSystem.AddPackage("npc-real-en", Path.Combine(root, "classic"));
            FileSystem.AddPackage("npc-real-de", Path.Combine(root, "classic_de"));
            try
            {
                FileSystem.UseLocalization = true;
                var resources = new TextResources();
                string[]? identities = null;
                foreach (string language in new[] { "en", "de" })
                {
                    FileSystem.LocalizationCode = language;
                    resources.ClearText();
                    Equal(true, FileSystem.ExistsFile("npc_names.txt"), "localized name file is visible");
                    Equal(language == "en" ? "Oscar" : "Oskar", resources.GetString("npc_names?0"),
                        "real resource lookup selects language");
                    var (game, manager) = CreateGame(resources);
                    foreach (int id in Enumerable.Range(179, 153))
                        AddNpc(game, manager, id, id <= 200 ? CharClass.Trader : CharClass.Doctor);
                    foreach (int id in Enumerable.Range(179, 42))
                        AddNpc(game, manager, id, CharClass.Mercenary);
                    // MAT_038 adds these two named humans alongside three dogs.
                    AddNpc(game, manager, 179, CharClass.Technician).NameId = "newburn?93";
                    AddNpc(game, manager, 181, CharClass.Mercenary).NameId = "newburn?94";
                    NpcNames.EnsureUnique(game, resources);
                    AssertUnique(game);
                    Equal(45, game.World.AllCharacters.Count(c => c.NameId.StartsWith("npc_names?")), "map residents do not consume spare names");
                    string[] actual = game.World.AllCharacters.Select(c => c.NameId).ToArray();
                    if (identities != null) Equal(true, identities.SequenceEqual(actual), "same localized assignments");
                    identities = actual;
                }
            }
            finally
            {
                FileSystem.RemovePackage("npc-real-de");
                FileSystem.RemovePackage("npc-real-en");
                FileSystem.LocalizationCode = previousLanguage;
                FileSystem.UseLocalization = previousLocalization;
            }
            return 0;
        });
    }

    static void AssertUnique(ClassicGame game)
    {
        var characters = game.World.AllCharacters.Where(NpcNames.IsNamedNpc).ToArray();
        Equal(characters.Length, characters.Select(c => c.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            "unique displayed names across professions, including dead NPCs");
    }

    static Character AddNpc(ClassicGame game, StateManager manager, int id, CharClass kind)
    {
        var npc = manager.Create<Character>();
        npc.Class = kind;
        npc.NameId = $"burn?{id - 1}";
        game.World.AllCharacters.Add(npc);
        return npc;
    }

    static (ClassicGame Game, StateManager Manager) CreateGame(IResourceManager resources)
    {
        var manager = new StateManager(resources);
        var game = manager.Create<ClassicGame>();
        manager.Root = game;
        game.SetRules(RuleSet.Extended);
        game.World = manager.Create(() =>
        {
            var world = (ClassicWorld)RuntimeHelpers.GetUninitializedObject(typeof(ClassicWorld));
            foreach (string field in new[] { "ID", "localID" })
                typeof(StateObject).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(world, -1);
            return world;
        });
        game.World.AllCharacters = manager.CreateLinkList<Character>();
        game.World.Locations = manager.CreateLinkList<Location>();
        game.World.Players = manager.CreateLinkList<Player>();
        game.World.Traders = manager.CreateLinkList<Trader>();
        game.World.Respawn = manager.Create<CharacterRespawn>(new object[] { 0, 0, 0, 0 });
        return (game, manager);
    }

    static int WithResources(string language, Func<IResourceManager, int> check)
    {
        string root = ResourceRoot();
        string directory = Path.Combine(root, language == "de" ? "classic_de/lang/de" : "classic/lang/en");
        FileSystem.AddPackage("npc-name-tests", directory);
        try
        {
            var resources = DispatchProxy.Create<IResourceManager, NameResources>();
            var proxy = (NameResources)(object)resources;
            proxy.Resources = resources;
            foreach (string file in new[] { "burn", "npc_names" })
                proxy.Names[file] = System.IO.File.ReadAllLines(Path.Combine(directory, file == "burn" ? "BURN.TXT" : "npc_names.txt"))
                    .Select(line => line.TrimEnd('}')).ToArray();
            return check(resources);
        }
        finally { FileSystem.RemovePackage("npc-name-tests"); }
    }

    static string ResourceRoot() => Path.GetFullPath(Path.Combine(
        Path.GetDirectoryName(ResourceFile("lang/en/BURN.TXT"))!, "../../.."));

    sealed class TextResources : ResourceManagerBase
    {
        public TextResources() : base(new TestLoadingCounter()) { }
        public override Font? GetFont(string file, PixelColor color) => null;
        public override Font? GetFont(string file, PixelColor color, PixelColor backColor) => null;
        public override ISprite GetImage(ResourceID id, ResourceLoadType loadType = ResourceLoadType.Delayed) => null!;
        public override void Reload(ISprite sprite, ResourceLoadType loadType = ResourceLoadType.Delayed) { }
    }

    sealed class TestLoadingCounter : ILoadingCounter
    {
        public void IncreaseLoadingCount() { }
        public void DecreaseLoadingCount() { }
    }

    public class NameResources : DispatchProxy
    {
        public readonly Dictionary<string, string[]> Names = new();
        public IResourceManager Resources = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "GetString")
            {
                string id = (string)args![0]!;
                int index;
                if (args.Length == 1)
                {
                    int separator = id.LastIndexOf('?');
                    index = int.Parse(id[(separator + 1)..]);
                    id = id[..separator];
                }
                else index = (int)args[1]!;
                return index < Names[id].Length ? Names[id][index] : "";
            }
            if (method.Name == "GetData")
                return new TestSprite { DataName = args![0]!.ToString(), ResourceManager = Resources };
            throw new NotSupportedException(method.Name);
        }
    }

    sealed class TestSprite : ISprite
    {
        public override ResourceID ID => DataName;
        public override Vector2f Resolution { get; set; } = Vector2f.One;
        public override Vector2 Size => Vector2.One;
        public override SpriteAnimation Animation { get; set; } = new(2);
        public override bool IsLoaded => true;
        public override bool HasSystemCopy => true;
        public override bool Touch() => true;
        public override void Update(float elapsed) { }
        public override int Unload() => 0;
        public override ISprite Clone() => this;
    }
}
