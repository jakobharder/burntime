using System;
using System.Linq;
using Burntime.Framework;
using Burntime.Data.BurnGfx;
using Burntime.Framework.Network;
using Burntime.Platform;
using Burntime.Remaster.Logic.Generation;
using Burntime.Framework.GUI;
using Burntime.Remaster.GUI;
using System.Collections.Generic;
using System.Reflection;

namespace Burntime.Remaster;

// Runtime-only fixtures: no changes to the save-game schema.
public sealed class VisualTestScenes(BurntimeClassic app, string bossName = "Visual Test")
{
    public static readonly string[] Names =
    [
        "menu", "setup-versus-original", "setup-credits", "options", "map", "manual", "location", "inventory", "room",
        "trader", "doctor", "pub", "restaurant", "info", "statistics",
        "church", "map-return", "construction-undiscovered", "construction-discovered", "mouse-location-hints", "setup-privacy", "options-privacy"
    ];

    public static readonly string[] TouchNames =
    [
        "touch-map-selected", "touch-map-info", "touch-map-menu", "touch-location-entrances", "touch-location-destination", "touch-item-selected",
        "touch-item-secondary", "touch-item-primary", "touch-trader-left", "touch-trader-right"
    ];

    public static readonly string[] PhoneScreenNames =
    [
        .. Names, "water-source", "options-saves", "options-jukebox", "options-give-up",
        "language", "pause", "last-rival", "cutscene", "menu-edge-top", "menu-edge-bottom"
    ];

    public static readonly string[] HudMenuNames =
    [
        "hud-menu-world-solo", "hud-menu-world-party", "hud-menu-local-solo", "hud-menu-local-party",
        "hud-menu-local-city", "hud-menu-select-member", "hud-menu-dismissed", "hud-menu-drag-cancelled",
        "hud-mode-talk", "hud-mode-fight", "hud-mode-city"
    ];

    void OpenHudModeCapture(string name)
    {
        OpenPhoneHudCapture("phone-location-touch-member");
        var player = app.Game.World.ActivePlayerObj;
        if (name.EndsWith("city"))
        {
            player.Location = app.Game.World.Locations.First(location => location.IsCity);
            player.Character.Position = player.Location.EntryPoint;
            app.SetScene("LocationScene");
        }
        app.LastInputMode = InputMode.Touch;
        app.Process(0);
        var scene = (LocationScene)ReadPrivate<Scene>(app.SceneManager, "activeScene");
        Button button = Descendants(scene).OfType<MainUiOriginalWindow>().Single().InteractionModeButton;
        if (name.EndsWith("city"))
        {
            if (button.IsVisible) throw new InvalidOperationException("Cities must not offer a fight toggle.");
            return;
        }
        if (!button.IsVisible) throw new InvalidOperationException("The local HUD must show its interaction-mode button.");
        var location = player.Location;
        Vector2 point = button.PositionOnScreen + button.Size / 2;
        Touch(TouchGestureKind.Tap, point, point);
        if (ReadPrivate<object>(scene, "interactionMode").ToString() != "Fight")
            throw new InvalidOperationException("The mode button must switch from Talk to Fight.");
        if (name.EndsWith("talk"))
        {
            Touch(TouchGestureKind.Tap, point, point);
            if (ReadPrivate<object>(scene, "interactionMode").ToString() != "Talk")
                throw new InvalidOperationException("The mode button must switch back from Fight to Talk.");
        }
        if (player.Location != location || Descendants(scene).OfType<MenuWindow>().Any(menu => menu.IsVisible))
            throw new InvalidOperationException("The mode button must not open a menu or trigger map travel.");
    }

    void OpenHudMenuCapture(string name)
    {
        bool local = name.Contains("local") || name.EndsWith("drag-cancelled");
        bool party = name.Contains("party") || name.EndsWith("select-member") || name.EndsWith("drag-cancelled");
        if (party) OpenPhoneHudCapture(local ? "phone-location-gamepad-member" : "phone-map-touch-group");
        else Open(local ? "location" : "map");
        var player = app.Game.World.ActivePlayerObj;
        if (!party)
        {
            foreach (var member in player.Party.Where(member => member != player.Character).ToArray())
                player.Party.Remove(member);
            player.SelectGroup(player.Party);
        }
        if (name.EndsWith("city"))
        {
            player.Location = app.Game.World.Locations.First(location => location.IsCity);
            player.Character.Position = player.Location.EntryPoint;
            app.SetScene("LocationScene");
        }
        app.LastInputMode = InputMode.Touch;
        app.Process(0);
        Scene scene = ReadPrivate<Scene>(app.SceneManager, "activeScene");
        Button trigger = Descendants(scene).OfType<Button>().Single(button => button.Text?.ID == "@controls?22");
        Vector2 origin = trigger.PositionOnScreen + trigger.Size / 2;
        Touch(TouchGestureKind.Tap, origin, origin);
        var popup = Descendants(scene).OfType<MenuWindow>().Single();
        if (!popup.IsVisible || !popup.UsesTouchLayout || popup.ContinuesTouchHold)
            throw new InvalidOperationException("The HUD button must open a persistent touch menu with one tap.");
        app.Process(0);
        if (!popup.IsVisible || popup.TouchPanelCount != (party ? 2 : 1) || popup.TouchRowHeight < 19)
            throw new InvalidOperationException("HUD menu panels or touch row spacing are incorrect.");
        Rect menuArea = Descendants(scene).OfType<MapView>().First().Boundings.Intersect(app.Engine.Resolution.SafeArea);
        Vector2 centerOffset = popup.Boundings.Center - menuArea.Center;
        if (System.Math.Abs(centerOffset.x) > 1 || System.Math.Abs(centerOffset.y) > 1)
            throw new InvalidOperationException("HUD menu panels must be centered together in the safe map area.");
        var entries = ReadPrivate<List<MenuItem>>(popup, "_menuEntries");
        for (int i = 0; i < entries.Count; i++)
            if (popup.TouchEntryAt(popup.TouchEntryBounds(i).Center) != i)
                throw new InvalidOperationException("Every menu row must be directly tappable.");
        if (name.EndsWith("select-member"))
        {
            int index = ReadPrivate<int>(popup, "touchSectionStart") + 2;
            Vector2 point = popup.PositionOnScreen + popup.TouchEntryBounds(index).Center;
            Touch(TouchGestureKind.Tap, point, point);
            if (popup.IsVisible || !player.SingleMode || player.SelectedCharacter != player.Party[1])
                throw new InvalidOperationException("Touching a party member must select that member and close the menu.");
        }
        if (name.EndsWith("dismissed"))
        {
            var destination = player.Destination;
            Touch(TouchGestureKind.Tap, origin, origin);
            if (popup.IsVisible || player.Destination != destination)
                throw new InvalidOperationException("Outside tap must dismiss without activating the map or reopening the menu.");
        }
        if (name.EndsWith("drag-cancelled"))
        {
            Vector2 start = popup.PositionOnScreen + popup.TouchEntryBounds(0).Center;
            Vector2 end = popup.PositionOnScreen + popup.TouchEntryBounds(1).Center;
            app.SceneManager.QueueTouchGesture(new(TouchGestureKind.Press, start, start, default, touchTestTime += .1), app.SceneManager.TouchInputContext);
            app.Process(0);
            app.SceneManager.QueueTouchGesture(new(TouchGestureKind.Tap, start, end, default, touchTestTime += .1), app.SceneManager.TouchInputContext);
            app.Process(0);
            if (!popup.IsVisible || ReadPrivate<Scene>(app.SceneManager, "activeScene") != scene)
                throw new InvalidOperationException("Releasing on a different row must not execute either action.");
        }
        app.RenderMouse = false;
    }

