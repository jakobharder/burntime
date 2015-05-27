
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
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;

namespace Burntime.Classic.Scenes
{
    class ChurchScene : Scene
    {
        GuiFont font;
        int txtoffset;
        float txtline;
        int txtlines;

        public ChurchScene(Module app)
            : base(app)
        {
            Music = "14_MUS 14_HSC.ogg";
            Position = (app.Engine.GameResolution - new Vector2(320, 200)) / 2;

            Image ani = new Image(app);
            ani.Position = new Vector2(200, 117);
            ani.Background = "film_08.ani??p";
            ani.Background.Animation.Speed = 4.5f;
            ani.Background.Animation.IntervalMargin = 1.0f;
            ani.Background.Animation.Progressive = false;
            Windows += ani;

            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(72, 72, 76));

            CaptureAllMouseClicks = true;
        }

        protected override void OnActivateScene(object parameter)
        {
            BurntimeClassic game = app as BurntimeClassic;
            Background = "film_08.pac";
            app.RenderMouse = false;

            txtlines = 9;
            txtline = 0;
            txtoffset = 590;
        }

        public override bool OnMouseClick(Vector2 position, MouseButton button)
        {
            app.SceneManager.PreviousScene();

            return base.OnMouseClick(position, button);
        }

        public override bool OnVKeyPress(Keys key)
        {
            app.SceneManager.PreviousScene();

            return true;
        }

        protected override void OnInactivateScene()
        {
            app.RenderMouse = true;
        }

        public override void OnUpdate(float elapsed)
        {
            base.OnUpdate(elapsed);

            if (txtlines != 0)
            {
                txtline += elapsed * 0.25f;
                if (txtline >= txtlines)
                {
                    txtlines = 0;
                }
            }
        }

        public override void OnRender(RenderTarget target)
        {
            base.OnRender(target);

            if (txtlines != 0)
            {
                TextHelper txt = new TextHelper(app, "burn");
                String line = txt[txtoffset + (int)txtline];
                font.DrawText(target, new Vector2(160, 200 - 15), line, TextAlignment.Center, VerticalTextAlignment.Top);
            }
        }
    }
}
