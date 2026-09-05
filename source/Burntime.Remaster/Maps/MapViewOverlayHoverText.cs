using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.Maps;

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
    Player player;
    IResourceManager resMan;

    public bool IsVisible { get; set; } = true;
    public bool ShowAllEntrances { get; set; }

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
        player = world.CurrentPlayer as Player;
    }

    public void RenderOverlay(RenderTarget Target, Vector2 Offset, Vector2 Size)
    {
        if (!IsVisible)
            return;

        const int topMargin = 8;

        var textTarget = Target.GetSubBuffer(new Rect(0, topMargin, Target.Width, Target.Height - topMargin));

        if (mapState != null && mapState.Hover != null)
        {
            Font font = resMan.GetFont(BurntimeClassic.FontName, mapState.Hover.Color);
            font.DrawText(textTarget, mapState.Hover.Position + Offset - new Vector2(0, topMargin), mapState.Hover.Title, TextAlignment.Center);
        }

        if (ShowAllEntrances && mapState != null)
        {
            int entranceCount = System.Math.Min(mapState.Map.Entrances.Length, mapState.Rooms.Count);
            bool entrancesBlocked = player != null && mapState.AreEntrancesBlockedFor(player);
            for (int i = 0; i < entranceCount; i++)
            {
                var entrance = mapState.Map.Entrances[i];
                MapViewHoverInfo info = entrancesBlocked
                    ? new MapViewHoverInfo(resMan.GetString("newburn?103"), entrance.Area.Center,
                        BurntimeClassic.LightGray)
                    : new MapViewHoverInfo(mapState.Rooms[i], resMan, BurntimeClassic.LightGray);
                Font font = resMan.GetFont(BurntimeClassic.FontName, info.Color);
                font.DrawText(textTarget, info.Position + Offset - new Vector2(0, topMargin),
                    info.Title, TextAlignment.Center);
            }
        }
    }

    public IMapObject GetObjectAt(Vector2 position)
    {
        return null;
    }
}
