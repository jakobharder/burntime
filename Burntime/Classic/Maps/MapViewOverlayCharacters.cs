
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

            debugRender = app.Settings["debug"].GetBool("show_path");
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
