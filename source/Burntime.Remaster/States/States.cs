using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Remaster.Logic;
using Burntime.Platform.Resource;
using Burntime.Remaster.Logic.Interaction;
using Burntime.Remaster.Logic.Generation;
using Burntime.Remaster.Logic.Rules;
using System.Linq;

namespace Burntime.Remaster
{
    [Serializable]
    public class ClassicGame : WorldState
    {
        const float CREATURE_ATTACK_COOLDOWN = 1.0f;

        [NonSerialized]
        float creatureAttackCooldownRemaining;

        StateLink<ClassicWorld> world;
        public ClassicWorld World
        {
            get { return world; }
            set { world = value; }
        }

        StateLinkList<Production> productions;
        public StateLinkList<Production> Productions
        {
            get { return productions; }
            set { productions = value; }
        }

        StateLink<ItemTypes> itemTypes;
        public ItemTypes ItemTypes
        {
            get { return itemTypes; }
            set { itemTypes = value; }
        }

        public override StateObject CurrentLocation
        {
            get { return World.ActiveLocationObj; }
        }

        public override StateObject CurrentPlayer
        {
            get { return World.ActivePlayerObj; }
        }

        public override PlayerState[] Player
        {
            get 
            {
                List<PlayerState> list = new List<PlayerState>();

                for (int i = 0; i < World.Players.Count; i++)
                    list.Add(World.Players[i]);

                return list.ToArray();
            }
        }

        public override int CurrentPlayerIndex
        {
            get { return World.ActivePlayer; }
        }

        DataID<Constructions> constructions;
        public Constructions Constructions
        {
            get { return constructions; }
            set { constructions = value; }
        }

        [System.Runtime.Serialization.OptionalField]
        string? ruleSetId;
        [System.Runtime.Serialization.OptionalField]
        string? aiProfileId;
        [System.Runtime.Serialization.OptionalField]
        string[]? aiProfileIds;
        [System.Runtime.Serialization.OptionalField]
        string? worldId;

        public RuleSetId Rules => GameDefinitions.ParseRules(
            ruleSetId,
            ItemTypes?.UsesExtendedRules == true ? RuleSetId.Extended : RuleSetId.Dos);
        public AiProfileId AI => GameDefinitions.ParseAi(aiProfileId);
        public WorldId WorldDefinition => Enum.TryParse(worldId, true, out WorldId value) &&
            Enum.IsDefined(value) ? value : WorldId.Original;
        public GameFeature Features => GameDefinitions.Get(Rules, WorldDefinition).Features;
        public bool HasFeature(GameFeature feature) => Features.HasFlag(feature);

        [NonSerialized]
        IGameRules? ruleBook;
        internal IGameRules RuleBook => ruleBook ??= GameRulesRegistry.Get(Rules);

        public AiProfileId GetAiProfile(Player player)
        {
            if (aiProfileIds != null && player.Index >= 0 && player.Index < aiProfileIds.Length)
                return GameDefinitions.ParseAi(aiProfileIds[player.Index], AI);
            if (player.AiState is IAiProfileState profileState)
                return profileState.Profile;
            return player.Type == PlayerType.Ai && player.IsDead ? AiProfileId.None : AI;
        }

        internal bool UsesAiProfile(Player player, AiProfileId profile) =>
            player.Type == PlayerType.Ai && GetAiProfile(player) == profile;

        internal void UpdateCreatureAttackCooldown(float elapsed)
        {
            creatureAttackCooldownRemaining = Math.Max(0,
                creatureAttackCooldownRemaining - elapsed);
        }

        internal bool TryBeginCreatureAttack()
        {
            if (creatureAttackCooldownRemaining > 0)
                return false;

            creatureAttackCooldownRemaining = CREATURE_ATTACK_COOLDOWN;
            return true;
        }

        internal void SetProfiles(RuleSetId rules, AiProfileId ai, WorldId world,
            AiProfileId[]? playerAiProfiles = null)
        {
            ruleSetId = rules.ToString();
            aiProfileId = ai.ToString();
            aiProfileIds = (playerAiProfiles ?? Enumerable.Repeat(ai, 4).ToArray())
                .Select(profile => profile.ToString())
                .ToArray();
            worldId = world.ToString();
        }

        protected override void InitInstance(object[] parameter)
        {
            productions = container.CreateLinkList<Production>();

            UpdateSaveHint();
        }

        protected override void AfterDeserialization()
        {
            base.AfterDeserialization();
            persistentTelemetry = null;
            ruleBook = null;
        }

        [System.Runtime.Serialization.OptionalField]
        byte[]? persistentTelemetryData;

        [NonSerialized]
        PersistentTelemetry? persistentTelemetry;

        public byte[]? PersistentTelemetryData => persistentTelemetryData;