    public string[] ValidateSafeScreenCapture(string name)
    {
        Scene scene = ReadPrivate<Scene>(app.SceneManager, "activeScene");
        Rect safe = app.Engine.Resolution.SafeArea;
        List<string> issues = [];
        if (app.SmallDeviceUi && app.IsNewGfx && scene is Scenes.InfoScene campInfo)
        {
            Rect board = campInfo.CompactBoardBoundsOnScreen;
            if (board.Left < safe.Left || board.Top < safe.Top || board.Right > safe.Right || board.Bottom > safe.Bottom)
                issues.Add($"{name}: the camp noticeboard is outside the safe area.");
        }
        foreach (Window window in Descendants(scene))
        {
            bool visible = true;
            for (Window? parent = window; parent != null; parent = parent.Parent)
                visible &= parent.IsVisible;
            if (!visible || window.Size.x <= 0 || window.Size.y <= 0 ||
                window is not (Button or FaceWindow or ItemGridWindow or ManualWindow or TooltipWindow or MenuWindow) &&
                window.Parent is not InputPromptOverlay)
                continue;
            // The tab rail is decorative; its individual buttons are checked.
            if (window.GetType().Name == "TabRailButton") continue;
            Rect bounds = window.Boundings + (window.Parent?.PositionOnScreen ?? Vector2.Zero);
            if (window is MenuWindow menu) bounds.Right += menu.PromptOverflowWidth;
            if (bounds.Left < safe.Left || bounds.Top < safe.Top || bounds.Right > safe.Right || bounds.Bottom > safe.Bottom)
                issues.Add($"{name}: {window.GetType().Name} at {bounds.Left},{bounds.Top}–{bounds.Right},{bounds.Bottom} exceeds safe area {safe.Left},{safe.Top}–{safe.Right},{safe.Bottom}.");
        }
        return issues.ToArray();
    }

    bool gameCreated;
    public static readonly string[] StoreNames = ["01-city", "02-sana", "03-world-map", "04-trader"];
    public static readonly string[] PhoneHudNames =
    [
        "phone-map-touch-group", "phone-map-gamepad-group", "phone-map-gamepad-member",
        "phone-location-touch-group", "phone-location-gamepad-member", "phone-location-gamepad-actions"
    ];
    public string CaptureCityName { get; private set; } = "";
    int storeRoomIndex;
    public object StoreCaptureState
    {
        get
        {
            var camp = app.Game.World.Locations[1];
            return new
            {
                camp = "Sana", room = storeRoomIndex, owner = camp.Player?.Name,
                rats = camp.Rooms[storeRoomIndex].Items.Count(item => item.ID == "item_rats"),
                ratTraps = camp.Rooms[storeRoomIndex].Items.Count(item => item.ID == "item_rat_trap"),
                foodPerDay = camp.GetFoodProductionRate().FoodPerDay,
                offer = new[] { "item_meat", "item_rags", "item_bones" },
                focusedMaterial = "item_spring"
            };
        }
    }

