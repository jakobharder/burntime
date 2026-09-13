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
    class DoctorScene : Scene
    {
        public override bool UseDiagonalGamepadNavigation => true;

        InventoryWindow inventory;
        ItemGridWindow grid;
        GuiFont font;
        String[] doctorText = null;
        InventoryKeyboardNavigation keyboardNavigation;
        readonly Button exitButton;
        readonly Button actionButton;

        public DoctorScene(Module app)
            : base(app)
        {
            Background = "arzt.pac";
            Music = "doctor";
            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;

            Image ani = new Image(app);
            ani.Position = new Vector2(211, 65);
            ani.Background = "arzt.ani??p";
            ani.Background.Animation.Speed = 6.5f;
            ani.Background.Animation.IntervalMargin = 4;
            ani.Background.Animation.Progressive = false;
            Windows += ani;

            inventory = new InventoryWindow(app, InventorySide.Left);
            inventory.Position = new Vector2(2, 5);
            inventory.LeftClickItemEvent += OnLeftClickItemInventory;
            Windows += inventory;

            exitButton = new Button(app);
            exitButton.Position = new Vector2(25, 183);
            exitButton.Text = app.ResourceManager.GetString("burn?354");
            exitButton.Font = new GuiFont(BurntimeClassic.FontName, ClassicColors.HudText);
            exitButton.HoverFont = new GuiFont(BurntimeClassic.FontName, ClassicColors.HudTextHover);
            exitButton.Command += OnButtonExit;
            exitButton.SetTextOnly();
            Windows += exitButton;

            actionButton = new Button(app);
            actionButton.Position = new Vector2(116, 183);
            actionButton.Text = app.ResourceManager.GetString("burn?369");
            actionButton.Font = new GuiFont(BurntimeClassic.FontName, ClassicColors.HudText);
            actionButton.HoverFont = new GuiFont(BurntimeClassic.FontName, ClassicColors.HudTextHover);
            actionButton.Command += OnButtonHeal;
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

            font = new GuiFont(BurntimeClassic.FontName, ClassicColors.LightGray);
            keyboardNavigation = new InventoryKeyboardNavigation(inventory, grid, OnButtonHeal, OnButtonExit);
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
                    : grid.Count < grid.MaxCount && focusedItem.HealValue != 0);
        }

        protected override void OnActivateScene(object parameter)
        {
            inventory.SetGroup(BurntimeClassic.Instance.SelectedCharacter);
            doctorText = null;
            grid.Clear();
            keyboardNavigation.Reset();
        }

        public override bool OnInputAction(InputAction action) => keyboardNavigation.Handle(action);

        public override void OnRender(RenderTarget target)
        {
            base.OnRender(target);

            if (doctorText != null)
            {
                int basex = 157 + 80;
                int basey = 4;
                for (int i = 0; i < 3; i++)
                    font.DrawText(target, new Vector2(basex, basey + 9 * i), doctorText[i], TextAlignment.Center, VerticalTextAlignment.Top);
            }
        }

        void OnButtonExit()
        {
            // return items
            inventory.ActiveCharacter.GetGroup().MoveItems(grid);

            app.SceneManager.PreviousScene();
        }

        void OnButtonHeal()
        {
            BurntimeClassic classic = app as BurntimeClassic;
            inventory.ActiveCharacter.Health = classic.Game.RuleBook.CalculateDoctorResult(
                inventory.ActiveCharacter.Health, grid);

            UpdateText();

            classic.Game.World.ActivePlayerObj.Character.Items.Remove(grid);
            grid.Clear();
            keyboardNavigation.ItemsChanged();
        }

        void OnLeftClickItemInventory(Framework.States.StateObject state)
        {
            if ((state as Item).HealValue == 0)
                return;

            if (!grid.Add(state as Item))
                return;

            // remove item from group
            inventory.Grid.Remove(state as Item);
            inventory.ActiveCharacter.Items.Remove(state as Item);
            keyboardNavigation.ItemsChanged();

            //UpdateText();
        }

        void OnLeftClickItemGrid(Framework.States.StateObject state)
        {
            // return item to group
            if (!inventory.Grid.Add(state as Item))
                return;
            inventory.ActiveCharacter.Items.Add(state as Item);

            grid.Remove(state as Item);
            keyboardNavigation.ItemsChanged();
            //UpdateText();
        }

        void UpdateText()
        {
            doctorText = new String[3];
            int baseLine = 0;
            BurntimeClassic classic = app as BurntimeClassic;
            int value = classic.Game.RuleBook.CalculateDoctorResult(
                inventory.ActiveCharacter.Health, grid) - inventory.ActiveCharacter.Health;

            if (value == 0)
                baseLine = 522;
            else
            {
                if (inventory.ActiveCharacter.Health <= 45)
                    baseLine = 516;
                else if (inventory.ActiveCharacter.Health <= 60)
                    baseLine = 513;
                else if (inventory.ActiveCharacter.Health <= 95)
                    baseLine = 510;
                else
                    baseLine = 519;
            }

            TextHelper txt = new TextHelper(app, "burn");
            doctorText[0] = txt[0 + baseLine];
            doctorText[1] = txt[1 + baseLine];
            doctorText[2] = txt[2 + baseLine];
        }
    }
}
