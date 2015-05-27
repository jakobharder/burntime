
#region GNU General Public License - Burntime
/*
 *  Burntime
 *  Copyright (C) 2008-2011 Jakob Harder
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
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
    class DoctorScene : Scene
    {
        InventoryWindow inventory;
        ItemGridWindow grid;
        GuiFont font;
        String[] doctorText = null;

        public DoctorScene(Module app)
            : base(app)
        {
            Background = "arzt.pac";
            Music = "03_MUS 03_HSC.ogg";
            Position = (app.Engine.GameResolution - new Vector2(320, 200)) / 2;
            
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
            button.Text = app.ResourceManager.GetString("burn?369");
            button.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            button.HoverFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(144, 160, 212));
            button.Command += OnButtonHeal;
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
            inventory.SetGroup(BurntimeClassic.Instance.SelectedCharacter);
            doctorText = null;
        }

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

            //UpdateText();
        }

        void OnLeftClickItemGrid(Framework.States.StateObject state)
        {

            // return item to group
            inventory.Grid.Add(state as Item);
            inventory.ActiveCharacter.Items.Add(state as Item);

            grid.Remove(state as Item);
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