    // A seeded, in-memory campaign shared by store captures on every platform.
    // No save schema or player profile is changed, and no simulation workers run.
    public void OpenStoreCapture(string name, InputMode inputMode)
    {
        if (name.StartsWith("phone-", StringComparison.Ordinal))
        {
            OpenPhoneHudCapture(name);
            return;
        }
        if (!gameCreated)
        {
            Open("map");
            app.Game.World.Day = 42;
            var player = app.Game.World.ActivePlayerObj;
            foreach (int id in new[] { 0, 1, 3, 4, 5, 6, 14, 15 })
            {
                var camp = app.Game.World.Locations[id];
                if (!camp.IsCity) camp.Player = player;
            }
            FillStoreItems(app.SelectedCharacter.Items,
                ["item_meat", "item_rags", "item_bones", "item_knife", "item_water_bottle", "item_wire"]);
            var sanaCamp = app.Game.World.Locations[1];
            storeRoomIndex = Array.FindIndex(sanaCamp.Map.Entrances, entrance => entrance.RoomType == RoomType.Normal);
            FillStoreItems(sanaCamp.Rooms[storeRoomIndex].Items, ["item_rats", "item_rats", "item_rat_trap"]);
            // A stationed survivor operates the trap; the stock and production persist on the map.
            var survivor = sanaCamp.Characters.First(character => character.IsHuman && !character.IsTrader);
            survivor.Player = player;
            sanaCamp.SelectProduction(app.Game.ItemTypes["item_rat_trap"].Production);
            Log.Info($"STORE Sana owner={sanaCamp.Player?.Name}, room={storeRoomIndex}, stock={sanaCamp.Rooms[storeRoomIndex].Items.Count}, capacity={sanaCamp.Rooms[storeRoomIndex].Items.MaxCount}, rate={sanaCamp.GetFoodProductionRate().FoodPerDay}");
        }
        app.LastInputMode = inputMode;
        app.RenderMouse = false;
        var world = app.Game.World;
        var boss = world.ActivePlayerObj;
        var sana = world.Locations[1];
        var city = world.Locations[13]; // Antella.
        CaptureCityName = app.ResourceManager.GetString("burn?" + city.Id);
        world.Day = name == "01-city" ? 45 : 42;
        var destination = name == "01-city" ? city : sana;
        boss.Location = destination;
        boss.Character.Position = destination.EntryPoint;
        boss.Character.Path.Stop(boss.Character.Position);
        boss.RefreshScrollPosition = true;

        if (name is "01-city" or "02-sana")
        {
            app.SetScene("LocationScene");
            app.Process(0);
            var scene = ReadPrivate<Scene>(app.SceneManager, "activeScene");
            var view = Descendants(scene).OfType<MapView>().First();
            var entrances = view.Map.Entrances;
            var entrance = name == "02-sana" ? entrances[storeRoomIndex] :
                entrances.First(e => e.RoomType == RoomType.Trader);
            // Stay within interaction range, with the boss clear of the entrance marker.
            boss.Character.Position = new Vector2(entrance.Area.Right + 10, entrance.Area.Bottom + 8);
            boss.Character.Path.Stop(boss.Character.Position);
            var center = boss.Character.Position + new Vector2(0, -25);
            app.Process(0);
            if (inputMode == InputMode.Touch)
                ReadPrivate<Maps.MapViewOverlayTouch>(scene, "touch").UpdateOverlay(app.Game, .3f);
            if (inputMode == InputMode.Gamepad && name == "01-city")
            {
                var nearby = ReadPrivate<Maps.MapViewOverlayNearbyAction>(scene, "nearbyAction");
                nearby.UpdateOverlay(app.Game, 0);
                int traderEntrance = Array.IndexOf(entrances, entrance);
                for (int attempt = 0; attempt < 32; attempt++)
                {
                    if (view.HoveredObject is Maps.EntranceObject selected && selected.Number == traderEntrance)
                        break;
                    nearby.CycleTarget(1);
                }
            }
            view.CenterTo(center);
            if (inputMode == InputMode.Mouse)
            {
                app.DeviceManager.MouseMove(entrance.Area.Center + view.ScrollPosition + view.Position);
                app.Process(0);
            }
        }
        else if (name == "03-world-map")
        {
            app.SetScene("MapScene");
            if (inputMode == InputMode.Mouse)
            {
                app.Process(0);
                var scene = ReadPrivate<Scene>(app.SceneManager, "activeScene");
                var view = Descendants(scene).OfType<MapView>().First();
                app.DeviceManager.MouseMove(view.Map.Entrances[sana.Id].Area.Center +
                    view.ScrollPosition + view.PositionOnScreen);
                app.Process(0);
            }
        }
        else if (name == "04-trader")
        {
            var trader = world.Traders.OrderBy(t => t.TraderId).First();
            FillStoreItems(trader.Items,
                ["item_meat", "item_water_bottle", "item_empty_canteen", "item_spring", "item_rope", "item_screws"]);
            world.ActiveTraderObj = trader;
            app.SetScene("TraderScene");
            app.Process(0);
            var scene = ReadPrivate<Scene>(app.SceneManager, "activeScene");
            var playerInventory = ReadPrivate<InventoryWindow>(scene, "inventory");
            var traderInventory = ReadPrivate<InventoryWindow>(scene, "inventoryTrader");
            // Use the same selection commands as the live UI; leave the trade uncommitted.
            playerInventory.Grid.LeftClickItemEvent.Execute(playerInventory.Grid[0]); // Meat.
            playerInventory.Grid.LeftClickItemEvent.Execute(playerInventory.Grid[1]); // Rags.
            playerInventory.Grid.LeftClickItemEvent.Execute(playerInventory.Grid[2]); // Bones.
            traderInventory.Grid.LeftClickItemEvent.Execute(traderInventory.Grid[1]); // Water.
            traderInventory.Grid.LeftClickItemEvent.Execute(traderInventory.Grid[2]); // Canteen.
            if (inputMode == InputMode.Gamepad) Press(InputAction.MoveRight);
            traderInventory.Grid.FocusItem(3); // Spring: Needed for ??? (an undiscovered trap recipe).
            if (inputMode == InputMode.Mouse)
            {
                var spring = ReadPrivate<ItemWindow[]>(traderInventory.Grid, "itemWindows")[3];
                app.DeviceManager.MouseMove(spring.PositionOnScreen + spring.Size / 2);
                app.Process(0);
            }
        }
        app.LastInputMode = inputMode;
    }

