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
public sealed class VisualTestScenes(BurntimeClassic app)
{
    public static readonly string[] Names =
    [
        "menu", "setup-notes", "setup-versus-original", "setup-credits", "options", "map", "manual", "location", "inventory", "room",
        "trader", "doctor", "pub", "restaurant", "info", "statistics",
        "church", "map-return", "construction-undiscovered", "construction-discovered"
    ];

    public static readonly string[] TouchNames =
    [
        "touch-map-selected", "touch-map-info", "touch-map-menu", "touch-location-entrances", "touch-location-destination", "touch-item-selected",
        "touch-item-secondary", "touch-item-primary", "touch-trader-left", "touch-trader-right"
    ];

    bool gameCreated;
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
                NameOne = "Visual Test", NameTwo = "", FaceOne = 0, FaceTwo = -1,
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
            case "menu": app.SetScene("MenuScene"); break;
            case "setup-notes":
                // Reach the new button through the normal setup focus order.
                Press(InputAction.MoveDown);
                Press(InputAction.MoveDown);
                Press(InputAction.MoveLeft);
                Press(InputAction.Primary);
                break;
            case "setup-versus-original":
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
            case "statistics": app.SetScene("StatisticsScene"); break;
            case "church": app.SetImageScene("scenes/church.txt"); break;
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
            if (app.Engine.Resolution.Game.x < 450)
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
                for (int y = 8; y < view.Size.y && empty == null; y += 8)
                    for (int x = 8; x < view.Size.x; x += 8)
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
        app.SceneManager.QueueTouchGesture(new(kind, origin, position, default, touchTestTime += delay), app.SceneManager.TouchInputContext);
        app.Process(0);
    }

    void Swipe(Vector2 origin, Vector2 delta)
    {
        app.SceneManager.QueueTouchGesture(new(TouchGestureKind.Drag, origin,
            origin + delta, delta, touchTestTime += 1), app.SceneManager.TouchInputContext);
        app.Process(.2f);
    }
}
