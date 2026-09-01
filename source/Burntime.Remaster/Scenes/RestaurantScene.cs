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
        readonly InputPromptOverlay promptOverlay;
        readonly InputPromptOverlay exitPromptOverlay;
        readonly InputPromptOverlay actionPromptOverlay;
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
            Windows += grid;

            font = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.LightGray);
            keyboardNavigation = new InventoryKeyboardNavigation(inventory, grid, OnButtonEat, OnButtonExit);
            Windows += promptOverlay = new InputPromptOverlay(app);
            promptOverlay.AnchorToScreenBottomRight();
            Windows += exitPromptOverlay = CreateInlinePrompt(InputAction.Back);
            Windows += actionPromptOverlay = CreateInlinePrompt(InputAction.SceneAction);
            UpdateInlinePromptPositions();
        }

        public override void OnResizeScreen()
        {
            base.OnResizeScreen();

            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
            promptOverlay.AnchorToScreenBottomRight();
            UpdateInlinePromptPositions();
        }

        InputPromptOverlay CreateInlinePrompt(InputAction action)
        {
            InputPromptOverlay prompt = new(app)
            {
                HorizontalAlignment = PositionAlignment.Left,
                VerticalAlignment = PositionAlignment.Left
            };
            prompt.SetPrompts(new InputPrompt(action, ""));
            return prompt;
        }

        void UpdateInlinePromptPositions()
        {
            exitPromptOverlay.Position = new Vector2(exitButton.Boundings.Right + 2, 181);
            actionPromptOverlay.Position = new Vector2(actionButton.Boundings.Right + 2, 181);
        }

        public override void OnUpdate(float elapsed)
        {
            base.OnUpdate(elapsed);
            UpdateInlinePromptPositions();
            UpdatePromptOverlay();
        }

        void UpdatePromptOverlay()
        {
            List<InputPrompt> prompts = [];
            if (keyboardNavigation.CanMoveSelectedItem())
                prompts.Add(new(InputAction.Primary,
                    keyboardNavigation.ActiveGrid == grid ? "@prompts?37" : "@prompts?36"));
            if (inventory.ActiveCharacter.GetGroup().Count > 1)
            {
                prompts.Add(new(InputAction.Statistics, "@prompts?16")
                {
                    PreferredKeyboardControl = new Key(SystemKey.Left, ModifierKeys.Shift),
                    PreferredGamepadControl = GamepadControl.LeftShoulder
                });
            }
            promptOverlay.SetPrompts(prompts.ToArray());
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

            eatLastAmount = -1;
            grid.Clear();
            keyboardNavigation.Reset();
            UpdatePromptOverlay();
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
            eatLastAmount = grid.GetEatValue();
            UpdateText();

            BurntimeClassic classic = app as BurntimeClassic;

            classic.Game.World.ActivePlayerObj.Character.Items.Remove(grid);

            classic.SelectedCharacter.GetGroup().Eat(classic.SelectedCharacter, (int)grid.GetEatValue());
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
            int value = grid.GetEatValue();

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
