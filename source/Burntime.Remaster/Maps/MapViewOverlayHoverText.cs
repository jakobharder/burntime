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
    public Character? Character { get; init; }

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
        Character = obj as Character;
    }
}

class MapViewOverlayHoverText : IMapViewOverlay
{
    const int CounterFrameCount = 15;
    const int FoodTrapOverlap = 4;
    const int CharacterCounterOverlap = FoodTrapOverlap + 2;
    const int TrapWaterOverlap = 5;
    const int RedRowOffset = CounterFrameCount;
    const int TrapRowOffset = CounterFrameCount * 2;

    ClassicGame game;
    Location mapState;
    Player player;
    IResourceManager resMan;
    readonly ISprite[] counterSprites = new ISprite[CounterFrameCount * 3];

    public bool IsVisible { get; set; } = true;
    public bool ShowAllEntrances { get; set; }
    public int HighlightedWorldLocation { get; set; } = -1;

    public MapViewOverlayHoverText(Module App)
    {
        resMan = App.ResourceManager;
        for (int frame = 0; frame < counterSprites.Length; frame++)
        {
            ISprite sprite = resMan.GetImage(
                $"pngsheet@gfx/ui/info_counter.png?{frame}?8x12");
            sprite.Touch();
            counterSprites[frame] = sprite;
        }
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
            else if (mapState.Hover.Character != null)
                DrawCharacterText(textTarget, mapState.Hover,
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

    internal void DrawCharacterText(RenderTarget target, MapViewHoverInfo info, Vector2 offset,
        float alpha)
    {
        Font titleFont = resMan.GetFont(BurntimeClassic.FontName, info.Color);
        Character? character = info.Character;
        bool showHealth = player != null && character?.Player == player;
        bool showExperience = player != null && character != null &&
            (character.Player == null || character.Player == player);
        if (!showHealth && !showExperience)
        {
            titleFont.DrawText(target, info.Position + offset, info.Title, TextAlignment.Center,
                VerticalTextAlignment.Center, alpha);
            return;
        }

        ISprite? healthCounter = showHealth
            ? GetCounterSprite(GetLevel(character!.Health, 7), red: true)
            : null;
        ISprite? experienceCounter = showExperience
            ? counterSprites[TrapRowOffset + GetLevel(character!.Experience, 4)]
            : null;
        int counterWidth = (healthCounter?.Width ?? 0) + (experienceCounter?.Width ?? 0) -
            (healthCounter != null && experienceCounter != null ? CharacterCounterOverlap : 0);
        string title = info.Title + " ";
        int titleWidth = titleFont.GetWidth(title);
        int totalWidth = titleWidth + counterWidth;
        Vector2 position = info.Position + offset - new Vector2(totalWidth / 2, 0);
        position.x = System.Math.Clamp(position.x, 0,
            System.Math.Max(0, target.Size.x - totalWidth));

        titleFont.DrawText(target, position, title, TextAlignment.Left,
            VerticalTextAlignment.Center, alpha);
        position.x += titleWidth;
        if (experienceCounter != null)
        {
            target.DrawSprite(new Vector2(position.x, position.y - experienceCounter.Height / 2),
                experienceCounter, alpha);
            position.x += experienceCounter.Width -
                (healthCounter != null ? CharacterCounterOverlap : 0);
        }
        if (healthCounter != null)
        {
            target.DrawSprite(new Vector2(position.x, position.y - healthCounter.Height / 2),
                healthCounter, alpha);
        }
    }

    static int GetLevel(int value, int levels)
    {
        int percentage = System.Math.Clamp(value, 1, 100);
        return System.Math.Clamp((percentage * levels + 99) / 100, 1, levels);
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

        string prefix = " ";
        int foodPerDay = info.WorldLocation.GetFoodProductionRate().FoodPerDay;
        ISprite? trapIcon = GetTrapIcon(info.WorldLocation);
        ISprite foodCounter = GetCounterSprite(foodPerDay, red: true);
        ISprite waterCounter = GetCounterSprite(info.WorldLocation.Source.Water, red: false);
        bool showFoodCounter = foodPerDay > 0;
        int counterWidth = (showFoodCounter ? foodCounter.Width : 0) +
            (trapIcon?.Width ?? 0) + waterCounter.Width -
            (showFoodCounter && trapIcon != null ? FoodTrapOverlap : 0) -
            (trapIcon != null ? TrapWaterOverlap : 0);
        int titleWidth = titleFont.GetWidth(info.Title + prefix);
        int totalWidth = titleWidth + counterWidth;
        Vector2 position = info.Position + offset - new Vector2(totalWidth / 2, 0);
        position.x = System.Math.Clamp(position.x, 0,
            System.Math.Max(0, target.Size.x - totalWidth));
        titleFont.DrawText(target, position, info.Title + prefix, TextAlignment.Left,
            VerticalTextAlignment.Center, alpha);
        position.x += titleWidth;
        if (showFoodCounter)
        {
            target.DrawSprite(new Vector2(position.x, position.y - foodCounter.Height / 2),
                foodCounter, alpha);
            position.x += foodCounter.Width - (trapIcon != null ? FoodTrapOverlap : 0);
        }
        if (trapIcon != null)
        {
            target.DrawSprite(new Vector2(position.x, position.y - trapIcon.Height / 2),
                trapIcon, alpha);
            position.x += trapIcon.Width - TrapWaterOverlap;
        }
        target.DrawSprite(new Vector2(position.x, position.y - waterCounter.Height / 2),
            waterCounter, alpha);
        position.x += waterCounter.Width;
    }

    internal void DrawEntranceText(RenderTarget target, MapViewHoverInfo info, Vector2 offset, float alpha,
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

        int foodUnits = foodValue / 3;
        ISprite foodCounter = GetCounterSprite(foodUnits, red: true);
        ISprite? trapIcon = GetTrapIcon(mapState, info.Room);
        ISprite waterCounter = GetCounterSprite(mapState.Source.Reserve, red: false);
        bool showFoodCounter = foodUnits > 0;
        bool showWaterCounter = info.Room.IsWaterSource;
        int counterWidth = (showFoodCounter ? foodCounter.Width : 0) +
            (trapIcon?.Width ?? 0) + (showWaterCounter ? waterCounter.Width : 0) -
            (showFoodCounter && trapIcon != null ? FoodTrapOverlap : 0) -
            (trapIcon != null && showWaterCounter ? TrapWaterOverlap : 0);
        string title = info.Title + (counterWidth > 0 ? " " : "");
        int titleWidth = titleFont.GetWidth(title);
        int totalWidth = titleWidth + counterWidth;
        Vector2 position = info.Position + offset - new Vector2(totalWidth / 2, 0);
        position.x = System.Math.Clamp(position.x, 0,
            System.Math.Max(0, target.Size.x - totalWidth));
        titleFont.DrawText(target, position, title, TextAlignment.Left,
            VerticalTextAlignment.Center, alpha);
        position.x += titleWidth;
        if (showFoodCounter)
        {
            target.DrawSprite(new Vector2(position.x, position.y - foodCounter.Height / 2),
                foodCounter, alpha);
            position.x += foodCounter.Width - (trapIcon != null ? FoodTrapOverlap : 0);
        }
        if (trapIcon != null)
        {
            target.DrawSprite(new Vector2(position.x, position.y - trapIcon.Height / 2),
                trapIcon, alpha);
            position.x += trapIcon.Width - (showWaterCounter ? TrapWaterOverlap : 0);
        }
        if (showWaterCounter)
        {
            target.DrawSprite(new Vector2(position.x, position.y - waterCounter.Height / 2),
                waterCounter, alpha);
            position.x += waterCounter.Width;
        }
    }

    ISprite GetCounterSprite(int value, bool red)
    {
        int frame = System.Math.Clamp(value, 0, CounterFrameCount - 1) +
            (red ? RedRowOffset : 0);
        return counterSprites[frame];
    }

    ISprite? GetTrapIcon(Location location, Room? room = null)
    {
        var production = location.Production;
        if (production == null ||
            (room != null && location.GetFoodProductionRate().FoodPerDay <= 0))
            return null;

        foreach (Room candidateRoom in location.Rooms)
        {
            foreach (Item item in candidateRoom.Items)
            {
                if (item.Type.Production != production)
                    continue;

                return room == null || candidateRoom == room
                    ? GetTrapSprite(production)
                    : null;
            }
        }

        if (room == null)
        {
            foreach (Character npc in location.CampNPC)
                foreach (Item item in npc.Items)
                    if (item.Type.Production == production)
                        return GetTrapSprite(production);
        }

        return null;
    }

    ISprite? GetTrapSprite(Production production)
    {
        int icon = production.Produce.ID switch
        {
            "item_maggots" => 1,
            "item_rats" => 2,
            "item_snake" => 3,
            "item_meat" => 4,
            _ => 0
        };
        if (icon == 0)
            return null;

        return counterSprites[TrapRowOffset + icon];
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
