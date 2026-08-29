using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Remaster.Logic;
using Burntime.Platform.Resource;
using Burntime.Remaster.Logic.Interaction;
using System.Linq;

namespace Burntime.Remaster
{
    [Serializable]
    public class ClassicGame : WorldState
    {
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

        protected override void InitInstance(object[] parameter)
        {
            productions = container.CreateLinkList<Production>();

            UpdateSaveHint();
        }

        protected override void AfterDeserialization()
        {
            base.AfterDeserialization();
            persistentTelemetry = null;
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
            foreach (Player player in World.Players)
            {
                if (player.AiState is AI.ClassicAiState ai)
                    ai.InitAfterLoad();
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

        public override bool HasValidSaveHint => saveHintDays != 0;

        public override void UpdateSaveHint()
        {
            if (World is null)
            {
                saveHintDays = 1;
                saveHintLocations = 0;
            }
            else
            {
                saveHintDays = World.Day;
                saveHintLocations = World.Players.OfType<Player>().FirstOrDefault(x => x.Type == PlayerType.Human)?.GetOwnedLocationCount(World) ?? 0;
            }
        }

        public override Dictionary<string, string> GetSaveHint()
        {
            return new Dictionary<string, string>()
            {
                { "days", saveHintDays.ToString() },
                { "camps", saveHintLocations.ToString() }
            };
        }

        public override Dictionary<string, string> GetSaveDetails()
        {
            Dictionary<string, string> details = GetSaveHint();
            details["difficulty"] = World.Difficulty.ToString();
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
