
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
using Burntime.Framework.Network;

namespace Burntime.Classic.Scenes
{
    class PauseScene : Scene
    {
        GuiFont font;
        bool handled = false;

        public PauseScene(Module App)
            : base(App)
        {
            Size = new Burntime.Platform.Vector2(320, 200);
            Position = (app.Engine.GameResolution - new Vector2(320, 200)) / 2;

            //Background = new SpriteImage(App, "blz.pac");
            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(255, 255, 255));
            CaptureAllMouseClicks = true;
        }

        public override void OnRender(RenderTarget Target)
        {
            font.DrawText(Target, new Vector2(160, 100), new GuiString("@newburn?11"), TextAlignment.Center, VerticalTextAlignment.Center);
        }

        public override bool OnMouseClick(Vector2 Position, MouseButton Button)
        {
            if (!handled)
            {
                app.SceneManager.PreviousScene();
                handled = true;
            }
            return true;
        }

        public override bool OnKeyPress(char Key)
        {
            if (!handled)
            {
                app.SceneManager.PreviousScene();
                handled = true;
            }
            return true;
        }

        public override bool OnVKeyPress(Keys Key)
        {
            if (!handled)
            {
                app.SceneManager.PreviousScene();
                handled = true;
            }
            return true;
        }

        protected override void OnActivateScene(object parameter)
        {
            app.RenderMouse = false;
            handled = false;
        }

        protected override void OnInactivateScene()
        {
            app.RenderMouse = true;
        }
    }
}