        internal void InitPersistentTelemetry(string reason)
        {
            persistentTelemetry = new PersistentTelemetry(this, persistentTelemetryData);
            persistentTelemetry.RecordSession(reason);
        }

        internal void SetPersistentTelemetryData(byte[] data)
        {
            persistentTelemetryData = data;
        }

        internal void NotifyCampOwnershipChanged(Location location, Player? previous, Player? current)
        {
            persistentTelemetry?.RecordCampOwnershipChange(location, previous, current);
        }

        /// <summary>
        /// Applies runtime initialization and compatibility cleanup after a
        /// save has been fully deserialized and its state links resolved.
        /// </summary>
        public void InitAfterLoad()
        {
            foreach (Item item in World.AllCharacters.SelectMany(c => c.Items)
                .Concat(World.Locations.SelectMany(l => l.Items))
                .Concat(World.Locations.SelectMany(l => l.Rooms).SelectMany(r => r.Items)).Distinct())
                item.MigrateAmmunition(id => ItemTypes[id]);
            ItemTypes.RefreshItemLinks();
            GameCreation.RefreshProductionSettings(this);
            // New Village is defined by map configuration rather than GAM.DAT.
            if (World.Locations.Count > 37)
            {
                var config = new Burntime.Platform.IO.ConfigFile();
                if (config.Open("maps/mat_038.txt"))
                    LocationCreator.ApplyEnvironment(World.Locations[37], config, ResourceManager);
            }
            GameCreation.RefreshExtendedTraderSettings(this);
            foreach (Player player in World.Players)
            {
                Burntime.Remaster.AI.AiStateOperations.InitAfterLoad(player.AiState);
            }
            InitPersistentTelemetry("load");
        }

        public override void Turn()
        {
            World.Turn();

            persistentTelemetry?.RecordCompletedTurn();

            base.Turn();
            UpdateSaveHint();
        }

        [NonSerialized]
        public bool MainMapView;

        public override PlayerState CheckWinner()
        {
            foreach (PlayerState player in Player)
            {
                if (World.VictoryCondition.Object.Process((Player)player))
                {
                    persistentTelemetry?.RecordVictory((Player)player);
                    return player;
                }
            }

            return null;
        }

        int saveHintDays;
        int saveHintLocations;

        // OptionalField keeps saves written before 1.1 loadable. Their default
        // values are handled by GetSaveHint and the options menu's legacy path.
        [System.Runtime.Serialization.OptionalField]
        int saveHintDifficulty;
        [System.Runtime.Serialization.OptionalField]
        string? saveHintPlayer;
        [System.Runtime.Serialization.OptionalField]
        long saveHintDateTimeUtcTicks;

        public override bool HasValidSaveHint => saveHintDays != 0;

        public override void UpdateSaveHint()
        {
            if (World is null)
            {
                saveHintDays = 1;
                saveHintLocations = 0;
                saveHintDifficulty = 0;
                saveHintPlayer = null;
            }
            else
            {
                saveHintDays = World.Day;
                Player? player = World.Players.OfType<Player>()
                    .FirstOrDefault(x => x.Type == PlayerType.Human);
                saveHintLocations = player?.GetOwnedLocationCount(World) ?? 0;
                saveHintDifficulty = World.Difficulty;
                saveHintPlayer = player?.Name;
            }
            saveHintDateTimeUtcTicks = DateTime.UtcNow.Ticks;
        }

        public override Dictionary<string, string> GetSaveHint()
        {
            var hint = new Dictionary<string, string>()
            {
                { "days", saveHintDays.ToString() },
                { "camps", saveHintLocations.ToString() },
                { "difficulty", saveHintDifficulty.ToString() }
            };
            if (!string.IsNullOrWhiteSpace(saveHintPlayer))
                hint["player"] = saveHintPlayer;
            if (saveHintDateTimeUtcTicks > 0)
                hint["datetime"] = new DateTime(saveHintDateTimeUtcTicks, DateTimeKind.Utc)
                    .ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            return hint;
        }

        public override Dictionary<string, string> GetSaveDetails()
        {
            Dictionary<string, string> details = GetSaveHint();
            details["difficulty"] = World?.Difficulty.ToString() ?? saveHintDifficulty.ToString();
            return details;
        }

        public bool CheatsEnabled { get; set; }
    }

    [Serializable]
    public class ClassicWorld : World
    {
        public Player ActivePlayerObj
        {
            get { if (ActivePlayer == -1) return null; else return Players[ActivePlayer]; }
        }

        public Location ActiveLocationObj
        {
            get { if (ActivePlayer == -1) return null; else return Players[ActivePlayer].Location; }
        }

        [NonSerialized]
        public Character SelectedCharacter;

        [NonSerialized]
        Trader activeTraderObj;
        public Trader ActiveTraderObj
        {
            get { return activeTraderObj; }
            set { activeTraderObj = value; }
        }
    }
}
