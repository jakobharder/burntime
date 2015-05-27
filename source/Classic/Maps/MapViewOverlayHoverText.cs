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
            position = new Vector2(obj.MapPosition);
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
                font.DrawText(Target, mapState.Hover.Position + Offset - new Vector2(0, 15), mapState.Hover.Title, TextAlignment.Center);
            }
        }

        public IMapObject GetObjectAt(Vector2 position)
        {
            return null;
        }
    }
}
