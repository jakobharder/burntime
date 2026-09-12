using Burntime.Classic.Logic.Generation;
using Burntime.Data.BurnGfx.Save;
using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Remaster.Logic.Interaction;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Burntime.Remaster.Logic.Generation
{
    struct NewGameInfo
    {
        public int FaceOne;
        public int FaceTwo;
        public String NameOne;
        public String NameTwo;
        public int Difficulty;
        public BurntimePlayerColor ColorOne;
        public BurntimePlayerColor ColorTwo;
        public RuleSet Rules;
        public AiProfile AI;
        public AiProfile[] AiProfiles;
        public int[] AiDifficulties;
    }

    enum BurntimePlayerColor
    {
        Green = 0,
        Red,
        Gray,
        Blue
    }

    class GameCreation
    {
        BurntimeClassic app;
        Framework.States.StateManager container;
        GameSettings settings;

        public GameCreation(BurntimeClassic App)
        {
            app = App;
        }

        void SetupServer()
        {
            container = new Framework.States.StateManager(app.ResourceManager);
            container.Root = container.Create<ClassicGame>();

            app.Server = new Burntime.Framework.Network.GameServer();
            app.GameServer = app.Server;
            app.GameServer.Create(container.Root, container);
        }

        public void CreateNewGame(NewGameInfo Info, bool startServer = true)
        {
            app.Autosaves.OnNewGameCreated();
            new LogicFactory();

            GameDefinition definition = GameDefinitions.Get(Info.Rules);

            // load game settings
            settings = new GameSettings(definition.SettingsPath);
            settings.SetDifficulty(Info.Difficulty);

            // set up game server
            SetupServer();

            // get root game object
            ClassicGame game = container.Root as ClassicGame;
            game.SetProfiles(Info.Rules, Info.AI, Info.AiProfiles);

            LogicFactory.SetParameter("resource", app.ResourceManager);

            // open gam.dat
            IGameObjectCreator creator = new ClassicBurnGfxCreator();
            creator.Create(game);
            var gamdat = LogicFactory.GetParameter<Burntime.Data.BurnGfx.Save.SaveGame>("gamdat");

            // create world object
            game.World = container.Create<ClassicWorld>();
            game.World.VictoryCondition = container.Create<VictoryCondition>();

            // set difficulty
            game.World.Difficulty = Info.Difficulty;

            // add player objects
            AddPlayer(game, Info, gamdat, settings);

            // set main map
            game.World.Map = container.Create<Map>(new object[] { definition.MapPath });
            LogicFactory.SetParameter("mainmap", game.World.Map);
            game.World.Ways = container.Create<Ways>(new object[] { definition.WaysPath });

            // set constructions
            game.Constructions = (Constructions)app.ResourceManager.GetData("constructions@construction.txt");
            game.ItemTypes = container.Create<ItemTypes>(new object[] { definition.ItemsPath });

            // set productions
            LoadProductions(game, gamdat);

            // create respawn class
            game.World.Respawn = container.Create<CharacterRespawn>(new object[] { settings.Respawn.NPC, settings.Respawn.Trader,
                settings.Respawn.Mutant, settings.Respawn.Dog });
            game.World.Respawn.Object.ApplySettings(settings);

            // create locations
            creator = new OriginalLocationCreator();
            creator.Create(game);
            creator = new LocationCreator();
            creator.Create(game);

            // place npcs
            LoadNPCs(game, gamdat);

            // set start locations for player
            game.RuleBook.SetStartLocations(game, settings, gamdat);

            // place items
            game.RuleBook.PopulateInitialItems(game, settings, gamdat);

            // Player-owned starting state belongs to the controlling rules or AI profile.
            InitializeStartingPlayers(game, gamdat);

            // set trader
            LoadTrader(game, gamdat, definition.TraderPath);

            game.InitPersistentTelemetry("new");

            container.Synchronize(false); // DEBUG

            if (startServer)
                app.Server.Run();
        }

        void AddPlayer(ClassicGame game, NewGameInfo Info, Burntime.Data.BurnGfx.Save.SaveGame gamdat, GameSettings settings)
        {
            // share container for all local player
            Burntime.Framework.States.StateManager sharedContainer = null;
            // if no synchronization mode is active, then share container with game server
            if (!BurntimeClassic.Instance.Settings["game"].GetBool("always_synchronize"))
                sharedContainer = container;

            app.ActiveClient = Burntime.Framework.Network.GameClient.NoClient;

            PixelColor[] colors = new PixelColor[4] {
                new PixelColor(0, 208, 0),
                new PixelColor(240, 64, 56),
                new PixelColor(0, 0, 0),    // not used
                new PixelColor(0, 0, 0)};   // not used

            PixelColor[] colorsdark = new PixelColor[4] {
                new PixelColor(0, 132, 0),
                new PixelColor(192, 0, 0),
                new PixelColor(80, 80, 132),
                new PixelColor(156, 156, 156)};

            int[] iconIDs = new int[] { 0, 1, 3, 2 };

            for (int i = 0; i < 4; i++)
            {
                game.World.Players.Add(container.Create<Player>(new object[] { i }));
                game.World.Players[i].Type = PlayerType.Ai;
                game.World.Players[i].IconID = iconIDs[i];
                game.World.Players[i].Character = container.Create<PlayerCharacter>();
                game.World.Players[i].Name = app.ResourceManager.GetString("burn?" + (170 + i).ToString());
                game.World.Players[i].Character.Position = new Vector2();
                int color = Helper.GetColorFromSpriteId(gamdat.Characters[i].SpriteId);
                game.World.Players[i].Character.Body = Helper.GetCharacterBody(3, color);
                game.World.Players[i].Character.Path = container.Create<PathFinding.ComplexPath>();
                game.World.Players[i].Character.Mind = container.Create<AI.PlayerControlledMind>(new object[] { game.World.Players[i].Character });
                game.World.Players[i].Character.Items.MaxCount = 9;
                game.World.Players[i].Flag = app.ResourceManager.GetData("burngfxani@syst.raw?" + gamdat.Player[i].Info.FlagId + "-" + (gamdat.Player[i].Info.FlagId + 3));
                game.World.Players[i].Flag.Object.Animation.Progressive = false;
                game.World.Players[i].Character.Class = (CharClass)gamdat.Characters[i].Type;
                game.World.Players[i].Character.Player = game.World.Players[i];
                game.World.Players[i].Color = colors[i];
                game.World.Players[i].ColorDark = colorsdark[i];
                game.World.Players[i].OnMainMap = true;
                game.World.AllCharacters += game.World.Players[i].Character;
            }

            if (Info.NameOne != "" && Info.NameOne != null)
            {
                game.World.Players[0].Name = Info.NameOne;
                game.World.Players[0].Character.FaceID = Info.FaceOne;
                game.World.Players[0].Type = PlayerType.Human;
                game.World.Players[0].Character.Items.MaxCount = 6;

                Burntime.Framework.Network.GameClient client = new Burntime.Framework.Network.GameClient(app, 0, sharedContainer);
                sharedContainer = client.StateContainer;
                app.GameServer.AddClient(client);
                app.Clients.Add(client);
            }
            else
            {
                Burntime.Framework.AI.GameClientAI client = new AI.AiPlayer(app, 0, container);
                app.Server.AddAI(client);
            }

            game.World.Players[0].Color = colors[(int)Info.ColorOne];
            game.World.Players[0].ColorDark = colorsdark[(int)Info.ColorOne];
            game.World.Players[0].IconID = Info.ColorOne == BurntimePlayerColor.Green ? 0 : 1;
            game.World.Players[0].BodyColorSet = Info.ColorOne == BurntimePlayerColor.Green ? 2 : 0;
            game.World.Players[0].Character.Body = Helper.GetCharacterBody(3, game.World.Players[0].BodyColorSet);
            game.World.Players[0].Flag = app.ResourceManager.GetData(Info.ColorOne == BurntimePlayerColor.Green ? "burngfxani@syst.raw?0-3" : "burngfxani@syst.raw?8-11");

            if (Info.NameTwo != "" && Info.NameTwo != null)
            {
                game.World.Players[1].Name = Info.NameTwo;
                game.World.Players[1].Character.FaceID = Info.FaceTwo;
                game.World.Players[1].Type = PlayerType.Human;
                game.World.Players[1].Character.Items.MaxCount = 6;

                Burntime.Framework.Network.GameClient client = new Burntime.Framework.Network.GameClient(app, 1, sharedContainer);
                app.GameServer.AddClient(client);
                app.Clients.Add(client);
            }
            else
                app.Server.AddAI(new AI.AiPlayer(app, 1, container));

            game.World.Players[1].Color = colors[(int)Info.ColorTwo];
            game.World.Players[1].ColorDark = colorsdark[(int)Info.ColorTwo];
            game.World.Players[1].IconID = Info.ColorTwo == BurntimePlayerColor.Green ? 0 : 1;
            game.World.Players[1].BodyColorSet = Info.ColorTwo == BurntimePlayerColor.Green ? 2 : 0;
            game.World.Players[1].Character.Body = Helper.GetCharacterBody(3, game.World.Players[1].BodyColorSet);
            game.World.Players[1].Flag = app.ResourceManager.GetData(Info.ColorTwo == BurntimePlayerColor.Green ? "burngfxani@syst.raw?0-3" : "burngfxani@syst.raw?8-11");

            game.World.Players[2].BodyColorSet = -1;
            game.World.Players[3].BodyColorSet = 1;

            app.Server.AddAI(new AI.AiPlayer(app, 2, container));
            app.Server.AddAI(new AI.AiPlayer(app, 3, container));

            // add ai state objects
            foreach (Player p in game.World.Players)
            {
                if (p.Type == PlayerType.Ai)
                {
                    AiProfile profile = Info.AiProfiles != null &&
                        p.Index < Info.AiProfiles.Length
                        ? Info.AiProfiles[p.Index]
                        : Info.AI;
                    bool explicitDifficulty = Info.AiDifficulties != null &&
                        p.Index < Info.AiDifficulties.Length;
                    AI.AiSettings aiSettings = explicitDifficulty
                        ? AI.AiPolicy.SettingsFor(Info.AiDifficulties[p.Index])
                        : AI.AiPolicy.SettingsForPlayer(p.Index, Info.Difficulty);
                    p.AiState = AI.AiStateFactory.Create(container, profile, p, aiSettings);
                    if (profile == AiProfile.None)
                    {
                        p.IsDead = true;
                        p.Character.Die();
                    }
                }
            }

            // Disable server clients for individually disabled AI slots.
            foreach (Burntime.Framework.Network.GameClient client in app.Server.Clients)
            {
                Player p = game.World.Players[client.Player];
                if (p.Type == PlayerType.Ai && game.GetAiProfile(p) == AiProfile.None)
                    client.Die();
            }
        }

        void InitializeStartingPlayers(ClassicGame game,
            Burntime.Data.BurnGfx.Save.SaveGame gamdat)
        {
            foreach (Player player in game.World.Players)
            {
                if (player.Type == PlayerType.Human)
                    game.RuleBook.InitializeHumanPlayer(player, player.Index, settings, gamdat);
                else
                    AI.AiStateOperations.InitializeNewGamePlayer(player.AiState, gamdat);
            }
        }

        void LoadProductions(ClassicGame game, Burntime.Data.BurnGfx.Save.SaveGame gamdat)
        {
            ConfigFile file = new ConfigFile();
            file.Open(GameDefinitions.Get(game.Rules).ProductionPath);
            ConfigSection[] sections = file.GetAllSections();

            foreach (ConfigSection section in sections)
            {
                if (section.Name == "")
                    continue;

                if (!game.ItemTypes.Contains(section.Name))
                {
                    Log.Warning("LoadProductions: item " + section.Name + " not found.");
                    continue;
                }

                string produce = section.GetString("produce");
                if (!game.ItemTypes.Contains(produce))
                {
                    Log.Warning("LoadProductions: item " + produce + " not found.");
                    continue;
                }

                Production p = container.Create(() => new Production(
                    section.GetInt("maxcombination"),
                    section.GetInts("amount"),
                    section.GetInts("amount2"),
                    game.ItemTypes[produce],
                    game.Productions.Count,
                    section.GetBool("allow_inventory")
                ));

                game.ItemTypes[section.Name].Production = p;

                foreach (string alternative in section.GetStrings("alternatives"))
                {
                    if (!game.ItemTypes.Contains(alternative))
                    {
                        Log.Warning("LoadProductions: item " + section.Name + " not found.");
                        continue;
                    }

                    game.ItemTypes[alternative].Production = p;
                }

                game.Productions.Add(p);
            }
        }

        internal static void RefreshExtendedTraderSettings(ClassicGame game)
        {
            if (game.Rules != RuleSet.Extended)
                return;
            ConfigFile file = new ConfigFile();
            if (!file.Open(GameDefinitions.Get(game.Rules).TraderPath))
                throw new InvalidOperationException("Could not load trader settings.");
            foreach (Trader trader in game.World.AllCharacters.OfType<Trader>())
            {
                string[] items = file["trader"].GetStrings(trader.TraderId.ToString());
                if (items.Length > 0)
                    trader.RefreshAssortment(items.Select(id => game.ItemTypes[id]));
            }
        }

        internal static void RefreshProductionSettings(ClassicGame game)
        {
            ConfigFile file = new ConfigFile();
            if (!file.Open(GameDefinitions.Get(game.Rules).ProductionPath))
                throw new InvalidOperationException("Could not load production settings.");
            foreach (ConfigSection section in file.GetAllSections())
            {
                if (section.Name == "" || !game.ItemTypes.Contains(section.Name))
                    continue;
                Production production = game.ItemTypes[section.Name].Production;
                if (production != null)
                    production.ApplySettings(section.GetInt("maxcombination"),
                        section.GetInts("amount"), section.GetInts("amount2"), section.GetBool("allow_inventory"));
            }
        }

        static byte FixClass(CharacterType type, byte spriteId)
        {
            int bodyId = Helper.GetSetBodyId(type);
            int color = Helper.GetColorFromSpriteId(spriteId);
            if (color < 0 || bodyId < 0)
                return spriteId;

            int body = Helper.GetBodyId(bodyId, color);
            if (body < 0)
                return spriteId;

            return (byte)body;
        }

        void LoadNPCs(ClassicGame game, Burntime.Data.BurnGfx.Save.SaveGame gamdat)
        {
            foreach (Burntime.Data.BurnGfx.Save.Character ch in gamdat.Characters)
            {
                CharacterInfo chr = ch.Info;

                chr.SpriteId = FixClass(ch.Type, chr.SpriteId);

                if (chr.LocationId > 0 && chr.LocationId <= game.World.Locations.Count)
                {
                    Character character;
                    if ((CharClass)chr.CharType == CharClass.Trader)
                    {
                        Trader trader = container.Create<Trader>();
                        trader.HomeArea = game.World.Locations[chr.LocationId - 1];
                        character = trader;
                        character.Class = CharClass.Trader;
                        character.Mind = container.Create<Burntime.Remaster.AI.SimpleMind>(new object[] { character });
                        trader.TraderId = chr.NameId;
                        character.Health = game.World.Respawn.Object.TraderHealth;
                    }
                    else if ((CharClass)chr.CharType == CharClass.Mutant)
                    {
                        if (chr.NameId == 178)
                        {
                            character = container.Create<Dog>();
                            character.Class = CharClass.Dog;
                            character.Health = game.World.Respawn.Object.DogHealth;
                        }
                        else
                        {
                            character = container.Create<Mutant>();
                            character.Class = CharClass.Mutant;
                            character.Health = game.World.Respawn.Object.MutantHealth;
                        }
                        character.Mind = container.Create<Burntime.Remaster.AI.CreatureMind>(new object[] { character });
                    }
                    else
                    {
                        character = container.Create<Character>();
                        character.Class = (CharClass)ch.Type;
                        character.Mind = container.Create<Burntime.Remaster.AI.SimpleMind>(new object[] { character });
                        character.Health = chr.Health;
                    }

                    character.FaceID = chr.FaceId;
                    character.Dialog.MenFile = chr.TextId;
                    character.Dialog.Parent = character;
                    character.Body = app.ResourceManager.GetData("burngfxani@syssze.raw?" + chr.SpriteId + "-" + (chr.SpriteId + 15), Burntime.Platform.Resource.ResourceLoadType.LinkOnly);
                    character.SetBodyId = Helper.GetSetBodyId(ch.Type);
                    character.Path = container.Create<Burntime.Remaster.PathFinding.SimplePath>();
                    character.Items.MaxCount = 6;
                    character.Experience = chr.Experience;
                    character.Food = chr.Food;
                    character.Water = chr.Water;
                    if (character.Class == CharClass.Trader)
                        character.Items.MaxCount += 6;
                    character.NameId = "burn?" + (chr.NameId - 1);
                    character.Location = game.World.Locations[chr.LocationId - 1];
                    character.Position = new Vector2(chr.MoveX, chr.MoveY);

                    // if no special item is given, set snake, meat, water bottle as hire requirements
                    if (chr.HireItemId == 0)
                    {
                        character.HireItems += game.ItemTypes["item_snake"];
                        character.HireItems += game.ItemTypes["item_meat"];
                        character.HireItems += game.ItemTypes["item_water_bottle"];
                    }
                    // if special item is given, set snake, meat, water bottle only if hire as the requested item
                    else
                    {
                        if (chr.HireItemId < 4)
                        {
                            character.HireItems += game.ItemTypes["item_snake"];
                            character.HireItems += game.ItemTypes["item_meat"];
                            character.HireItems += game.ItemTypes["item_water_bottle"];
                        }

                        character.HireItems += game.ItemTypes[chr.HireItemId];
                    }

                    game.World.AllCharacters += character;
                }
                else if (chr.LocationId != 0)
                {
                    Trader character = container.Create<Trader>();
                    character.FaceID = chr.FaceId;
                    character.Dialog.MenFile = chr.TextId;
                    character.Dialog.Parent = character;
                    character.Body = app.ResourceManager.GetData("burngfxani@syssze.raw?" + chr.SpriteId + "-" + (chr.SpriteId + 15), Burntime.Platform.Resource.ResourceLoadType.LinkOnly);
                    character.Path = container.Create<Burntime.Remaster.PathFinding.SimplePath>();
                    character.Mind = container.Create<Burntime.Remaster.AI.PlayerControlledMind>(new object[] { character });
                    character.Items.MaxCount = 6;
                    character.Health = chr.Health;
                    character.Experience = chr.Experience;
                    character.Food = chr.Food;
                    character.Water = chr.Water;
                    character.Class = (CharClass)chr.CharType;
                    if (character.Class == CharClass.Trader)
                        character.Items.MaxCount += 6;
                    character.NameId = "burn?" + (chr.NameId - 1);
                    character.Position = new Vector2(chr.MoveX, chr.MoveY);
                    character.TraderId = chr.NameId;
                    game.World.AllCharacters += character;
                }
            }
        }

        void LoadTrader(ClassicGame game, Burntime.Data.BurnGfx.Save.SaveGame gamdat,
            string traderPath)
        {
            ConfigFile traderItems = new ConfigFile();
            traderItems.Open(traderPath);

            for (int i = 0; i < gamdat.Locations.Length; i++)
            {
                if (gamdat.Locations[i].IsCity)
                {
                    Trader trader = (Trader)game.World.AllCharacters[gamdat.Locations[i].TraderId];

                    game.World.Locations[i].LocalTrader = trader;
                    game.World.Traders += trader;
                }
            }

            for (int i = 0; i < game.World.AllCharacters.Count; i++)
            {
                if (game.World.AllCharacters[i] is Trader)
                {
                    Trader trader = (Trader)game.World.AllCharacters[i];

                    string[] items = traderItems["trader"].GetStrings(trader.TraderId.ToString());
                    foreach (string item in items)
                        trader.AddRefreshItem(game.ItemTypes[item], 1);

                    game.RuleBook.InitializeTraderInventory(trader);
                }
            }
        }

        public void SaveGame(string filename)
        {
            Burntime.Framework.SaveGame game = new Burntime.Framework.SaveGame(filename, "classic", BurntimeClassic.SavegameVersion);
            try
            {
                if (!game.IsValid || game.Stream is null)
                    throw new InvalidOperationException($"Could not create save game '{filename}'.");

                game.Stream.WriteByte((byte)app.Clients.Count);
                for (int i = 0; i < app.Clients.Count; i++)
                    game.Stream.WriteByte((byte)app.Clients[i].Player);
                //app.Server.StateContainer.Save(game.Stream);
                Framework.States.StateManager saveContainer =
                    app.ActiveClient?.StateContainer ?? container;
                if (saveContainer is null)
                    throw new InvalidOperationException("No game state is available to save.");
                saveContainer.Root.UpdateSaveHint();
                saveContainer.Save(game.Stream);
            }
            finally
            {
                game.Close();
            }
        }

        public bool LoadGame(string filename, bool startServer = true)
        {
            if (container == null)
                container = new Burntime.Framework.States.StateManager(app.ResourceManager);

            app.Clients.Clear();

            Burntime.Framework.SaveGame game = new Burntime.Framework.SaveGame(filename);
            List<int> ids = new List<int>();
            try
            {
                if (!game.IsValid || game.Stream is null || game.Game != "classic" ||
                    !BurntimeClassic.IsSupportedSavegameVersion(game.Version))
                    return false;

                int player = game.Stream.ReadByte();
                for (int i = 0; i < player; i++)
                    ids.Add(game.Stream.ReadByte());

                container.Load(game.Stream);
            }
            finally
            {
                game.Close();
            }

            app.Server = new Burntime.Framework.Network.GameServer();
            app.GameServer = app.Server;
            app.GameServer.Create(container.Root, container);

            ClassicGame classic = container.Root as ClassicGame;
            classic.InitAfterLoad();

            GameDefinition definition = GameDefinitions.Get(classic.Rules);
            settings = new GameSettings(definition.SettingsPath);
            settings.SetDifficulty(classic.World.Difficulty);
            classic.World.Respawn.Object.ApplySettings(settings);

            _ = new LogicFactory();
            LogicFactory.SetParameter("mainmap", classic.World.Map);

            // share container for all local player
            Burntime.Framework.States.StateManager sharedContainer = null;
            // if no synchronization mode is active, then share container with game server
            if (!BurntimeClassic.Instance.Settings["game"].GetBool("always_synchronize"))
                sharedContainer = container;

            if (!ids.Contains(0))
                app.Server.AddAI(new AI.AiPlayer(app, 0, container));
            else
            {
                Burntime.Framework.Network.GameClient client = new Burntime.Framework.Network.GameClient(app, 0, sharedContainer);
                sharedContainer = client.StateContainer;
                app.GameServer.AddClient(client);
                app.Clients.Add(client);
            }
            if (!ids.Contains(1))
                app.Server.AddAI(new AI.AiPlayer(app, 1, container));
            else
            {
                Burntime.Framework.Network.GameClient client = new Burntime.Framework.Network.GameClient(app, 1, sharedContainer);
                app.GameServer.AddClient(client);
                app.Clients.Add(client);
            }
            app.Server.AddAI(new AI.AiPlayer(app, 2, container));
            app.Server.AddAI(new AI.AiPlayer(app, 3, container));

            // disable already dead players
            foreach (Burntime.Framework.Network.GameClient client in app.Server.Clients)
            {
                if (classic.World.Players[client.Player].IsDead)
                    client.Die();
            }

#warning TODO: store progressive setting properly
            foreach (Player p in classic.World.Players)
            {
                p.Flag.Object.Animation.Progressive = false;
            }

            if (startServer)
                app.Server.Run();

            app.Autosaves.OnGameLoaded(filename);

            return true;
        }
    }
}
