
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
using Burntime.Classic.Logic;

namespace Burntime.Classic
{
    public class MapGuiWindow : Container
    {
        GuiFont font;
        GuiFont playerColor;
        FaceWindow face;
        String name;

        public MapGuiWindow(Module App)
            : base(App)
        {
            Size = app.Engine.GameResolution;

            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            playerColor = new GuiFont(BurntimeClassic.FontName, PixelColor.White);

            Image image = new Image(App);
            image.Background = "munt.raw?1";
            image.Position = new Vector2(Size.x / 2 - 60, Size.y - 40);
            Windows += image;

            image = new Image(App);
            image.Background = "munt.raw?22";
            image.Position = new Vector2(Size.x / 2 - 42, 0);
            Windows += image;

            face = new FaceWindow(App);
            face.Position = new Vector2(Size.x / 2 - 31, Size.y - 56);
            face.FaceID = 0;
            face.DisplayOnly = true;
            face.Layer++;
            Windows += face;
        }

        public override void OnRender(RenderTarget Target)
        {
            base.OnRender(Target);

            Target.RenderRect(new Vector2(0, 0), new Vector2(16, Size.y - 40), new PixelColor(0, 0, 0));
            Target.RenderRect(new Vector2(Size.x - 16, 0), new Vector2(16, Size.y - 40), new PixelColor(0, 0, 0));
            Target.RenderRect(new Vector2(0, Size.y - 40), new Vector2(Size.x, 40), new PixelColor(0, 0, 0));

            Target.Layer++;
            ClassicGame game = app.GameState as ClassicGame;

            Vector2 health = new Vector2(Size.x / 2 + 64, Size.y - 30);
            int fullBar = 75;
            int healthBar = fullBar * game.World.ActivePlayerObj.Character.Health / 100;
            Target.RenderRect(health, new Vector2(healthBar, 5), new PixelColor(240, 64, 56));

            Vector2 timebar = new Vector2(Target.Width / 2 - 30, 2);
            int dayTime = (int)(game.World.Time * 60);
            Target.RenderRect(timebar, new Vector2(dayTime, 3), new PixelColor(240, 64, 56));

            Vector2 name = new Vector2(Size.x / 2 - 97, Size.y - 30);
            playerColor.DrawText(Target, name, this.name, TextAlignment.Center, VerticalTextAlignment.Top);

            Vector2 day = new Vector2(Size.x / 2 + 100, Size.y - 15);
            TextHelper txt = new TextHelper(app, "burn");
            txt.AddArgument("|A", game.World.Day);
            font.DrawText(Target, day, txt[404], TextAlignment.Center, VerticalTextAlignment.Top);
        }

        public void UpdatePlayer()
        {
            ClassicGame game = app.GameState as ClassicGame;
            if (game.World.ActivePlayer == -1)
            {
                face.FaceID = -1;
                name = "";
                return;
            }

            Player player = game.World.Players[game.World.ActivePlayer];

            face.FaceID = player.FaceID;
            name = player.Name;
            playerColor = new GuiFont(BurntimeClassic.FontName, player.Color);
        }
    }
}