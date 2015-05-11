
#region GNU General Public License - Burntime
/*
 *  Burntime
 *  Copyright (C) 2008-2013 Jakob Harder
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
using Burntime.Classic.Logic;
using Burntime.Classic.GUI;

namespace Burntime.Classic
{
    public class MapGuiWindowNew : Container
    {
        GuiFont font;
        GuiFont playerColor;
        Player player;
        ProgressWindow foodField;
        ProgressWindow waterField;
        ProgressWindow nameField;
        ProgressWindow timeField;

        public CommandHandler ShowInventory;
        public CommandHandler SwitchMap;
        public CommandHandler NextTurn;
        public CommandHandler ShowStatistics;

        public MapGuiWindowNew(Module App)
            : base(App)
        {
            Size = app.Engine.GameResolution;

            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            playerColor = new GuiFont(BurntimeClassic.FontName, PixelColor.White);

            // food field
            foodField = new ProgressWindow(app, "gfx/gui_progress.png", "gfx/gui_progress_glass.png");
            foodField.Position = new Vector2(15, Size.y - 15);
            foodField.Color = new PixelColor(240, 64, 56);
            foodField.Progress = 1.0f;
            foodField.Border = 2;//1.5
            foodField.Layer++;
            Windows += foodField;

            // water field
            waterField = new ProgressWindow(app, "gfx/gui_progress.png", "gfx/gui_progress_glass.png");
            waterField.Position = new Vector2(57, Size.y - 15);
            waterField.Color = new PixelColor(120, 132, 184);
            waterField.Progress = 1.0f;
            waterField.Border = 2;//1.5
            waterField.Layer++;
            Windows += waterField;

            // name field
            nameField = new ProgressWindow(app, "gfx/gui_progress_wide.png", "gfx/gui_progress_wide_glass.png");
            nameField.Position = new Vector2(Size.x / 2 - 42, Size.y - 15);
            nameField.Progress = 1.0f;
            nameField.Border = 2;//1.5
            nameField.Layer++;
            Windows += nameField;

            // time field
            timeField = new ProgressWindow(app, "gfx/gui_progress_wide.png", "gfx/gui_progress_wide_glass.png");
            timeField.Position = new Vector2(Size.x - 15 - 84, Size.y - 15);
            timeField.Color = new PixelColor(240, 64, 56);
            timeField.Progress = 1.0f;
            timeField.Border = 2;//1.5
            timeField.Layer++;
            Windows += timeField;

            Button button = new Button(app);
            button.Image = "gfx/button_normal.png";
            button.HoverImage = "gfx/button_hover.png";
            button.Position = new Vector2(2, Size.y - 14);
            button.Command += new CommandHandler(OnShowInventory);
            button.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            button.Text = "I";
            Windows += button;

            button = new Button(app);
            button.Image = "gfx/button_normal.png";
            button.HoverImage = "gfx/button_hover.png";
            button.Position = new Vector2(Size.x / 2 - 42 - 14, Size.y - 14);
            button.Command += new CommandHandler(OnSwitchMap);
            button.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            button.Text = "M";
            Windows += button;

            button = new Button(app);
            button.Image = "gfx/button_normal.png";
            button.HoverImage = "gfx/button_hover.png";
            button.Position = new Vector2(Size.x / 2 + 42 + 2, Size.y - 14);
            button.Command += new CommandHandler(OnShowStatistics);
            button.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            button.Text = "S";
            Windows += button;

            button = new Button(app);
            button.Image = "gfx/button_normal.png";
            button.HoverImage = "gfx/button_hover.png";
            button.Position = new Vector2(Size.x - 14, Size.y - 14);
            button.Command += new CommandHandler(OnNextTurn);
            button.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            button.Text = "T";
            Windows += button;
        }

        public override void OnRender(RenderTarget Target)
        {
            base.OnRender(Target);

            Target.RenderRect(new Vector2(0, Size.y - 16), new Vector2(Size.x, Size.y), new PixelColor(64, 64, 104));

            //Target.Layer++;
            //ClassicGame game = app.GameState as ClassicGame;

            //Vector2 health = new Vector2(Size.x / 2 + 64, Size.y - 30);
            //int fullBar = 75;
            //int healthBar = fullBar * game.World.ActivePlayerObj.Character.Health / 100;
            //Target.RenderRect(health, new Vector2(healthBar, 5), new PixelColor(240, 64, 56));

            //Vector2 timebar = new Vector2(Size.x / 2 - 30, 2);
            //int dayTime = (int)(game.World.Time * 60);
            //Target.RenderRect(timebar, new Vector2(dayTime, 3), new PixelColor(240, 64, 56));

            //Vector2 name = new Vector2(Size.x / 2 - 97, Size.y - 30);
            //playerColor.DrawText(Target, name, this.name, TextAlignment.Center, VerticalTextAlignment.Top);

            //Vector2 day = new Vector2(Size.x / 2 + 100, Size.y - 15);
            //TextHelper txt = new TextHelper(app, "burn");
            //txt.AddArgument("|A", game.World.Day);
            //font.DrawText(Target, day, txt[404], TextAlignment.Center, VerticalTextAlignment.Top);
        }

        public override void OnUpdate(float Elapsed)
        {
            base.OnUpdate(Elapsed);

            if (player != null)
            {
                nameField.Progress = player.Character.Health / 100.0f;
                foodField.Progress = player.Character.Food / (float)player.Character.MaxFood;
                foodField.Text = player.Character.Food.ToString();
                waterField.Progress = player.Character.Water / (float)player.Character.MaxWater;
                waterField.Text = player.Character.Water.ToString();
            }

            ClassicGame game = app.GameState as ClassicGame;
            timeField.Progress = game.World.Time;

            TextHelper txt = new TextHelper(app, "burn");
            txt.AddArgument("|A", game.World.Day);
            timeField.Text = txt[404];
        }

        public void UpdatePlayer()
        {
            ClassicGame game = app.GameState as ClassicGame;
            if (game.World.ActivePlayer == -1)
            {
                nameField.Text = "";
                nameField.Progress = 0;
                foodField.Text = "";
                foodField.Progress = 0;
                waterField.Text = "";
                waterField.Progress = 0;
                player = null;
                return;
            }

            player = game.World.Players[game.World.ActivePlayer];

            nameField.Text = player.Name;
            nameField.Color = player.Color;
            playerColor = new GuiFont(BurntimeClassic.FontName, player.Color);
        }

        private void OnShowInventory()
        {
        }

        private void OnSwitchMap()
        {
        }

        private void OnShowStatistics()
        {
        }

        private void OnNextTurn()
        {
        }
    }
}