    void OpenPhoneHudCapture(string name)
    {
        InputMode mode = name.Contains("-touch-") ? InputMode.Touch : InputMode.Gamepad;
        OpenStoreCapture("03-world-map", mode);
        var player = app.Game.World.ActivePlayerObj;
        if (player.Party.Count == 1)
        {
            var recruits = app.Game.World.Locations.SelectMany(location => location.Characters)
                .Where(character => (character.Class is CharClass.Mercenary or CharClass.Technician or CharClass.Doctor) &&
                    !player.Party.Contains(character)).Take(2).ToArray();
            foreach (var recruit in recruits)
            {
                recruit.Player = player;
                recruit.Location = player.Location;
                recruit.Position = player.Character.Position + new Vector2(12 * player.Party.Count, 0);
                recruit.Path.Stop(recruit.Position);
                player.Party.Add(recruit);
            }
        }
        if (player.Party.Count != 3)
            throw new InvalidOperationException("Phone HUD fixture requires a three-person party.");
        for (int i = 0; i < player.Party.Count; i++)
        {
            var member = player.Party[i];
            member.Health = i == 0 ? 73 : 42;
            member.Food = 4;
            member.Water = i == 2 ? 0 : 3;
            member.Items.Clear();
            member.Position = player.Character.Position + new Vector2(12 * i, 0);
            member.Path.Stop(member.Position);
        }
        player.SelectGroup(player.Party);
        app.Game.World.Time = .55f;
        bool local = name.StartsWith("phone-location-", StringComparison.Ordinal);
        app.SetScene(local ? "LocationScene" : "MapScene");
        app.LastInputMode = mode;
        app.Process(0);
        if (name.EndsWith("-member") || name.EndsWith("-actions"))
        {
            app.Process(.2f); // Release the previous capture's controller-cycle debounce.
            Press(InputAction.RightArea);
            if (player.SelectedCharacter != player.Party[1])
                throw new InvalidOperationException("Phone HUD fixture did not select the second party member.");
        }
        app.Game.World.Time = .55f;
        var scene = ReadPrivate<Scene>(app.SceneManager, "activeScene");
        var view = Descendants(scene).OfType<MapView>().First();
        if (local)
            view.CenterTo(player.Character.Position + new Vector2(0, -20));
        else
        {
            int destination = player.Location.Neighbors.First(location => !location.IsCity).Id;
            view.ActiveEntrance = destination;
            view.CenterTo(view.Map.Entrances[destination].Area.Center);
        }
        app.Process(0);
        if (mode == InputMode.Gamepad)
        {
            var inline = new InputPromptController(app, scene.Prompts).Resolve().InlinePrompts;
            if (!inline.Any(entry => entry.Prompt.Action == InputAction.LeftArea) ||
                !inline.Any(entry => entry.Prompt.Action == InputAction.RightArea) ||
                local && !inline.Any(entry => entry.Prompt.Action == InputAction.Secondary))
                throw new InvalidOperationException("Phone party selection/action prompts are missing.");
            if (inline.Any(entry => entry.Position.x < app.Engine.Resolution.SafeArea.Left ||
                entry.Position.x >= app.Engine.Resolution.SafeArea.Left + MainUiOriginalWindow.CompactWidth))
                throw new InvalidOperationException("Phone party prompts are outside the HUD.");
        }
        if (name.EndsWith("-actions"))
        {
            Press(InputAction.Secondary);
            if (!Descendants(scene).OfType<MenuWindow>().Any(window => window.IsVisible))
                throw new InvalidOperationException("Party actions did not open in the phone HUD fixture.");
        }
        Log.Info($"PHONE HUD party={player.Party.Count}, single={player.SingleMode}, selected={player.SelectedCharacter.Name}, mode={mode}");
    }

    public void ValidatePhoneHudCapture(string name)
    {
        var scene = ReadPrivate<Scene>(app.SceneManager, "activeScene");
        var view = Descendants(scene).OfType<MapView>().First();
        Rect safe = app.Engine.Resolution.SafeArea;
        int hudRight = safe.Left + MainUiOriginalWindow.CompactWidth;
        if (view.Position.x != hudRight || view.Position.y != 0 || view.Size.y != 160 ||
            view.Position.x + view.Size.x != app.Engine.Resolution.Game.x)
            throw new InvalidOperationException("Phone party map does not fill the area beside the HUD.");
        var portrait = Descendants(scene).OfType<FaceWindow>().First();
        if (portrait.PositionOnScreen.x < safe.Left || portrait.PositionOnScreen.y < safe.Top ||
            portrait.PositionOnScreen.x + portrait.Size.x > hudRight ||
            portrait.PositionOnScreen.y + portrait.Size.y > safe.Bottom)
            throw new InvalidOperationException("Phone portrait is outside the safe HUD area.");
        if (!name.EndsWith("-actions"))
        {
            var overlay = Descendants(scene).OfType<InputPromptOverlay>().First();
            var context = ReadPrivate<Window>(overlay, "_contextRow");
            if (ReadPrivate<bool>(context, "_hasVisiblePrompts") &&
                (context.Boundings.Left < hudRight || context.Boundings.Right > safe.Right ||
                 context.Boundings.Bottom > safe.Bottom))
                throw new InvalidOperationException("Target-specific prompts must stay on the map.");
            var globalRows = ReadPrivate<System.Collections.IEnumerable>(overlay, "_sidebarRows");
            foreach (Window row in globalRows)
                if (ReadPrivate<bool>(row, "_hasVisiblePrompts") &&
                    (row.Boundings.Left < safe.Left || row.Boundings.Right > hudRight ||
                     row.Boundings.Top < safe.Top || row.Boundings.Bottom > safe.Bottom))
                    throw new InvalidOperationException("Global prompts must stay inside the HUD.");
        }
        if (name.Contains("-gamepad-") && !name.EndsWith("-actions"))
        {
            var overlay = Descendants(scene).OfType<InputPromptOverlay>().First();
            var rows = ReadPrivate<System.Collections.IDictionary>(overlay, "_inlineRows");
            int partyRows = 0;
            foreach (Window row in rows.Values)
            {
                if (row.Boundings.Left < safe.Left || row.Boundings.Right > hudRight || row.Boundings.Bottom > safe.Bottom)
                    throw new InvalidOperationException("Phone HUD inline prompts are outside safe bounds.");
                if (row.Boundings.Top >= safe.Top + MainUiOriginalWindow.CompactPartyPromptY) continue;
                partyRows++;
                if (row.Boundings.Top < safe.Top || row.Boundings.Bottom > safe.Top + MainUiOriginalWindow.CompactPartyPromptY)
                    throw new InvalidOperationException("Phone party controls are clipped or outside their HUD row.");
            }
            if (partyRows < 2)
                throw new InvalidOperationException("Phone party controls were not rendered.");
        }
        if (name.EndsWith("-actions"))
        {
            var menu = Descendants(scene).OfType<MenuWindow>().First(window => window.IsVisible);
            if (ReadPrivate<List<MenuItem>>(menu, "_menuEntries").Count != 3)
                throw new InvalidOperationException("Party menu must show All, Dismiss and Stay there.");
        }
    }

    void FillStoreItems(ItemList items, string[] ids)
    {
        items.Clear();
        foreach (string id in ids)
            items.Add(app.Game.Container.Create<Item>(app.Game.ItemTypes[id]));
    }
    public bool SaveAndReload(string path)
    {
        var creation = new GameCreation(app);
        return creation.SaveGame(path) && creation.LoadGame(path, startServer: false);
    }
    Item[]? serviceItems;
    int serviceHealth;
    Item[]? equipmentItems;
    Item? equippedProtection;
    CharClass? constructionClass;
    Item[]? constructionRoomItems;

