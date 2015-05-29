
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
using Burntime.Framework.Event;
using Burntime.Framework.GUI;
using Burntime.Classic;
using Burntime.Data.BurnGfx;
using Burntime.Classic.Logic;
using Burntime.Classic.Maps;

namespace Burntime.Classic.GUI
{
    public interface IMapEntranceHandler
    {
        String GetEntranceTitle(int Number);
        bool OnClickEntrance(int Number, MouseButton Button);
    }

    public class MapScrollArgs : EventArgs
    {
        public MapScrollArgs(Vector2 offset)
        {
            this.offset = offset;
        }

        Vector2 offset;
        public Vector2 Offset
        {
            get { return offset; }
        }
    }

    public class ObjectArgs : EventArgs
    {
        public ObjectArgs(IMapObject obj, Vector2 position, MouseButton button)
        {
            this.obj = obj;
            this.button = button;
            this.position = position;
        }

        IMapObject obj;
        public IMapObject Object
        {
            get { return obj; }
        }

        MouseButton button;
        public MouseButton Button
        {
            get { return button; }
        }

        Vector2 position;
        public Vector2 Position
        {
            get { return position; }
        }
    }

    public class MapView : Window
    {
        public event EventHandler<ObjectArgs> ClickObject;
        public event EventHandler<MapScrollArgs> Scroll;

        Vector2 mousePosition = new Vector2();
        ParticleEngine particles = new ParticleEngine();
        protected bool enabled;

        List<Maps.IMapViewOverlay> overlays = new List<Maps.IMapViewOverlay>();
        public List<Maps.IMapViewOverlay> Overlays
        {
            get { return overlays; }
            set { overlays = value; }
        }

        IMapEntranceHandler handler;
        GuiFont font;
        MouseClickEvent mouseClickEvent;
        public MouseClickEvent MouseClickEvent
        {
            get { return mouseClickEvent; }
            set { mouseClickEvent = value; }
        }

