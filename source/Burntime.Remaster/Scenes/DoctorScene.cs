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
        readonly InputPromptOverlay promptOverlay;
        readonly InputPromptOverlay exitPromptOverlay;
        readonly InputPromptOverlay actionPromptOverlay;
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
            exitButton.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            exitButton.HoverFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(144, 160, 212));
            exitButton.Command += OnButtonExit;
            exitButton.SetTextOnly();
            Windows += exitButton;

            actionButton = new Button(app);
            actionButton.Position = new Vector2(116, 183);
            actionButton.Text = app.ResourceManager.GetString("burn?369");
            actionButton.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            actionButton.HoverFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(144, 160, 212));
            actionButton.Command += OnButtonHeal;
            actionButton.SetTextOnly();
            Windows += actionButton;

            grid = new ItemGridWindow(app);
            grid.Position = new Vector2(160, 165);
            grid.Spacing = new Vector2(4, 4);
            grid.Grid = new Vector2(4, 1);
            grid.LeftClickItemEvent += OnLeftClickItemGrid;
            Windows += grid;

            font = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.LightGray);
            keyboardNavigation = new InventoryKeyboardNavigation(inventory, grid, OnButtonHeal, OnButtonExit);
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
            if (keyboardNavigation.CanMoveSelectedItem(item => item.HealValue != 0))
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
            inventory.SetGroup(BurntimeClassic.Instance.SelectedCharacter);
            doctorText = null;
            grid.Clear();
            keyboardNavigation.Reset();
            UpdatePromptOverlay();
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
            int value = grid.GetHealValue();

            BurntimeClassic classic = app as BurntimeClassic;

            inventory.ActiveCharacter.Health += value;

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
            int value = grid.GetHealValue();

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