    public void Open(string name)
    {
        if (name.StartsWith("hud-mode-", StringComparison.Ordinal))
        {
            OpenHudModeCapture(name);
            return;
        }
        if (name.StartsWith("hud-menu-", StringComparison.Ordinal))
        {
            OpenHudMenuCapture(name);
            return;
        }
        if (name.StartsWith("touch-"))
        {
            OpenTouchFixture(name);
            return;
        }
        app.LastInputMode = InputMode.Keyboard;
        if (name != "construction-discovered" && equipmentItems != null)
        {
            app.SelectedCharacter.Items.Clear();
            foreach (Item item in equipmentItems)
                app.SelectedCharacter.Items.Add(item);
            app.SelectedCharacter.Protection = equippedProtection;
            equipmentItems = null;
        }
        if (name != "construction-discovered" && constructionClass.HasValue)
        {
            app.SelectedCharacter.Class = constructionClass.Value;
            app.InventoryRoom.Items.Clear();
            foreach (Item item in constructionRoomItems!)
                app.InventoryRoom.Items.Add(item);
            constructionClass = null;
            constructionRoomItems = null;
        }
        if (serviceItems != null)
        {
            Press(InputAction.Back); // Return the uncommitted offer through the normal exit action.
            for (int i = 0; i < serviceItems.Length; i++)
                app.SelectedCharacter.Items[i] = serviceItems[i];
            app.SelectedCharacter.Health = serviceHealth;
            serviceItems = null;
        }
        if (name is not ("menu" or "setup-notes" or "setup-versus-original" or "setup-credits" or "options") && !gameCreated)
        {
            Platform.Math.SetRandomSeed(123);
            new GameCreation(app).CreateNewGame(new NewGameInfo
            {
                NameOne = bossName, NameTwo = "", FaceOne = 0, FaceTwo = -1,
                ColorOne = BurntimePlayerColor.Green, ColorTwo = BurntimePlayerColor.Red,
                Difficulty = 1, Rules = RuleSet.Extended, AI = AiProfile.None
            }, startServer: false);
            app.ActiveClient = new GameClient(app, 0, app.Server.StateContainer);
            app.Game.World.ActivePlayer = 0;
            app.Game.World.ActivePlayerObj.SelectCharacter(app.Game.World.ActivePlayerObj.Character);
            gameCreated = true;
        }

        switch (name)
        {
            case "setup-privacy":
            case "options-privacy":
                app.SetScene(name == "setup-privacy" ? "MenuScene" : "OptionsScene");
                app.Process(0);
                var privacyScene = ReadPrivate<Scene>(app.SceneManager, "activeScene");
                ReadPrivate<ManualWindow>(privacyScene, name == "setup-privacy" ? "_setupNotes" : "_privacyWindow").OpenPrivacyPolicy();
                break;
            case "menu": app.SetScene("MenuScene"); break;
            case "menu-edge-top":
            case "menu-edge-bottom":
                Open("map");
                app.Process(0);
                Scene edgeScene = ReadPrivate<Scene>(app.SceneManager, "activeScene");
                edgeScene.GetType().GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(edgeScene, [name == "menu-edge-top" ? Vector2.Zero : app.Engine.Resolution.Game, false, false]);
                break;
            case "water-source":
                var waterLocation = app.Game.World.Locations.First(location => location.Source != null && location.Rooms.Count > 0);
                app.Game.World.ActivePlayerObj.Location = waterLocation;
                app.InventoryRoom = waterLocation.Rooms[0];
                app.InventoryRoom.IsWaterSource = true;
                app.InventoryBackground = 0;
                app.SetScene("InventoryScene", app.SelectedCharacter);
                break;
            case "options-saves":
            case "options-jukebox":
            case "options-give-up":
                app.SetScene("OptionsScene");
                app.Process(0);
                ReadPrivate<ManualWindow>(ReadPrivate<Scene>(app.SceneManager, "activeScene"), "_privacyWindow").Hide();
                if (name != "options-saves") Press(InputAction.RightArea);
                if (name == "options-give-up") { Press(InputAction.RightArea); Press(InputAction.RightArea); }
                break;
            case "language": app.SetScene("LanguageScene"); break;
            case "pause": app.SetScene("PauseScene"); break;
            case "last-rival": app.SetScene("LastRivalScene"); break;
            case "setup-notes":
                // Reach the new button through the normal setup focus order.
                Press(InputAction.MoveDown);
                Press(InputAction.MoveDown);
                Press(InputAction.MoveLeft);
                Press(InputAction.Primary);
                break;
            case "setup-versus-original":
                // Exercise opening patch notes without capturing changing changelog content.
                Open("setup-notes");
                Press(InputAction.Back);
                // Retain coverage of the physical Ctrl and gamepad shortcuts.
                app.DeviceManager.VKeyPress(SystemKey.Ctrl);
                app.Process(0);
                Press(InputAction.Back);
                app.DeviceManager.GamepadControlPress(GamepadControl.View);
                app.Process(0);
                Press(InputAction.RightArea);
                break;
            case "setup-credits":
                Press(InputAction.RightArea);
                break;
            case "manual":
                app.ShowManualOnNextWorldMap = true;
                app.SetScene("MapScene");
                break;
            case "options":
                Press(InputAction.Back); // Close setup notes before leaving the menu.
                app.SetScene("OptionsScene");
                Press(InputAction.RightArea); // Saves -> jukebox -> settings.
                Press(InputAction.RightArea);
                break;
            case "map":
            case "map-return":
                app.SetScene("MapScene");
                Press(InputAction.MoveLeft); // Select a neighboring camp to show travel time.
                break;
            case "mouse-location-hints":
                Open("location");
                var hintLocation = app.Game.World.ActiveLocationObj;
                var hintRoom = hintLocation.Rooms[0];
                hintLocation.Player = app.Game.World.ActivePlayerObj;
                hintRoom.Items.Clear();
                hintRoom.Items.Add(app.Game.Container.Create<Item>(app.Game.ItemTypes["item_knife"]));
                hintRoom.Items.Add(app.Game.Container.Create<Item>(app.Game.ItemTypes["item_maggots"]));
                hintLocation.Production = app.Game.ItemTypes["item_knife"].Production;
                if (hintLocation.GetFoodProductionRate().FoodPerDay <= 0)
                    throw new InvalidOperationException("Mouse hint fixture requires active food production.");
                app.LastInputMode = InputMode.Mouse;
                app.Process(0);
                var hintScene = ReadPrivate<Scene>(app.SceneManager, "activeScene");
                var hintView = Descendants(hintScene).OfType<MapView>().First();
                hintView.CenterTo(hintRoom.MapArea.Center);
                app.DeviceManager.MouseMove(hintRoom.MapArea.Center + hintView.ScrollPosition + hintView.Position);
                app.Process(0);
                if (hintLocation.Hover?.Room != hintRoom)
                    throw new InvalidOperationException("Mouse did not hover the food production room.");
                break;
            case "location":
                Press(InputAction.Back); // Close the field manual before leaving the map.
                app.SetScene("LocationScene");
                break;
            case "inventory":
            case "room":
                // Exercise armour and hazard rows with the actual equipped item.
                equipmentItems = app.SelectedCharacter.Items.Cast<Item>().ToArray();
                equippedProtection = app.SelectedCharacter.Protection;
                if (app.SelectedCharacter.Protection is Item previousProtection)
                    app.SelectedCharacter.Items.Remove(previousProtection);
                Item protection = app.Game.Container.Create<Item>(app.Game.ItemTypes[
                    name == "inventory" ? "item_steel_helmet" : "item_protective_suit"]);
                app.SelectedCharacter.Items.Add(protection);
                app.SelectedCharacter.Protection = protection;
                app.InventoryBackground = name == "room" ? 0 : -1;
                app.InventoryRoom = name == "room" ? app.Game.World.ActiveLocationObj.Rooms[0] : null;
                app.SetScene("InventoryScene", app.SelectedCharacter);
                Press(InputAction.MoveLeft); // Enter item focus from the initial page selection.
                if (name == "room")
                {
                    // Cross the three inventory columns to retain the room's Bible tooltip.
                    Press(InputAction.MoveRight);
                    Press(InputAction.MoveRight);
                    Press(InputAction.MoveRight);
                }
                break;
            case "construction-undiscovered":
                // A spring reveals only the trap recipe; tin and wire stay absent
                // so the second capture must offer Check materials, not Build.
                equipmentItems = app.SelectedCharacter.Items.Cast<Item>().ToArray();
                equippedProtection = app.SelectedCharacter.Protection;
                constructionClass = app.SelectedCharacter.Class;
                app.SelectedCharacter.Class = CharClass.Technician;
                app.SelectedCharacter.Protection = null;
                app.SelectedCharacter.Items.Clear();
                app.SelectedCharacter.Items.Add(app.Game.Container.Create<Item>(
                    app.Game.ItemTypes["item_spring"]));
                app.InventoryBackground = 0;
                app.InventoryRoom = app.Game.World.ActiveLocationObj.Rooms[0];
                constructionRoomItems = app.InventoryRoom.Items.Cast<Item>().ToArray();
                app.InventoryRoom.Items.Clear();
                if (app.Game.IsConstructionKnown("item_trap"))
                    throw new InvalidOperationException("Construction fixture requires an undiscovered trap recipe.");
                app.SetScene("InventoryScene", app.SelectedCharacter);
                Press(InputAction.MoveLeft);
                break;
            case "construction-discovered":
                // Reveal it through the actual investigation action, then return
                // to the same focused material without constructing anything.
                Press(InputAction.Secondary);
                if (!app.Game.IsConstructionKnown("item_trap"))
                    throw new InvalidOperationException("Investigating the spring did not reveal the trap recipe.");
                Press(InputAction.Back);
                break;
            case "trader":
                app.Game.World.ActiveTraderObj = app.Game.World.Traders[0];
                app.SetScene("TraderScene");
                break;
            case "doctor":
            case "pub":
            case "restaurant":
                serviceItems = app.SelectedCharacter.Items.Cast<Item>().ToArray();
                serviceHealth = app.SelectedCharacter.Health;
                if (name == "doctor")
                    app.SelectedCharacter.Health = 30; // Injured patient: preview the snake's healing.
                app.SetScene("ServiceScene", new MapEntrance
                {
                    RoomType = name == "doctor" ? RoomType.Doctor :
                        name == "pub" ? RoomType.Pub : RoomType.Restaurant,
                    Background = 22
                });
                string itemId = name == "doctor" ? "item_snake" :
                    name == "pub" ? "item_bottle" : "item_knife";
                int itemIndex = Array.FindIndex(serviceItems, item => item.ID == itemId);
                if (itemIndex < 0 || itemIndex >= 3)
                    throw new InvalidOperationException($"Expected {itemId} in the first inventory row.");
                for (int i = 0; i < itemIndex; i++)
                    Press(InputAction.MoveRight);
                Press(InputAction.Primary); // Space/Enter: offer the item, without buying the service.
                if (app.SelectedCharacter.Items.Contains(serviceItems[itemIndex]))
                    throw new InvalidOperationException($"Service fixture did not offer {itemId}.");
                break;
            case "info":
                app.InfoCity = app.Game.World.ActiveLocationObj.Id;
                app.SetScene("InfoScene");
                break;
            case "camp-info-overview":
            case "camp-info-production":
            case "camp-info-storage":
                OpenStoreCapture("03-world-map", InputMode.Gamepad);
                var infoCamp = app.Game.World.Locations[1];
                var infoOwner = app.Game.World.ActivePlayerObj;
                infoCamp.Player = infoOwner;
                int infoRoom = Array.FindIndex(infoCamp.Map.Entrances, entrance => entrance.RoomType == RoomType.Normal);
                FillStoreItems(infoCamp.Rooms[infoRoom].Items,
                    ["item_rats", "item_rats", "item_rat_trap", "item_meat", "item_wire", "item_bones"]);
                infoCamp.SelectProduction(app.Game.ItemTypes["item_rat_trap"].Production);
                foreach (CharClass role in new[] { CharClass.Mercenary, CharClass.Technician, CharClass.Doctor })
                {
                    var staff = app.Game.World.Locations.SelectMany(location => location.Characters)
                        .Where(character => character.Class == role && !infoOwner.Party.Contains(character))
                        .Take(5).ToArray();
                    foreach (var character in staff)
                    {
                        character.Player = infoOwner;
                        character.Location = infoCamp;
                        character.Health = 75;
                    }
                }
                app.InfoCity = infoCamp.Id;
                app.SetScene("InfoScene");
                app.Process(0);
                if (name != "camp-info-production") Press(InputAction.Primary);
                if (name == "camp-info-storage")
                {
                    var infoScene = ReadPrivate<Scene>(app.SceneManager, "activeScene");
                    var infoGrid = ReadPrivate<ItemGridWindow>(infoScene, "grid");
                    infoGrid.FocusVisible = true;
                    infoGrid.FocusItem(0);
                }
                break;
            case "statistics": app.SetScene("StatisticsScene"); break;
            case "church": app.SetImageScene("scenes/church.txt"); break;
            case "cutscene": app.SetImageScene("scenes/film_05.txt"); break;
            default: throw new ArgumentException($"Unknown visual scenario: {name}");
        }
        app.RenderMouse = false;
    }

