using System;
using System.Linq;
using Burntime.Framework;
using Burntime.Data.BurnGfx;
using Burntime.Framework.Network;
using Burntime.Platform;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster;

// Runtime-only fixtures: no changes to the save-game schema.
public sealed class VisualTestScenes(BurntimeClassic app)
{
    public static readonly string[] Names =
    [
        "menu", "menu-mouse", "setup-notes", "setup-versus-original", "setup-credits", "options", "map", "manual", "location", "inventory", "room",
        "trader", "doctor", "pub", "restaurant", "info", "statistics",
        "church", "map-return"
    ];

    bool gameCreated;
    Item[]? serviceItems;
    int serviceHealth;

    public void Open(string name)
    {
        app.LastInputMode = InputMode.Keyboard;
        if (serviceItems != null)
        {
            Press(InputAction.Back); // Return the uncommitted offer through the normal exit action.
            for (int i = 0; i < serviceItems.Length; i++)
                app.SelectedCharacter.Items[i] = serviceItems[i];
            app.SelectedCharacter.Health = serviceHealth;
            serviceItems = null;
        }
        if (name is not ("menu" or "menu-mouse" or "setup-notes" or "setup-versus-original" or "setup-credits" or "options") && !gameCreated)
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
            case "menu-mouse":
                app.LastInputMode = InputMode.Mouse;
                break;
            case "setup-notes":
                // Exercise physical shortcuts, not just their semantic action.
                app.DeviceManager.VKeyPress(SystemKey.F1);
                app.Process(0);
                break;
            case "setup-versus-original":
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
            case "map-return": app.SetScene("MapScene"); break;
            case "location":
                Press(InputAction.Back); // Close the field manual before leaving the map.
                app.SetScene("LocationScene");
                break;
            case "inventory":
            case "room":
                app.InventoryBackground = name == "room" ? 0 : -1;
                app.InventoryRoom = name == "room" ? app.Game.World.ActiveLocationObj.Rooms[0] : null;
                app.SetScene("InventoryScene", app.SelectedCharacter);
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
}
