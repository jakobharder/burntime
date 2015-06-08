
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
using Burntime.Platform.Resource;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Classic.Logic;

namespace Burntime.Classic.Maps
{
    public class MapViewHoverInfo
    {
        PixelColor color;
        String title;
        Vector2 position;

        public String Title
        {
            get { return title; }
        }

        public Vector2 Position
        {
            get { return position; }
            set { position = value; }
        }

        public PixelColor Color
        {
            get { return color; }
        }

        public MapViewHoverInfo(String title, Vector2 position, PixelColor color)
        {
            this.title = title;
            this.position = new Vector2(position);
            this.color = color;
        }

        public MapViewHoverInfo(IMapObject obj, ResourceManager manager, PixelColor color)
        {
            title = obj.GetTitle(manager);
            position = new Vector2(obj.MapArea.Left + obj.MapArea.Width / 2, obj.MapArea.Top - 10);
            this.color = color;
        }
    }

    class MapViewOverlayHoverText : IMapViewOverlay
    {
        Location mapState;
        ResourceManager resMan;
        bool isVisible = true;

        public bool IsVisible
        {
            get { return isVisible; }
            set { isVisible = value; }
        }

        public MapViewOverlayHoverText(Module App)
        {
            resMan = App.ResourceManager;
        }

        public void MouseMoveOverlay(Vector2 Position)
        {
        }

        public void UpdateOverlay(WorldState world, float elapsed)
        {
            mapState = world.CurrentLocation as Location;
        }

        public void RenderOverlay(RenderTarget Target, Vector2 Offset, Vector2 Size)
        {
            if (!isVisible)
                return;

            if (mapState != null && mapState.Hover != null)
            {
                Font font = resMan.GetFont(BurntimeClassic.FontName, mapState.Hover.Color);
                font.DrawText(Target, mapState.Hover.Position + Offset, mapState.Hover.Title, TextAlignment.Center);
            }
        }

        public IMapObject GetObjectAt(Vector2 position)
        {
            return null;
        }
    }
}
