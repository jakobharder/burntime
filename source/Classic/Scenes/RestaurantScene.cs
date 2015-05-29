
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
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Classic.GUI;
using Burntime.Classic.Logic;

namespace Burntime.Classic.Scenes
{
    class RestaurantScene : Scene
    {
        InventoryWindow inventory;
        ItemGridWindow grid;
        GuiFont font;
        String[] restaurantText = null;
        int eatLastAmount = 0;
        Image ani;

        public RestaurantScene(Module app)
            : base(app)
        {
            Music = "01_MUS 01_HSC.ogg";
            Position = (app.Engine.GameResolution - new Vector2(320, 200)) / 2;

            BurntimeClassic classic = app as BurntimeClassic;

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
            button.Text = app.ResourceManager.GetString("burn?415");
            button.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            button.HoverFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(144, 160, 212));
            button.Command += OnButtonEat;
            button.SetTextOnly();
            Windows += button;

            grid = new ItemGridWindow(app);
            grid.Position = new Vector2(160, 165);
            grid.Spacing = new Vector2(4, 4);
            grid.Grid = new Vector2(4, 1);
            grid.LeftClickItemEvent += OnLeftClickItemGrid;
            Windows += grid;

            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(212, 212, 212));
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

        void OnButtonEat()
        {
            eatLastAmount = grid.GetEatValue();
            UpdateText();

            BurntimeClassic classic = app as BurntimeClassic;

            classic.Game.World.ActivePlayerObj.Character.Items.Remove(grid);

            classic.SelectedCharacter.GetGroup().Eat(classic.SelectedCharacter, (int)grid.GetEatValue());
            grid.Clear();
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
        }

        void OnLeftClickItemGrid(Framework.States.StateObject state)
        {
            eatLastAmount = -1;

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
