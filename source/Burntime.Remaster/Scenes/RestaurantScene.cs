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
    class RestaurantScene : Scene
    {
        public override bool UseDiagonalGamepadNavigation => true;

        InventoryWindow inventory;
        ItemGridWindow grid;
        GuiFont font;
        String[] restaurantText = null;
        int eatLastAmount = 0;
        Image ani;
        InventoryKeyboardNavigation keyboardNavigation;
        readonly Button exitButton;
        readonly Button actionButton;

        public RestaurantScene(Module app)
            : base(app)
        {
            Music = "diner";
            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;

            BurntimeClassic classic = app as BurntimeClassic;

            inventory = new InventoryWindow(app, InventorySide.Left);
            inventory.Position = new Vector2(2, 5);
            inventory.LeftClickItemEvent += OnLeftClickItemInventory;
            Windows += inventory;

            exitButton = new Button(app);
            exitButton.Position = new Vector2(25, 183);
            exitButton.Text = app.ResourceManager.GetString("burn?354");
            exitButton.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            exitButton.HoverFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(144, 160, 212));
            exitButton.Command += OnButtonExit;
            exitButton.SetTextOnly();
            Windows += exitButton;

            actionButton = new Button(app);
            actionButton.Position = new Vector2(116, 183);
            actionButton.Text = app.ResourceManager.GetString("burn?415");
            actionButton.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            actionButton.HoverFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(144, 160, 212));
            actionButton.Command += OnButtonEat;
            actionButton.SetTextOnly();
            Windows += actionButton;

            grid = new ItemGridWindow(app);
            grid.Position = new Vector2(160, 165);
            grid.Spacing = new Vector2(4, 4);
            grid.Grid = new Vector2(4, 1);
            grid.LeftClickItemEvent += OnLeftClickItemGrid;
            grid.Prompts.Add(InputAction.Primary, "@prompts?37",
                () => CanMoveFocusedItem(grid));
            Windows += grid;

            inventory.Grid.Prompts.Add(InputAction.Primary, "@prompts?36",
                () => CanMoveFocusedItem(inventory.Grid));

            font = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.LightGray);
            keyboardNavigation = new InventoryKeyboardNavigation(inventory, grid, OnButtonEat, OnButtonExit);
            inventory.Grid.MouseFocusChanged += keyboardNavigation.FocusFromMouse;
            grid.MouseFocusChanged += keyboardNavigation.FocusFromMouse;
            Windows += new InputPromptOverlay(app, Prompts,
                InputPromptColorScheme.Hud);
            exitButton.Prompts.Add(InputAction.Back, "",
                new Vector2(exitButton.Size.x + 2, -2));
            actionButton.Prompts.Add(InputAction.Action, "",
                new Vector2(actionButton.Size.x + 2, -2));
            Prompts.Add(InputPattern.HorizontalPaging, "@prompts?16",
                () => inventory.PageCount > 1);
        }

        public override void OnResizeScreen(bool reload = false)
        {
            base.OnResizeScreen(reload);

            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
        }

        bool CanMoveFocusedItem(ItemGridWindow source)
        {
            Item? focusedItem = source.FocusedItem;
            return focusedItem != null &&
                (source == grid
                    ? inventory.Grid.Count < inventory.Grid.MaxCount
                    : grid.Count < grid.MaxCount);
        }

        protected override void OnActivateScene(object parameter)
        {
            BurntimeClassic classic = app as BurntimeClassic;
            inventory.SetGroup(BurntimeClassic.Instance.SelectedCharacter);
           
            Background = classic.InventoryBackground == 22 ? "wirt.pac" : "koch.pac";
            restaurantText = null;

            Windows.Remove(ani);

            if (classic.InventoryBackground != 22)
            {
                ani = new Image(app);
                ani.Position = new Vector2(186, 50);
                ani.Background = "koch.ani??p";
                ani.Background.Animation.Speed = 6.5f;
                ani.Background.Animation.Progressive = false;
                Windows += ani;
            }
            else
            {
                ani = new Image(app);
                ani.Position = new Vector2(202, 51);
                ani.Background = "wirt.ani??p";
                //ani.Background.Animation.Speed = 6.5f;
                ani.Background.Animation.Progressive = false;
                Windows += ani;
            }

            eatLastAmount = -1;
            grid.Clear();
            keyboardNavigation.Reset();
        }

        public override bool OnInputAction(InputAction action) => keyboardNavigation.Handle(action);

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

        void OnButtonEat()
        {
            BurntimeClassic classic = app as BurntimeClassic;
            eatLastAmount = classic.Game.RuleBook.CalculateRestaurantValue(grid);
            UpdateText();

            classic.Game.World.ActivePlayerObj.Character.Items.Remove(grid);

            classic.SelectedCharacter.GetGroup().Eat(classic.SelectedCharacter, eatLastAmount);
            grid.Clear();
            keyboardNavigation.ItemsChanged();
        }

        void OnLeftClickItemInventory(Framework.States.StateObject state)
        {
            if (!grid.Add(state as Item))
                return;

            eatLastAmount = -1;

            // remove item from group
            inventory.Grid.Remove(state as Item);
            inventory.ActiveCharacter.Items.Remove(state as Item);

            UpdateText();
            keyboardNavigation.ItemsChanged();
        }

        void OnLeftClickItemGrid(Framework.States.StateObject state)
        {
            eatLastAmount = -1;

            // return item to group
            if (!inventory.Grid.Add(state as Item))
                return;
            inventory.ActiveCharacter.Items.Add(state as Item);

            grid.Remove(state as Item);
            UpdateText();
            keyboardNavigation.ItemsChanged();
        }

        void UpdateText()
        {
            restaurantText = new String[3];
            int baseLine = 0;
            BurntimeClassic classic = app as BurntimeClassic;
            int value = classic.Game.RuleBook.CalculateRestaurantValue(grid);

            //if (restaurantType == RestaurantType.Water)
            //    baseLine += 20;

            if (eatLastAmount == 0)
                baseLine += 6;
            else if (eatLastAmount > 0)
                baseLine += 3;
            else if (value == 0)
                baseLine += 9;

            TextHelper txt = new TextHelper(app, "burn");
            txt.AddArgument("|E", value);
            restaurantText[0] = txt[530 + baseLine];
            restaurantText[1] = txt[531 + baseLine];
            restaurantText[2] = txt[532 + baseLine];
        }
    }
}