        public ParticleEngine Particles
        {
            get { return particles; }
        }

        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; }
        }

        public MapView(IMapEntranceHandler Handler, Module App)
            : base(App)
        {
            enabled = true;
            handler = Handler;
            CaptureAllMouseMove = true;

            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(212, 212, 212));

            BurntimeClassic classic = app as BurntimeClassic;
            DebugView = classic.Settings["debug"].GetBool("show_routes") && classic.Settings["debug"].GetBool("enable_cheats");
        }

        MapData map;
        public MapData Map
        {
            get { return map; }
            set { map = value; }
        }

        WayData ways;
        public WayData Ways
        {
            get { return ways; }
            set { ways = value; }
        }

        Location location;
        public Location Location
        {
            get { return location; }
            set { location = value; }
        }

        Classic.Logic.Player player;
        public Classic.Logic.Player Player
        {
            get { return player; }
            set { player = value; }
        }

        public bool DebugView = false;

        Vector2f position = new Vector2f();
        public Vector2 ScrollPosition
        {
            get { return new Vector2(position); }
            set { position = value; }
        }

        float scrollSpeed = 70;

        Vector2f border = new Vector2f();
        int entrance = -1;

        public int ActiveEntrance
        {
            get { return entrance; }
        }

        public override void OnRender(RenderTarget Target)
        {
            base.OnRender(Target);

            if (map != null)
            {
                Vector2 offset = ScrollPosition;

                Rect visible = new Rect(-offset / map.TileSize, Size / map.TileSize + 2);
                visible = visible.Intersect(new Rect(0, 0, map.Width, map.Height));

                foreach (Vector2 pos in visible)
                {
                    Vector2 trans = pos * map.TileSize + offset;
                    Target.DrawSprite(trans, map[pos.x, pos.y].Image);
                }

                Target.Layer++;

                if (DebugView)
                {
                    foreach (Vector2 pos in new Rect(0, 0, map.Mask.Width, map.Mask.Height))
                    {
                        if (!map.Mask[pos])
                            Target.RenderRect(offset + pos * map.Mask.Resolution, Vector2.One * map.Mask.Resolution, new PixelColor(50, 255, 0, 0));
                    }
                    for (int i = 0; i < map.Entrances.Length; i++)
                        Target.RenderRect(map.Entrances[i].Area.Position + offset, map.Entrances[i].Area.Size, new PixelColor(50, 0, 0, 255));
                }

                List<int> renderWays = new List<int>();

                int e = entrance;
                if (e != -1)
                {
                    if (DebugView && ways != null)
                    {
                        foreach (int way in ways.Cross[e].Ways)
                            renderWays.Add(way);
                    }
                }

                foreach (int way in renderWays)
                {
                    if (ways.Ways[way].Images.Length > 0)
                    {
                        Vector2 pos = ways.Ways[way].Position + offset;

                        foreach (Sprite sprite in ways.Ways[way].Images)
                        {
                            Target.DrawSprite(pos, sprite);
                            pos.x += 32;
                        }
                    }
                }

                Target.Layer++;

                int old = Target.Layer;
                Target.Layer += 30;

                foreach (Maps.IMapViewOverlay overlay in overlays)
                {
                    Target.Layer++;
                    overlay.RenderOverlay(Target, offset, Size);
                }

                Target.Layer++;
                Target.Offset += ScrollPosition;
                particles.Render(Target);
                Target.Offset -= ScrollPosition;

                Target.Layer = old;
            }
        }

        public override bool OnMouseMove(Vector2 Position)
        {
            if (!enabled)
                return false;

            const int Margin = 4;
            const int BigMargin = 10;

            border.x = 0;
            border.y = 0;

            border.x += (Position.x < Margin) ? 1 : 0;
            border.x -= (Position.x > Size.x - Margin) ? 1 : 0;
            border.x += (Position.x < BigMargin) ? 1 : 0;
            border.x -= (Position.x > Size.x - BigMargin) ? 1 : 0;
            border.y += (Position.y < Margin) ? 1 : 0;
            border.y -= (Position.y > Size.y - Margin) ? 1 : 0;
            border.y += (Position.y < BigMargin) ? 1 : 0;
            border.y -= (Position.y > Size.y - BigMargin) ? 1 : 0;

            bool found = false;
            for (int i = 0; i < map.Entrances.Length; i++)
            {
                if (map.Entrances[i].Area.PointInside(Position - (Vector2)position))
                {
                    entrance = i;
                    found = true;
                }
            }
            if (!found)
                entrance = -1;

            mousePosition = Position - ScrollPosition;

            return true;
        }

        public override bool OnMouseClick(Vector2 position, MouseButton button)
        {
            if (!enabled)
                return false;

            if (entrance != -1 && handler != null)
                if (handler.OnClickEntrance(entrance, button))
                    return true;

            IMapObject mostTopObj = null;
            foreach (Maps.IMapViewOverlay overlay in overlays)
            {
                IMapObject obj = overlay.GetObjectAt(position - ScrollPosition);
                if (obj != null)
                    mostTopObj = obj;
            }

            if (mostTopObj != null)
            {
                if (ClickObject != null)
                    ClickObject.Invoke(this, new ObjectArgs(mostTopObj, position, button));
                return true;
            }

            if (mouseClickEvent != null && button == MouseButton.Left)
            {
                mouseClickEvent.Execute(position - ScrollPosition, button);
                return true;
            }

            return false;
        }

        public override void OnUpdate(float Elapsed)
        {
            position += border * Elapsed * scrollSpeed;
            if (border != Vector2f.Zero && Scroll != null)
                Scroll.Invoke(this, new MapScrollArgs(position));

            position.ThresholdGT(0);
            position.ThresholdLT(Boundings.Size - map.TileSize * map.Size);

            if (handler != null)
            {
                ClassicGame game = app.GameState as ClassicGame;
                game.World.ActiveLocationObj.Hover = null;

                foreach (Maps.IMapViewOverlay overlay in overlays)
                {
                    overlay.MouseMoveOverlay(mousePosition);
                    overlay.UpdateOverlay(game, Elapsed);
                }

                if (entrance != -1)
                {
                    if (game.MainMapView)
                    {
                        Burntime.Data.BurnGfx.MapEntrance e = game.World.Map.Entrances[entrance];
                        game.World.ActiveLocationObj.Hover = new MapViewHoverInfo(app.ResourceManager.GetString("burn?" + entrance), e.Area.Center, new PixelColor(212, 212, 212));
                    }
                    else if (entrance < game.World.ActiveLocationObj.Rooms.Count)
                    {
                        game.World.ActiveLocationObj.Hover = new MapViewHoverInfo(game.World.ActiveLocationObj.Rooms[entrance], app.ResourceManager, new PixelColor(212, 212, 212));
                    }
                }
            }

            particles.Update(Elapsed);
        }

        public void CenterTo(Vector2 centerTo)
        {
            position = -centerTo + (Boundings.Size / 2);

            OnUpdate(0);
        }
    }
}
