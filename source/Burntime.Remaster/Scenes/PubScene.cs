using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Remaster.GUI;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.Scenes
{
    class PubScene : Scene
    {
        InventoryWindow inventory;
        ItemGridWindow grid;
        GuiFont font;
        String[] restaurantText = null;
        int drinkLastAmount = 0;

        public PubScene(Module app)
            : base(app)
        {
            BurntimeClassic classic = app as BurntimeClassic;
            Background = classic.InventoryBackground == 14 ? "bar.pac" : "pub1.pac";
            Music = "pub";
            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;

            if (classic.InventoryBackground == 14)
            {
                Image ani = new Image(app);
                ani.Position = new Vector2(206, 67);
                ani.Background = "bar.ani??p";
                ani.Background.Animation.IntervalMargin = 6;
                ani.Background.Animation.Progressive = false;
                Windows += ani;
            }
            else
            {
                Image ani = new Image(app);
                ani.Position = new Vector2(201, 22);
                ani.Background = "pub1.ani?0-11?p";
                ani.Background.Animation.Speed = 6.1f;
                ani.Background.Animation.IntervalMargin = 1;
                ani.Background.Animation.Progressive = false;
                Windows += ani;
                ani = new Image(app);
                ani.Position = new Vector2(237, 28);
                ani.Background = "pub1.ani?12-13";
                ani.Background.Animation.Speed = 8;
                ani.Background.Animation.Progressive = false;
                Windows += ani;
                ani = new Image(app);
                ani.Position = new Vector2(231, 55);
                ani.Background = "pub1.ani?14-64?p";
                ani.Background.Animation.Speed = 6.6f;
                ani.Background.Animation.IntervalMargin = 6;
                ani.Background.Animation.Progressive = false;
                Windows += ani;
            }

            inventory = new InventoryWindow(app, InventorySide.Left);
            inventory.Position = new Vector2(2, 5);
            inventory.LeftClickItemEvent += OnLeftClickItemInventory;
            Windows += inventory;

            Button button = new Button(app);
            button.Position = new Vector2(25, 183);
            button.Text = app.ResourceManager.GetString("burn?354");
            button.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            button.HoverFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(144, 160, 212));
            button.Command += OnButtonExit;
            button.SetTextOnly();
            Windows += button;

            button = new Button(app);
            button.Position = new Vector2(116, 183);
            button.Text = app.ResourceManager.GetString("burn?414");
            button.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            button.HoverFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(144, 160, 212));
            button.Command += OnButtonDrink;
            button.SetTextOnly();
            Windows += button;

            grid = new ItemGridWindow(app);
            grid.Position = new Vector2(160, 165);
            grid.Spacing = new Vector2(4, 4);
            grid.Grid = new Vector2(4, 1);
            grid.LeftClickItemEvent += OnLeftClickItemGrid;
            Windows += grid;

            font = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.LightGray);
        }

        public override void OnResizeScreen()
        {
            base.OnResizeScreen();

            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
        }

        protected override void OnActivateScene(object parameter)
        {
            inventory.SetGroup(BurntimeClassic.Instance.SelectedCharacter);

            restaurantText = null;
            drinkLastAmount = -1;
            grid.Clear();
        }

        public override void OnRender(RenderTarget target)
        {
            base.OnRender(target);

            if (restaurantText != null)
            {
                int basex = 157 + 80;
                int basey = 4;
                for (int i = 0; i < 3; i++)
                    font.DrawText(target, new Vector2(basex, basey + 9 * i), restaurantText[i], TextAlignment.Center, VerticalTextAlignment.Top);
            }
        }

        void OnButtonExit()
        {
            // return items
            inventory.ActiveCharacter.GetGroup().MoveItems(grid);

            app.SceneManager.PreviousScene();
        }

        void OnButtonDrink()
        {
            drinkLastAmount = grid.GetDrinkValue();
            UpdateText();

            BurntimeClassic classic = app as BurntimeClassic;

            classic.Game.World.ActivePlayerObj.Character.Items.Remove(grid);

            classic.SelectedCharacter.GetGroup().Drink(BurntimeClassic.Instance.SelectedCharacter, grid.GetDrinkValue());
            grid.Clear();
        }

        void OnLeftClickItemInventory(Framework.States.StateObject state)
        {
            if (!grid.Add(state as Item))
                return;

            drinkLastAmount = -1;
            
            // remove item from group
            inventory.Grid.Remove(state as Item);
            inventory.ActiveCharacter.Items.Remove(state as Item);

            UpdateText();
        }

        void OnLeftClickItemGrid(Framework.States.StateObject state)
        {
            drinkLastAmount = -1;

            // return item to group
            inventory.Grid.Add(state as Item);
            inventory.ActiveCharacter.Items.Add(state as Item);

            grid.Remove(state as Item);
            UpdateText();
        }

        void UpdateText()
        {
            restaurantText = new String[3];
            int baseLine = 0;
            int value = grid.GetDrinkValue();

            if (drinkLastAmount == 0)
                baseLine += 6;
            else if (drinkLastAmount > 0)
                baseLine += 3;
            else if (value == 0)
                baseLine += 9;

            TextHelper txt = new TextHelper(app, "burn");
            txt.AddArgument("|E", value);
            restaurantText[0] = txt[550 + baseLine];
            restaurantText[1] = txt[551 + baseLine];
            restaurantText[2] = txt[552 + baseLine];
        }
    }
}
