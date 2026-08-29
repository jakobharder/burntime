using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using Burntime.Remaster.AI;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster;

/// <summary>
/// A small, append-only campaign history stored as compressed NDJSON.
/// Records are deliberately independent and additive: readers must ignore
/// unknown record types and fields, while writers never reinterpret old ones.
/// </summary>
public sealed class PersistentTelemetry
{
    const int EconomyInterval = 50;

    readonly ClassicGame game;
    readonly List<string> records = new();
    bool[] knownDeadPlayers = Array.Empty<bool>();
    int?[] knownCampOwners = Array.Empty<int?>();
    bool victoryRecorded;

    internal PersistentTelemetry(ClassicGame game, byte[]? compressedData)
    {
        this.game = game;
        Load(compressedData);
        CaptureRuntimeState();
    }

    internal byte[] Data { get; private set; } = Array.Empty<byte>();

    internal void RecordSession(string reason)
    {
        var aiProfiles = game.World.Players
            .Where(player => player.AiState is ClassicAiState)
            .Select(player => new
            {
                player = player.Index,
                difficulty = ((ClassicAiState)player.AiState).Difficulty
            });

        Append(new
        {
            type = "session",
            turn = game.World.Day,
            reason,
            gameVersion = BurntimeClassic.Version,
            telemetryRevision = 1,
            aiProfiles
        });
    }

    internal void RecordCampOwnershipChange(Location location, Player? previous, Player? current)
    {
        if (location.IsCity || previous == current)
            return;

        int turn = game.World.Day;
        int? from = previous?.Index;
        int? to = current?.Index;

        EnsureCampStateCapacity();
        knownCampOwners[location.Id] = to;

        // Capturing an enemy camp currently passes briefly through neutral
        // ownership. Collapse the two same-turn changes into one transition.
        if (TryCoalesceCampTransition(turn, location.Id, from, to))
        {
            SaveData();
            return;
        }

        Append(new
        {
            type = "camp",
            turn,
            location = location.Id,
            from,
            to
        });
    }

    internal void RecordCompletedTurn()
    {
        EnsureCampStateCapacity();
        foreach (Location location in game.World.Locations.Where(location => !location.IsCity))
        {
            int? currentOwner = location.Player?.Index;
            int? previousOwner = knownCampOwners[location.Id];
            if (previousOwner != currentOwner)
                RecordCampOwnershipChange(
                    location,
                    PlayerByIndex(previousOwner),
                    PlayerByIndex(currentOwner));
        }

        EnsurePlayerStateCapacity();
        foreach (Player player in game.World.Players)
        {
            if (!knownDeadPlayers[player.Index] && player.IsDead)
            {
                Append(new
                {
                    type = "outcome",
                    turn = game.World.Day,
                    outcome = "death",
                    player = player.Index
                });
            }
            knownDeadPlayers[player.Index] = player.IsDead;
        }

        if (game.World.Day > 0 && game.World.Day % EconomyInterval == 0)
            RecordEconomySummary();
    }

    internal void RecordVictory(Player winner)
    {
        if (victoryRecorded)
            return;
        victoryRecorded = true;
        Append(new
        {
            type = "outcome",
            turn = game.World.Day,
            outcome = "victory",
            player = winner.Index
        });
    }

    void RecordEconomySummary()
    {
        var players = game.World.Players.Select(player =>
        {
            Location[] camps = game.World.Locations
                .Where(location => !location.IsCity && location.Player == player)
                .ToArray();
            return new
            {
                player = player.Index,
                ownedCamps = camps.Length,
                advancedTrapCamps = camps.Count(camp => CampItems(camp, player)
                    .Any(item => item.ID is "item_rat_trap" or "item_trap" or "item_snake_trap")),
                activeProductionCamps = camps.Count(camp => camp.Production != null &&
                    camp.GetFoodProductionRate().FoodPerDay > 0),
                storedFoodValue = camps.Sum(camp => CampItems(camp, player).Sum(item => item.FoodValue)),
                waterContainers = camps.Sum(camp => CampItems(camp, player)
                    .Count(item => AiItemPool.IsWaterContainer(item.Type))),
                pumpCamps = camps.Count(camp => CampItems(camp, player).Any(Trading.IsPump))
            };
        });

        Append(new
        {
            type = "economy",
            turn = game.World.Day,
            players
        });
    }