    void Press(InputAction action)
    {
        app.InputManager.Press(action);
        app.Process(0);
    }

    // Exercise real dispatch against isolated fixture state, including cancellation.
    void OpenTouchFixture(string name)
    {
        if (name.StartsWith("touch-location-"))
        {
            if (!gameCreated) Open("map");
            app.SetScene("LocationScene");
            app.LastInputMode = InputMode.Touch;
            app.Process(0);
            var locationScene = ReadPrivate<Scene>(app.SceneManager, "activeScene");
            var view = Descendants(locationScene).OfType<MapView>().First();
            var overlay = ReadPrivate<Maps.MapViewOverlayTouch>(locationScene, "touch");
            var actor = app.SelectedCharacter;
            var area = view.Map.Entrances[0].Area;
            actor.Position = area.Center;
            actor.Path.Stop(actor.Position);
            view.CenterTo(area.Center);
            overlay.UpdateOverlay(app.Game, .3f);
            overlay.HitTestEntrance(Vector2.Zero, view.ScrollPosition, view.Size);
            var targets = ReadPrivate<System.Collections.Generic.List<Maps.TouchEntranceTarget>>(overlay, "targets");
            var target = targets.First(t => t.Number == 0);
            if (target.Label == null)
                throw new InvalidOperationException("Nearby touch entrance has no label.");
            if (overlay.HitTestEntrance(target.Label.Value.Center, view.ScrollPosition, view.Size) != 0)
                throw new InvalidOperationException("Entrance label is not tappable.");
            var point = view.PositionOnScreen + target.Label.Value.Center;
            Touch(TouchGestureKind.Drag, point, point + new Vector2(20, 0));
            if (overlay.Destination != -1)
                throw new InvalidOperationException("Dragging across a label activated an entrance.");
            view.CenterTo(area.Center);
            overlay.HitTestEntrance(Vector2.Zero, view.ScrollPosition, view.Size);
            target = targets.First(t => t.Number == 0);
            point = view.PositionOnScreen + target.Label!.Value.Center;
            // Keep the actor outside entry range while exercising the real touch dispatch.
            actor.Position = area.Center + new Vector2(100, 100);
            Touch(TouchGestureKind.Tap, point, point);
            if (overlay.Destination != 0)
                throw new InvalidOperationException($"Touch entrance label did not set the destination: destination={overlay.Destination}, scene={ReadPrivate<Scene>(app.SceneManager, "activeScene").GetType().Name}, position={actor.Position}, entrance={area.Center}, hit={overlay.HitTestEntrance(point - view.PositionOnScreen, view.ScrollPosition, view.Size)}.");
            ((LocationScene)locationScene).OnMouseClickMap(actor.Position, MouseButton.Left);
            overlay.UpdateOverlay(app.Game, 0);
            if (overlay.Destination != -1)
                throw new InvalidOperationException("Cancelled command retained the entrance highlight.");
            app.LastInputMode = InputMode.Gamepad;
            overlay.IsVisible = false;
            overlay.UpdateOverlay(app.Game, 0);
            if (overlay.HitTestEntrance(point - view.PositionOnScreen, view.ScrollPosition, view.Size) != -1)
                throw new InvalidOperationException("Touch entrance overlay stayed active for gamepad.");
            app.LastInputMode = InputMode.Touch;
            overlay.IsVisible = true;
            actor.Position = area.Center;
            actor.Path.Stop(actor.Position);
            view.CenterTo(area.Center);
            overlay.UpdateOverlay(app.Game, .3f);
            if (name == "touch-location-destination")
            {
                actor.Position = area.Center + new Vector2(100, 100);
                ((LocationScene)locationScene).OnClickEntrance(0, MouseButton.Left);
                overlay.UpdateOverlay(app.Game, .3f);
                ((LocationScene)locationScene).OnMenuFight();
            }
            return;
        }
        if (name.StartsWith("touch-trader-"))
        {
            Open("trader");
            app.LastInputMode = InputMode.Touch;
            app.Process(0);
            var traderScene = ReadPrivate<Scene>(app.SceneManager, "activeScene");
            var playerInventory = ReadPrivate<InventoryWindow>(traderScene, "inventory");
            var traderInventory = ReadPrivate<InventoryWindow>(traderScene, "inventoryTrader");
            var traderExchange = ReadPrivate<ExchangeWindow>(traderScene, "exchangeTop");
            var playerExchange = ReadPrivate<ExchangeWindow>(traderScene, "exchangeBottom");
            if (!app.SmallDeviceUi && app.Engine.Resolution.Game.x < 450)
            {
                var point = traderScene.PositionOnScreen + traderScene.Size / 2;
                Swipe(point, new Vector2(40, 0));
                if (!playerInventory.IsVisible || traderInventory.IsVisible)
                    throw new InvalidOperationException("Trader swipe right did not show the player inventory.");
                if (name == "touch-trader-right")
                {
                    Swipe(point + new Vector2(1, 0), new Vector2(-40, 0));
                    if (playerInventory.IsVisible || !traderInventory.IsVisible)
                        throw new InvalidOperationException("Trader swipe left did not show the trader inventory.");
                }
            }

            InventoryWindow activeInventory = name == "touch-trader-right"
                ? traderInventory
                : playerInventory;
            ExchangeWindow activeExchange = name == "touch-trader-right"
                ? traderExchange
                : playerExchange;
            ItemWindow traderItem = Descendants(activeInventory).OfType<ItemWindow>()
                .First(window => window.IsVisible && window.Item != null);
            var selectedItem = traderItem.Item!;
            Touch(TouchGestureKind.Tap, traderItem.PositionOnScreen + traderItem.Size / 2,
                traderItem.PositionOnScreen + traderItem.Size / 2);
            if (!activeExchange.Grid.Contains(selectedItem))
                throw new InvalidOperationException("First trader item tap did not add the item to the trade.");

            if (name == "touch-trader-right")
            {
                int pageBefore = ReadPrivate<int>(traderInventory, "activePageIndex");
                // Start over the portrait, outside the item grid: the whole
                // inventory window is the vertical paging gesture target.
                Vector2 point = traderInventory.PositionOnScreen + new Vector2(100, 35);
                Swipe(point, new Vector2(0, -40));
                int pageAfter = ReadPrivate<int>(traderInventory, "activePageIndex");
                if (pageAfter == pageBefore)
                    throw new InvalidOperationException("Trader inventory swipe up did not advance the page.");
            }
            return;
        }
        bool map = name.StartsWith("touch-map-");
        Open(map ? "map" : name == "touch-item-primary" ? "room" : "inventory");
        app.LastInputMode = InputMode.Touch;
        app.Process(0);
        var scene = ReadPrivate<Scene>(app.SceneManager, "activeScene");
        Vector2 origin;
        ItemWindow item;
        if (map)
        {
            var view = Descendants(scene).OfType<MapView>().First();
            var player = app.Game.World.ActivePlayerObj;
            var departure = player.Location;
            if (name == "touch-map-menu")
            {
                Vector2? empty = null;
                // Exercise the map itself, away from edge overlays and their
                // expanded touch targets on compact layouts.
                for (int y = 32; y < view.Size.y - 16 && empty == null; y += 8)
                    for (int x = 32; x < view.Size.x - 16; x += 8)
                        if (view.HitTestEntrance(new Vector2(x, y)) < 0)
                        {
                            empty = new Vector2(x, y);
                            break;
                        }
                if (empty == null)
                    throw new InvalidOperationException("World map fixture has no empty long-press target.");
                int selectedEntrance = view.ActiveEntrance;
                origin = view.PositionOnScreen + empty.Value;
                Touch(TouchGestureKind.LongPress, origin, origin);
                MenuWindow? contextMenu = Descendants(scene).OfType<MenuWindow>()
                    .FirstOrDefault(window => window.IsVisible);
                if (contextMenu == null)
                    throw new InvalidOperationException($"Empty-map long press opened location {selectedEntrance} instead of the context menu.");
                var entries = ReadPrivate<System.Collections.Generic.List<MenuItem>>(
                    contextMenu, "_menuEntries");
                if (entries.Any(entry =>
                    entry.Shortcut.Action == InputAction.ToggleInteractionMode))
                    throw new InvalidOperationException(
                        "Touch world-map context menu contains the travel/info mode toggle.");
                contextMenu.Hide();
                return;
            }
            int destinationNumber = name == "touch-map-info" ? view.ActiveEntrance :
                Enumerable.Range(0, app.Game.World.Locations.Count).First(number =>
                    number != view.ActiveEntrance);
            var destination = app.Game.World.Locations[destinationNumber];
            var area = view.Map.Entrances[destinationNumber].Area;
            view.CenterTo(area.Center);
            origin = view.PositionOnScreen + view.ScrollPosition + area.Center;
            if (name == "touch-map-info")
            {
                Touch(TouchGestureKind.LongPress, origin, origin);
                return;
            }
            int activeBeforeTap = view.ActiveEntrance;
            var oldDestination = player.Destination;
            float oldTime = app.Game.World.Time;
            Touch(TouchGestureKind.Tap, origin, origin);
            if (player.Location != departure || player.Destination != oldDestination || app.Game.World.Time != oldTime)
                throw new InvalidOperationException($"Touch selection committed travel: departure={departure.Id}, active={activeBeforeTap}, tapped={destinationNumber}.");
            if (view.ActiveEntrance != destinationNumber)
                throw new InvalidOperationException("Touch did not select the location.");
            return;
        }
        else
        {
            item = Descendants(scene).OfType<ItemWindow>().First(window => window.IsVisible &&
                window.Item != null && window.Item == app.SelectedCharacter.Protection && window.Parent is ItemGridWindow);
            origin = item.PositionOnScreen + item.Size / 2;
        }
        var selected = app.SelectedCharacter.Protection;
        Touch(TouchGestureKind.Tap, origin, origin);
        if (((ItemGridWindow)item.Parent).FocusedItem != selected ||
            app.SelectedCharacter.Protection != selected)
            throw new InvalidOperationException("Single tap did not inspect without acting.");
        if (name == "touch-item-secondary")
        {
            Touch(TouchGestureKind.LongPress, origin, origin);
            if (app.SelectedCharacter.Protection != null)
                throw new InvalidOperationException("Long press did not execute Unequip.");
            Touch(TouchGestureKind.HoldEnd, origin, origin);
            if (app.SelectedCharacter.Protection != null)
                throw new InvalidOperationException("Long-press release executed an extra action.");
        }
        if (name == "touch-item-primary")
        {
            Touch(TouchGestureKind.Tap, origin, origin + new Vector2(14, 0), 5);
            if (app.SelectedCharacter.Items.Contains(selected))
                throw new InvalidOperationException("Second tap did not transfer the selected item.");
        }
    }

