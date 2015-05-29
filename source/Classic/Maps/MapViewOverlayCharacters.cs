
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
using Burntime.Framework.States;
using Burntime.Classic.Logic;

namespace Burntime.Classic.Maps
{
    class MapViewOverlayCharacters : IMapViewOverlay
    {
        Location mapState;
        Player player;
        Module app;
        SpriteAnimation ani;
        bool debugRender;

        //Character selectedCharacter;
        public Character SelectedCharacter
        {
            get { return BurntimeClassic.Instance.SelectedCharacter; }
            set { BurntimeClassic.Instance.SelectedCharacter = value; }
        }

        public bool IsVisible
        {
            get { return true; }
            set { }
        }

        public MapViewOverlayCharacters(Module App)
        {
            app = App;

            ani = new SpriteAnimation(2);
            ani.Speed = 10.0f;

            debugRender = app.Settings["debug"].GetBool("show_path") && app.Settings["debug"].GetBool("enable_cheats");
        }

        public void MouseMoveOverlay(Vector2 position)
        {
            if (mapState == null)
                return;

            Character hoveredChar = (Character)GetObjectAt(position);
            if (hoveredChar != null)
            {
                PixelColor color;
                if (hoveredChar.Player != null)
                {
                    if (hoveredChar.Player.Group.Contains(hoveredChar))
                        color = hoveredChar.Player.Color;
                    else
                        color = hoveredChar.Player.ColorDark;
                }
                else
                {
                    color = new PixelColor(252, 220, 0);
                }

                mapState.Hover = new MapViewHoverInfo(hoveredChar, app.ResourceManager, color);
            }
        }

        public void UpdateOverlay(WorldState world, float elapsed)
        {
            mapState = world.CurrentLocation as Location;
            player = world.CurrentPlayer as Player;

            ani.Update(elapsed);
        }

        public void RenderOverlay(RenderTarget target, Vector2 offset, Vector2 size)
        {
            if (mapState != null)
            {
                List<Character> characters = new List<Character>();

                foreach (Character chr in mapState.Characters)
                    characters.Add(chr);

                if (player != null)
                {
                    for (int i = 0; i < player.Group.Count; i++)
                        characters.Add(player.Group[i]);
                }

                foreach (Character chr in characters)
                {
                    // skip if dead
                    if (chr.IsDead && chr.DeadAnimationFinished ||
                        chr.IsPlayerCharacter && chr.Player.IsDead)
                    {
                        continue;
                    }

                    Vector2 pos = chr.Position + offset - Vector2.One * 8;

                    if (!chr.IsDead && SelectedCharacter == chr && chr.Animation == 0)
                        chr.Body.Object.Animation.Frame = ani.Frame;
                    else
                        chr.Body.Object.Animation.Frame = chr.Animation;
                    target.DrawSprite(pos, chr.Body);

                    if (debugRender)
                    {
                        RenderTarget lineTarget = target.GetSubBuffer(new Rect(Vector2.Zero, target.Size));
                        lineTarget.Offset = offset;
                        chr.Path.DebugRender(lineTarget);
                    }
                }
            }
        }

        public IMapObject GetObjectAt(Vector2 position)
        {
            if (mapState == null)
                return null;

            IMapObject obj = null;

            foreach (Character chr in mapState.Characters)
            {
                if (chr.IsDead)
                    continue;

                Vector2 pos = chr.Position - position;

                if (pos.Length < 10)
                    obj = chr;
            }

            if (player != null)
            {
                for (int i = 0; i < player.Group.Count; i++)
                {
                    Vector2 pos = player.Group[i].Position - position;
                    
                    if (pos.Length < 10)
                        obj = player.Group[i];
                }
            }

            return obj;
        }
    }
}
