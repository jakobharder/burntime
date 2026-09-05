using System;
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
    public Room Room { get; init; }
    public Location WorldLocation { get; init; }

    public MapViewHoverInfo(String title, Vector2 position, PixelColor color, Room room = null)
    {
        Title = title;
        Position = new Vector2(position.x, position.y - 9);
        Color = color;
        Room = room;
    }

    public MapViewHoverInfo(IMapObject obj, IResourceManager manager, PixelColor color)
    {
        Title = obj.GetTitle(manager);
        Position = new Vector2(obj.MapArea.Left + obj.MapArea.Width / 2, obj.MapArea.Top - 10);
        Color = color;
        Room = obj as Room;
    }
}

class MapViewOverlayHoverText : IMapViewOverlay
{
    ClassicGame game;
    Location mapState;
    Player player;
    IResourceManager resMan;

    public bool IsVisible { get; set; } = true;
    public bool ShowAllEntrances { get; set; }
    public int HighlightedWorldLocation { get; set; } = -1;

    public MapViewOverlayHoverText(Module App)
    {
        resMan = App.ResourceManager;
    }

    public void MouseMoveOverlay(Vector2 Position)
    {
    }

    public void UpdateOverlay(WorldState world, float elapsed)
    {
        game = world as ClassicGame;
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
            if (mapState.Hover.WorldLocation != null)
                DrawWorldLocationText(textTarget, mapState.Hover,
                    Offset - new Vector2(0, topMargin), 1);
            else
                DrawEntranceText(textTarget, mapState.Hover, Offset - new Vector2(0, topMargin), 1,
                    showInventoryHint: mapState.Player == player);
        }