    static IEnumerable<Item> CampItems(Location camp, Player owner) => camp.Rooms
        .SelectMany(room => room.Items)
        .Concat(camp.CampNPC
            .Where(character => character.Player == owner && !character.IsDead)
            .SelectMany(character => character.Items));

    bool TryCoalesceCampTransition(int turn, int location, int? from, int? to)
    {
        if (records.Count == 0)
            return false;

        try
        {
            using JsonDocument document = JsonDocument.Parse(records[^1]);
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("type", out JsonElement typeElement) ||
                typeElement.GetString() != "camp" ||
                !root.TryGetProperty("turn", out JsonElement turnElement) ||
                turnElement.GetInt32() != turn ||
                !root.TryGetProperty("location", out JsonElement locationElement) ||
                locationElement.GetInt32() != location ||
                !root.TryGetProperty("to", out JsonElement previousToElement) ||
                ReadNullableInt(previousToElement) != from ||
                !root.TryGetProperty("from", out JsonElement originalFromElement))
                return false;

            int? originalFrom = ReadNullableInt(originalFromElement);
            if (originalFrom == to)
            {
                records.RemoveAt(records.Count - 1);
                return true;
            }
            records[^1] = Serialize(new
            {
                type = "camp",
                turn,
                location,
                from = originalFrom,
                to
            });
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    static int? ReadNullableInt(JsonElement element) =>
        element.ValueKind == JsonValueKind.Null ? null : element.GetInt32();

    Player? PlayerByIndex(int? index) => index.HasValue
        ? game.World.Players.FirstOrDefault(player => player.Index == index.Value)
        : null;

    void Append<T>(T record)
    {
        records.Add(Serialize(record));
        SaveData();
    }

    static string Serialize<T>(T record) => JsonSerializer.Serialize(record);

    void Load(byte[]? compressedData)
    {
        if (compressedData is not { Length: > 0 })
            return;

        try
        {
            using MemoryStream input = new(compressedData);
            using DeflateStream deflate = new(input, CompressionMode.Decompress);
            using StreamReader reader = new(deflate, Encoding.UTF8);
            while (reader.ReadLine() is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    records.Add(line);
            }
            Data = compressedData;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            records.Clear();
            Data = Array.Empty<byte>();
        }
    }

    void SaveData()
    {
        using MemoryStream output = new();
        using (DeflateStream deflate = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
        using (StreamWriter writer = new(deflate, new UTF8Encoding(false)))
        {
            foreach (string record in records)
                writer.WriteLine(record);
        }
        Data = output.ToArray();
        game.SetPersistentTelemetryData(Data);
    }

    void CaptureRuntimeState()
    {
        EnsurePlayerStateCapacity();
        foreach (Player player in game.World.Players)
            knownDeadPlayers[player.Index] = player.IsDead;

        EnsureCampStateCapacity();
        foreach (Location location in game.World.Locations.Where(location => !location.IsCity))
            knownCampOwners[location.Id] = location.Player?.Index;
    }

    void EnsurePlayerStateCapacity()
    {
        int size = game.World.Players.Count == 0
            ? 0
            : game.World.Players.Max(player => player.Index) + 1;
        if (knownDeadPlayers.Length < size)
            Array.Resize(ref knownDeadPlayers, size);
    }

    void EnsureCampStateCapacity()
    {
        int size = game.World.Locations.Count == 0
            ? 0
            : game.World.Locations.Max(location => location.Id) + 1;
        if (knownCampOwners.Length < size)
            Array.Resize(ref knownCampOwners, size);
    }
}
