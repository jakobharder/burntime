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
        string? ruleSet;
        [System.Runtime.Serialization.OptionalField]
        string[]? aiProfiles;

        public RuleSet Rules => GameDefinitions.ParseRules(ruleSet);
        public GameFeature Features => GameDefinitions.Get(Rules).Features;
        public bool HasFeature(GameFeature feature) => Features.HasFlag(feature);

        [NonSerialized]
        GameRules? ruleBook;
        internal GameRules RuleBook => ruleBook ??= new(Rules);

        public AiProfile GetAiProfile(Player player)
        {
            if (aiProfiles != null && player.Index >= 0 && player.Index < aiProfiles.Length)
                return GameDefinitions.ParseAi(aiProfiles[player.Index]);
            if (player.AiState is IAiProfileState profileState)
                return profileState.Profile;
            return player.Type == PlayerType.Ai && player.IsDead
                ? AiProfile.None
                : AiProfile.Modern;
        }

        internal bool UsesAiProfile(Player player, AiProfile profile) =>
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

        internal void SetProfiles(RuleSet rules, AiProfile ai,
            AiProfile[]? playerAiProfiles = null)
        {
            ruleSet = rules.ToString();
            aiProfiles = (playerAiProfiles ?? Enumerable.Repeat(ai, 4).ToArray())
                .Select(profile => profile.ToString())
                .ToArray();
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
            if (ruleSet is null)
            {
                RuleSet rules = ItemTypes.Path switch
                {
                    "items@items.txt" => RuleSet.Extended,
                    "items@items_original.txt" => RuleSet.Classic,
                    _ => RuleSet.Extended
                };
                AiProfile[] profiles = Enumerable.Repeat(
                    AiProfile.Modern, World.Players.Count).ToArray();

                foreach (Player player in World.Players.Where(player => player.Type == PlayerType.Ai))
                {
                    if (player.IsDead)
                        profiles[player.Index] = AiProfile.None;
                }

                SetProfiles(rules, AiProfile.Modern, profiles);
            }

            // remove old intermediate rifle states
            foreach (Item item in World.AllItems)
                if (item.ID == "item_loaded_rifle_1")
                    item.Type = ItemTypes["item_loaded_rifle"];
            ItemTypes["item_loaded_rifle"].Empty = ItemTypes["item_unloaded_rifle"];

            GameCreation.RefreshProductionSettings(this);
            // New Village is defined by map configuration rather than GAM.DAT.
            if (World.Locations.Count > 37)
            {
                var config = new Burntime.Platform.IO.ConfigFile();
                if (config.Open("maps/mat_038.txt"))
                    LocationCreator.ApplyEnvironment(World.Locations[37], config, ResourceManager);
                if (World.Locations[37].Rooms.Count > 3)
                    World.Locations[37].Rooms[3].IsWaterSource = true;
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