        if (ShowAllEntrances && game?.MainMapView == true)
        {
            for (int i = 0; i < game.World.Locations.Count && i < game.World.Map.Entrances.Length; i++)
            {
                Location location = game.World.Locations[i];
                if (location.Player != player || i == HighlightedWorldLocation ||
                    mapState?.Hover?.WorldLocation == location)
                    continue;

                var entrance = game.World.Map.Entrances[i];
                if (!IsInViewport(entrance.Area, Offset, Size))
                    continue;

                var info = new MapViewHoverInfo(resMan.GetString(entrance.TitleId),
                    entrance.Area.Center, BurntimeClassic.LightGray)
                {
                    WorldLocation = location
                };
                DrawWorldLocationText(textTarget, info, Offset - new Vector2(0, topMargin), 0.7f);
            }
        }
        else if (ShowAllEntrances && mapState != null)
        {
            int entranceCount = System.Math.Min(mapState.Map.Entrances.Length, mapState.Rooms.Count);
            bool entrancesBlocked = player != null && mapState.AreEntrancesBlockedFor(player);
            for (int i = 0; i < entranceCount; i++)
            {
                Room room = mapState.Rooms[i];
                if (mapState.Hover?.Room == room)
                    continue;

                var entrance = mapState.Map.Entrances[i];
                if (!IsInViewport(entrance.Area, Offset, Size))
                    continue;

                MapViewHoverInfo info = entrancesBlocked
                    ? new MapViewHoverInfo(resMan.GetString("newburn?103"), entrance.Area.Center,
                        BurntimeClassic.LightGray, room)
                    : new MapViewHoverInfo(room, resMan, BurntimeClassic.LightGray);
                DrawEntranceText(textTarget, info, Offset - new Vector2(0, topMargin), 0.7f,
                    showInventoryHint: false);
            }
        }
    }

    internal void DrawWorldLocationText(RenderTarget target, MapViewHoverInfo info, Vector2 offset,
        float alpha)
    {
        Font titleFont = resMan.GetFont(BurntimeClassic.FontName, info.Color);
        if (info.WorldLocation?.Player != player)
        {
            titleFont.DrawText(target, info.Position + offset, info.Title, TextAlignment.Center,
                VerticalTextAlignment.Center, alpha);
            return;
        }

        string prefix = " (";
        string foodText = info.WorldLocation.GetFoodProductionRate().FoodPerDay.ToString();
        string waterText = " " + info.WorldLocation.Source.Water;
        const string closingBracket = ")";
        Font foodFont = resMan.GetFont(BurntimeClassic.FontName, new PixelColor(240, 64, 56));
        Font waterFont = resMan.GetFont(BurntimeClassic.FontName, new PixelColor(128, 136, 192));

        int titleWidth = titleFont.GetWidth(info.Title + prefix);
        int totalWidth = titleWidth + foodFont.GetWidth(foodText) +
            waterFont.GetWidth(waterText) + titleFont.GetWidth(closingBracket);
        Vector2 position = info.Position + offset - new Vector2(totalWidth / 2, 0);
        position.x = System.Math.Clamp(position.x, 0,
            System.Math.Max(0, target.Size.x - totalWidth));
        titleFont.DrawText(target, position, info.Title + prefix, TextAlignment.Left,
            VerticalTextAlignment.Center, alpha);
        position.x += titleWidth;
        foodFont.DrawText(target, position, foodText, TextAlignment.Left,
            VerticalTextAlignment.Center, alpha);
        position.x += foodFont.GetWidth(foodText);
        waterFont.DrawText(target, position, waterText, TextAlignment.Left,
            VerticalTextAlignment.Center, alpha);
        position.x += waterFont.GetWidth(waterText);
        titleFont.DrawText(target, position, closingBracket, TextAlignment.Left,
            VerticalTextAlignment.Center, alpha);
    }

    void DrawEntranceText(RenderTarget target, MapViewHoverInfo info, Vector2 offset, float alpha,
        bool showInventoryHint)
    {
        Font titleFont = resMan.GetFont(BurntimeClassic.FontName, info.Color);
        if (!showInventoryHint || info.Room == null)
        {
            titleFont.DrawText(target, info.Position + offset, info.Title, TextAlignment.Center,
                VerticalTextAlignment.Center, alpha);
            return;
        }

        int foodValue = 0;
        foreach (Item item in info.Room.Items)
            if (item.FoodValue > 0)
                foodValue += item.FoodValue;

        string itemText = " (" + info.Room.Items.Count;
        string foodText = foodValue > 0 ? " " + foodValue : "";
        string waterText = info.Room.IsWaterSource ? " " + mapState.Source.Reserve : "";
        const string closingBracket = ")";
        Font itemFont = resMan.GetFont(BurntimeClassic.FontName, info.Color);
        Font foodFont = resMan.GetFont(BurntimeClassic.FontName, new PixelColor(240, 64, 56));
        Font waterFont = resMan.GetFont(BurntimeClassic.FontName, new PixelColor(128, 136, 192));
        int titleWidth = titleFont.GetWidth(info.Title);
        int totalWidth = titleWidth + itemFont.GetWidth(itemText) + foodFont.GetWidth(foodText) +
            waterFont.GetWidth(waterText) + itemFont.GetWidth(closingBracket);
        Vector2 position = info.Position + offset - new Vector2(totalWidth / 2, 0);
        position.x = System.Math.Clamp(position.x, 0,
            System.Math.Max(0, target.Size.x - totalWidth));
        titleFont.DrawText(target, position, info.Title, TextAlignment.Left,
            VerticalTextAlignment.Center, alpha);
        position.x += titleWidth;
        itemFont.DrawText(target, position, itemText, TextAlignment.Left,
            VerticalTextAlignment.Center, alpha);
        position.x += itemFont.GetWidth(itemText);
        foodFont.DrawText(target, position, foodText, TextAlignment.Left,
            VerticalTextAlignment.Center, alpha);
        position.x += foodFont.GetWidth(foodText);
        waterFont.DrawText(target, position, waterText, TextAlignment.Left,
            VerticalTextAlignment.Center, alpha);
        position.x += waterFont.GetWidth(waterText);
        itemFont.DrawText(target, position, closingBracket, TextAlignment.Left,
            VerticalTextAlignment.Center, alpha);
    }

    static bool IsInViewport(Rect area, Vector2 offset, Vector2 size)
    {
        Rect visiblePart = (area + offset).Intersect(new Rect(Vector2.Zero, size));
        return visiblePart.Width > 0 && visiblePart.Height > 0;
    }

    public IMapObject GetObjectAt(Vector2 position)
    {
        return null;
    }
}
