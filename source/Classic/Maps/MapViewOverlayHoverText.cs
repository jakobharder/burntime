using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Classic.Logic;

namespace Burntime.Classic.Maps;

public class MapViewHoverInfo
{
    public String Title { get; init; }
    public Vector2 Position { get; set; }
    public PixelColor Color { get; init; }

    public MapViewHoverInfo(String title, Vector2 position, PixelColor color)
    {
        Title = title;
        Position = new Vector2(position.x, position.y - 9);
        Color = color;
    }

    public MapViewHoverInfo(IMapObject obj, IResourceManager manager, PixelColor color)
    {
        Title = obj.GetTitle(manager);
        Position = new Vector2(obj.MapArea.Left + obj.MapArea.Width / 2, obj.MapArea.Top - 10);
        Color = color;
    }
}

class MapViewOverlayHoverText : IMapViewOverlay
{
    Location mapState;
    IResourceManager resMan;
    bool isVisible = true;

    public bool IsVisible { get; set; }

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
