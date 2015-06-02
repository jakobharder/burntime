
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Framework;
using Burntime.Data.BurnGfx;
using Burntime.Data.BurnGfx.Save;

using Burntime.Classic.Logic.Interaction;

namespace Burntime.Classic.Logic.Generation
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

        public void CreateNewGame(NewGameInfo Info)
        {
            // load game settings
            settings = new GameSettings("gamesettings.txt");
            settings.SetDifficulty(Info.Difficulty);

            // set up game server
            SetupServer();

            // get root game object
            ClassicGame game = container.Root as ClassicGame;

            // open gam.dat
            Burntime.Data.BurnGfx.Save.SaveGame gamdat = new Burntime.Data.BurnGfx.Save.SaveGame();
            gamdat.Open();

            // create world object
            game.World = container.Create<ClassicWorld>();
            game.World.VictoryCondition = container.Create<VictoryCondition>();

            // set difficulty
            game.World.Difficulty = Info.Difficulty;

            // add player objects
            AddPlayer(game, Info, gamdat, settings);

            // set main map
            game.World.Map = container.Create<Map>(new object[] { "maps/mat_000.burnmap" });
            game.World.Ways = container.Create<Ways>(new object[] { "burngfxways@gam.dat" });

            // set constructions
            game.Constructions = (Constructions)app.ResourceManager.GetData("constructions@construction.txt");
            game.ItemTypes = container.Create<ItemTypes>(new object[] { "items@items.txt" });

            // set productions
            LoadProductions(game, gamdat);

            // create respawn class
            game.World.Respawn = container.Create<CharacterRespawn>(new object[] { settings.Respawn.NPC, settings.Respawn.Trader,
                settings.Respawn.Mutant, settings.Respawn.Dog });

            // create cities
            LoadCities(game, gamdat);
            LoadLocations(game);

            // place npcs
            LoadNPCs(game, gamdat);

            // set start locations for player
            SetStartLocations(game);

            // place items
            LoadItems(game, gamdat);

            // set trader
            LoadTrader(game, gamdat);

            // set start inventory for player
            SetStartInventory(game);

            container.Synchronize(false); // DEBUG

            app.Server.Run();
        }

        void AddPlayer(ClassicGame game, NewGameInfo Info, Burntime.Data.BurnGfx.Save.SaveGame gamdat, GameSettings settings)
        {
            // share container for all local player
            Burntime.Framework.States.StateManager sharedContainer = null;
            // if no synchronization mode is active, then share container with game server
            if (!BurntimeClassic.Instance.Settings["game"].GetBool("always_synchronize"))
                sharedContainer = container;

            // no ai mode
            bool disableAI = BurntimeClassic.Instance.Settings["game"].GetBool("disable_ai");

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
                game.World.Players[i].Character.Body = app.ResourceManager.GetData("syssze.raw?" + gamdat.Characters[i].SpriteId + "-" + (gamdat.Characters[i].SpriteId + 15));
                game.World.Players[i].Character.Path = container.Create<PathFinding.ComplexPath>();
                game.World.Players[i].Character.Mind = container.Create<AI.PlayerControlledMind>(new object[] { game.World.Players[i].Character });
                game.World.Players[i].Character.Items.MaxCount = 6;
                game.World.Players[i].Flag = app.ResourceManager.GetData("burngfxani@syst.raw?" + gamdat.Player[i].Info.FlagId + "-" + (gamdat.Player[i].Info.FlagId + 3));
                game.World.Players[i].Flag.Object.Animation.Progressive = false;
                game.World.Players[i].Character.Health = settings.StartHealth;
                game.World.Players[i].Character.Experience = settings.StartExperience;
                game.World.Players[i].Character.Food = settings.StartFood;
                game.World.Players[i].Character.Water = settings.StartWater;
                game.World.Players[i].Character.Class = (CharClass)gamdat.Characters[i].Type;
                game.World.Players[i].Character.Player = game.World.Players[i];
                game.World.Players[i].Color = colors[i];
                game.World.Players[i].ColorDark = colorsdark[i];
                game.World.Players[i].OnMainMap = true;
                game.World.Players[i].BaseExperience = settings.StartExperience;

                game.World.AllCharacters += game.World.Players[i].Character;
            }

            if (Info.NameOne != "" && Info.NameOne != null)
            {
                game.World.Players[0].Name = Info.NameOne;
                game.World.Players[0].Character.FaceID = Info.FaceOne;
                game.World.Players[0].Type = PlayerType.Human;

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
            game.World.Players[0].Character.Body = Helper.GetCharacterBody(2, game.World.Players[0].BodyColorSet);
            game.World.Players[0].Flag = app.ResourceManager.GetData(Info.ColorOne == BurntimePlayerColor.Green ? "burngfxani@syst.raw?0-3" : "burngfxani@syst.raw?8-11");

            if (Info.NameTwo != "" && Info.NameTwo != null)
            {
                game.World.Players[1].Name = Info.NameTwo;
                game.World.Players[1].Character.FaceID = Info.FaceTwo;
                game.World.Players[1].Type = PlayerType.Human;

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
            game.World.Players[1].Character.Body = Helper.GetCharacterBody(2, game.World.Players[1].BodyColorSet);
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
                    p.AiState = container.Create<AI.ClassicAiState>(p, settings.AiSettings);
                }
            }

            // kill all ai player
            if (disableAI)
            {
                foreach (Player p in game.World.Players)
                {
                    if (p.Type == PlayerType.Ai)
                        p.IsDead = true;
                }

                foreach (Burntime.Framework.Network.GameClient client in app.Server.Clients)
                {
                    if (game.World.Players[client.Player].Type == PlayerType.Ai)
                        client.Die();
                }
            }
        }

        void SetStartLocations(ClassicGame game)
        {
            // copy start locations since we mark used ones with -1
            int[] loc = (int[])settings.StartLocations.Clone();

            foreach (Player player in game.World.Players)
            {
                // find free location
                int start;
                do
                {
                    int index = Burntime.Platform.Math.Random.Next(0, loc.Length);
                    start = loc[index];
                    loc[index] = -1;
                } while (start == -1);

                // set location
                player.Location = game.World.Locations[start];
                player.Character.Position = new Vector2(game.World.Locations[start].EntryPoint);
                player.Character.Path.MoveTo = new Vector2(game.World.Locations[start].EntryPoint);
            }
        }

        void SetStartInventory(ClassicGame game)
        {
            foreach (Player player in game.World.Players)
            {
                // clear
                player.Character.Items.Clear();

                // add items
                string[] items = settings.StartItems;
                foreach (string item in items)
                {
                    player.Character.Items.Add(game.ItemTypes[item].Generate());
                }
            }
        }

        void LoadProductions(ClassicGame game, Burntime.Data.BurnGfx.Save.SaveGame gamdat)
        {
            ConfigFile file = new ConfigFile();
            file.Open("production.txt");
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

                Production p = container.Create<Production>();
                p.MaxCombination = section.GetInt("maxcombination");
                p.ProductionPerDay = section.GetInts("amount");
                p.ProductionPerDay2Person = section.GetInts("amount2");
                if (p.ProductionPerDay2Person.Length == 0)
                    p.ProductionPerDay2Person = p.ProductionPerDay;

                p.Produce = game.ItemTypes[produce];
                p.ID = game.Productions.Count;

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

        void LoadCities(ClassicGame game, Burntime.Data.BurnGfx.Save.SaveGame gamdat)
        {
            int i = 1;
            foreach (Burntime.Data.BurnGfx.Save.Location city in gamdat.Locations)
            {
                Location loc = container.Create<Location>();
                loc.Id = i - 1;
                loc.Source.Water = city.WaterSource;
                loc.Source.Reserve = city.Water;
                loc.Source.Capacity = city.WaterCapacity;
                loc.Production = city.Producing == -1 ? null : game.Productions[city.Producing];
                loc.AvailableProducts = (int[])city.Production.Clone();
                if (city.Danger != 0)
                    loc.Danger = Danger.Instance((city.Danger == 3) ? "radiation" : "gas", city.DangerAmount);
                loc.IsCity = city.IsCity;
                loc.EntryPoint = city.EntryPoint;
                loc.Ways = city.Ways;
                loc.WayLengths = city.WayLengths;

                loc.Map = container.Create<Map>(new object[] { "maps/mat_" + i.ToString("D3") + ".burnmap??" + i });

                loc.Rooms = container.CreateLinkList<Room>();

                for (int j = 0; j < loc.Map.Entrances.Length; j++)
                {
                    RoomType type = loc.Map.Entrances[j].RoomType;

                    Room room = container.Create<Room>();
                    room.IsWaterSource = type == RoomType.WaterSource;
                    if (type != RoomType.Normal && type != RoomType.Rope && type != RoomType.WaterSource)
                        room.Items.MaxCount = 0;
                    else
                        room.Items.MaxCount = room.IsWaterSource ? 8 : 32;
                    room.EntryCondition.MaxDistanceOnMap = 15;
                    if (loc.Map.Entrances[j].RoomType == RoomType.Rope)
                    {
                        room.EntryCondition.MaxDistanceOnMap = 75;
                        room.EntryCondition.RequiredItem = game.ItemTypes["item_rope"];
                    }
                    room.EntryCondition.RegionOnMap = loc.Map.Entrances[j].Area;
                    room.EntryCondition.HasRegionOnMap = true;
                    room.TitleId = loc.Map.Entrances[j].TitleId;
                    loc.Rooms += room;
                }

                game.World.Locations += loc;
                i++;
            }

            for (int n = 0; n < game.World.Locations.Count; n++)
            {
                Burntime.Data.BurnGfx.Save.Location info = gamdat.Locations[n];
                for (int k = 0; k < 4; k++)
                {
                    if (info.Neighbors[k] != -1)
                        game.World.Locations[n].Neighbors.Add(game.World.Locations[info.Neighbors[k]]);
                }
            }
        }

        void LoadLocations(ClassicGame game)
        {
            // for the time being only add to existing locations
            for (int i = game.World.Locations.Count + 1; i < game.World.Map.Entrances.Length + 1; i++)
            {
                Location loc = container.Create<Location>();
                loc.Id = i - 1;
                loc.Source.Water = 0; //city.WaterSource;
                loc.Source.Reserve = 0; //city.Water;
                loc.Source.Capacity = 0; //city.WaterCapacity;
                loc.Production = null;// city.Producing == -1 ? null : game.Productions[city.Producing];
                loc.AvailableProducts = new int[] { };// (int[])city.Production.Clone();
                //if (city.Danger != 0)
                //    loc.Danger = Danger.Instance((city.Danger == 3) ? "radiation" : "gas", city.DangerAmount);
                //loc.IsCity = city.IsCity;
                
                ConfigFile cfg = new ConfigFile();
                cfg.Open("maps/MAT_" + i.ToString("D3") + ".txt");

                if (loc.EntryPoint != Vector2.Zero)
                {
                    loc.EntryPoint = cfg[""].GetVector2("entry_point");
                }
                
                //loc.EntryPoint = city.EntryPoint;
                //loc.Ways = city.Ways;
                //loc.WayLengths = city.WayLengths;

                loc.Map = container.Create<Map>(new object[] { "maps/mat_" + i.ToString("D3") + ".burnmap??" + i });

                loc.Rooms = container.CreateLinkList<Room>();

                for (int j = 0; j < loc.Map.Entrances.Length; j++)
                {
                    RoomType type = loc.Map.Entrances[j].RoomType;

                    Room room = container.Create<Room>();
                    room.IsWaterSource = type == RoomType.WaterSource;
                    if (type != RoomType.Normal && type != RoomType.Rope && type != RoomType.WaterSource)
                        room.Items.MaxCount = 0;
                    else
                        room.Items.MaxCount = room.IsWaterSource ? 8 : 32;
                    room.EntryCondition.MaxDistanceOnMap = 15;
                    if (loc.Map.Entrances[j].RoomType == RoomType.Rope)
                    {
                        room.EntryCondition.MaxDistanceOnMap = 75;
                        room.EntryCondition.RequiredItem = game.ItemTypes["item_rope"];
                    }
                    room.EntryCondition.RegionOnMap = loc.Map.Entrances[j].Area;
                    room.EntryCondition.HasRegionOnMap = true;
                    room.TitleId = loc.Map.Entrances[j].TitleId;
                    loc.Rooms += room;
                }

                game.World.Locations += loc;
                i++;
            }
        }

        void LoadNPCs(ClassicGame game, Burntime.Data.BurnGfx.Save.SaveGame gamdat)
        {
            foreach (Burntime.Data.BurnGfx.Save.Character ch in gamdat.Characters)
            {
                CharacterInfo chr = ch.Info;

                if (chr.LocationId > 0 && chr.LocationId <= game.World.Locations.Count)
                {
                    Character character;
                    if ((CharClass)chr.CharType == CharClass.Trader)
                    {
                        Trader trader = container.Create<Trader>();
                        trader.HomeArea = game.World.Locations[chr.LocationId - 1];
                        character = trader;
                        character.Class = CharClass.Trader;
                        character.Mind = container.Create<Burntime.Classic.AI.SimpleMind>(new object[] { character });
                        trader.TraderId = chr.NameId;
                    }
                    else if ((CharClass)chr.CharType == CharClass.Mutant)
                    {
                        if (chr.NameId == 178)
                        {
                            character = container.Create<Dog>();
                            character.Class = CharClass.Dog;
                        }
                        else
                        {
                            character = container.Create<Mutant>();
                            character.Class = CharClass.Mutant;
                        }
                        character.Mind = container.Create<Burntime.Classic.AI.CreatureMind>(new object[] { character });
                    }
                    else
                    {
                        character = container.Create<Character>();
                        character.Class = (CharClass)ch.Type;
                        character.Mind = container.Create<Burntime.Classic.AI.SimpleMind>(new object[] { character });
                    }

                    character.FaceID = chr.FaceId;
                    character.Dialog.MenFile = chr.TextId;
                    character.Dialog.Parent = character;
                    character.Body = app.ResourceManager.GetData("burngfxani@syssze.raw?" + chr.SpriteId + "-" + (chr.SpriteId + 15), Burntime.Platform.Resource.ResourceLoadType.LinkOnly);
                    character.SetBodyId = Helper.GetSetBodyId(ch.Type);
                    character.Path = container.Create<Burntime.Classic.PathFinding.SimplePath>();
                    character.Items.MaxCount = 6;
                    character.Health = chr.Health;
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
                    character.Path = container.Create<Burntime.Classic.PathFinding.SimplePath>();
                    character.Mind = container.Create<Burntime.Classic.AI.PlayerControlledMind>(new object[] { character });
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

        void LoadTrader(ClassicGame game, Burntime.Data.BurnGfx.Save.SaveGame gamdat)
        {
            ConfigFile traderItems = new ConfigFile();
            traderItems.Open("trader.txt");

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

                    trader.RandomizeInventory();
                }
            }
        }

        void LoadItems(ClassicGame game, Burntime.Data.BurnGfx.Save.SaveGame gamdat)
        {
            GameSettings.ItemGeneration ground = settings.GetItemGeneration("random_items_ground");
            GameSettings.ItemGeneration room = settings.GetItemGeneration("random_items_room");
            GameSettings.ItemGeneration danger = settings.GetItemGeneration("random_items_danger_location");
            GameSettings.ItemGeneration closed = settings.GetItemGeneration("random_items_closed_room");
            GameSettings.ItemGeneration start = settings.GetItemGeneration("random_items_start");

            string[] groundItems = game.ItemTypes.GetTypesWithClass(ground.Include, ground.Exclude);
            string[] roomItems = game.ItemTypes.GetTypesWithClass(room.Include, room.Exclude);
            string[] dangerItems = game.ItemTypes.GetTypesWithClass(danger.Include, danger.Exclude);
            string[] closedItems = game.ItemTypes.GetTypesWithClass(closed.Include, closed.Exclude);
            string[] startItems = game.ItemTypes.GetTypesWithClass(start.Include, start.Exclude);

            foreach (Player p in game.Player)
            {
                // add start location items
                Location location = p.Location;

                int count = start.RandomCount;
                for (int i = 0; i < count; i++)
                {
                    string insert = startItems[Burntime.Platform.Math.Random.Next() % startItems.Length];
                    location.StoreItemRandom(game.ItemTypes[insert].Generate());
                }
            }

            foreach (Location location in game.World.Locations)
            {
                int count;

                // add ground items
                count = ground.RandomCount;
                for (int i = 0; i < count; i++)
                {
                    string insert = groundItems[Burntime.Platform.Math.Random.Next() % groundItems.Length];
                    location.DropItemRandom(game.ItemTypes[insert].Generate());
                }

                // add room items (only non cities)
                if (!location.IsCity)
                {
                    count = room.RandomCount;
                    for (int i = 0; i < count; i++)
                    {
                        string insert = roomItems[Burntime.Platform.Math.Random.Next() % roomItems.Length];
                        location.StoreItemRandom(game.ItemTypes[insert].Generate());
                    }
                }

                // add special danger location items
                if (location.Danger != null)
                {
                    count = danger.RandomCount;
                    for (int i = 0; i < count; i++)
                    {
                        string insert = dangerItems[Burntime.Platform.Math.Random.Next() % dangerItems.Length];

                        // insert items in room or drop it outside
                        if (Burntime.Platform.Math.Random.Next() % 2 == 0)
                            location.StoreItemRandom(game.ItemTypes[insert].Generate());
                        else
                            location.DropItemRandom(game.ItemTypes[insert].Generate());
                    }
                }

                // add items to closed rooms (e.g. rooms only accessible with rope)
                if (!location.IsCity)
                {
                    foreach (Room r in location.Rooms)
                    {
                        if (r.EntryCondition != null && r.EntryCondition.RequiredItem != null)
                        {
                            count = closed.RandomCount;
                            for (int i = 0; i < count; i++)
                            {
                                string insert = closedItems[Burntime.Platform.Math.Random.Next() % closedItems.Length];
                                r.Items.Add(game.ItemTypes[insert].Generate());
                            }
                        }
                    }
                }
            }


            //foreach (Burntime.Data.BurnGfx.Save.Item info in gamdat.Items)
            //{
            //    if (info.OwnerType != ItemOwnerType.Pool)
            //    {
            //        Item item = game.ItemTypes[info.SpriteId].Generate();
            //        if (info.OwnerId >= 0 && info.OwnerId < game.World.AllCharacters.Count)
            //        {
            //            game.World.AllCharacters[info.OwnerId].Items.Add(item);
            //        }
            //        else if (info.OwnerType == ItemOwnerType.Room)
            //        {
            //            if (info.RoomId < game.World.Locations[info.LocationId].Rooms.Count)
            //            {
            //                Location location = game.World.Locations[info.LocationId];
            //                Room room = location.Rooms[info.RoomId];
            //                room.Items.Add(item);

            //                // fill up empty bottles
            //                if (room.IsWaterSource && item.Type.Full != null && location.Source.Reserve >= item.Type.Full.WaterValue)
            //                {
            //                    item.MakeFull();
            //                    location.Source.Reserve -= item.WaterValue;
            //                }
            //            }
            //        }
            //        else if (info.OwnerType == ItemOwnerType.Dropped)
            //        {
            //            game.World.Locations[info.DroppedLocationId].Items.DropAt(item, info.DroppedPosition);
            //        }
            //    }
            //}
        }

        public void SaveGame(string filename)
        {
            Burntime.Framework.SaveGame game = new Burntime.Framework.SaveGame(filename, "classic", BurntimeClassic.SavegameVersion);

            game.Stream.WriteByte((byte)app.Clients.Count);
            for (int i = 0; i < app.Clients.Count; i++)
                game.Stream.WriteByte((byte)app.Clients[i].Player);
            //app.Server.StateContainer.Save(game.Stream);
            app.ActiveClient.StateContainer.Save(game.Stream);

            game.Close();
        }

        public bool LoadGame(string filename)
        {
            if (container == null)
                container = new Burntime.Framework.States.StateManager(app.ResourceManager);

            app.Clients.Clear();

            Burntime.Framework.SaveGame game = new Burntime.Framework.SaveGame(filename);

            if (game.Game != "classic" || game.Version != BurntimeClassic.SavegameVersion)
                return false;

            int player = game.Stream.ReadByte();
            List<int> ids = new List<int>();
            for (int i = 0; i < player; i++)
                ids.Add(game.Stream.ReadByte());

            container.Load(game.Stream);

            game.Close();

            app.Server = new Burntime.Framework.Network.GameServer();
            app.GameServer = app.Server;
            app.GameServer.Create(container.Root, container);

            ClassicGame classic = container.Root as ClassicGame;

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

            app.Server.Run();

            return true;
        }
    }
}