    static T ReadPrivate<T>(object owner, string name) =>
        (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;

    static IEnumerable<Window> Descendants(Container parent)
    {
        foreach (var window in parent.Windows)
        {
            if (!window.IsVisible) continue;
            yield return window;
            if (window is Container child)
                foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    double touchTestTime = 1;

    void Touch(TouchGestureKind kind, Vector2 origin, Vector2 position, double delay = 1)
    {
        if (kind is TouchGestureKind.Tap or TouchGestureKind.LongPress or
            TouchGestureKind.Drag)
        {
            app.SceneManager.QueueTouchGesture(new(TouchGestureKind.Press,
                origin, origin, default, touchTestTime += .01),
                app.SceneManager.TouchInputContext);
            app.Process(0);
        }
        app.SceneManager.QueueTouchGesture(new(kind, origin, position, default, touchTestTime += delay), app.SceneManager.TouchInputContext);
        app.Process(0);
    }

    void Swipe(Vector2 origin, Vector2 delta)
    {
        app.SceneManager.QueueTouchGesture(new(TouchGestureKind.Press,
            origin, origin, default, touchTestTime += .01),
            app.SceneManager.TouchInputContext);
        app.Process(0);
        app.SceneManager.QueueTouchGesture(new(TouchGestureKind.Drag, origin,
            origin + delta, delta, touchTestTime += 1), app.SceneManager.TouchInputContext);
        app.Process(.2f);
    }
}
