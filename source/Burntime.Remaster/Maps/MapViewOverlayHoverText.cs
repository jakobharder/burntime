using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Remaster.GUI;
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
    ClassicGame game;
    Location mapState;
    Player player;
    IResourceManager resMan;
    readonly GuiTextBars textBars;

    public bool IsVisible { get; set; } = true;
    public bool ShowAllEntrances { get; set; }
    public int HighlightedWorldLocation { get; set; } = -1;

    public MapViewOverlayHoverText(Module App)
    {
        resMan = App.ResourceManager;
        textBars = new GuiTextBars(App);
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
                    Offset - new Vector2(0, topMargin), 1, showOwnershipFlag: true);
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
                    entrance.Area.Center, ClassicColors.LightGray)
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
                        ClassicColors.LightGray, room)
                    : new MapViewHoverInfo(room, resMan, ClassicColors.LightGray);
                DrawEntranceText(textTarget, info, Offset - new Vector2(0, topMargin), 0.7f,
                    showInventoryHint: false);
            }
        }
    }

    internal void DrawCharacterText(RenderTarget target, MapViewHoverInfo info, Vector2 offset,
        float alpha)
    {
        Character? character = info.Character;
        bool showHealth = player != null && character?.Player == player;
        bool showExperience = player != null && character != null &&
            character.Class is not (CharClass.Dog or CharClass.Mutant or CharClass.Trader) &&
            (character.Player == null || character.Player == player);
        List<GuiTextBar> bars = new(2);
        if (showExperience)
            bars.Add(new GuiTextBar(GuiTextBarType.Dots,
                game.RuleBook.GetExperienceTier(character!.Experience) + 1));
        if (showHealth)
            bars.Add(new GuiTextBar(GuiTextBarType.RedBar,
                GetLevel(character!.Health, 7)));

        textBars.Draw(target, info.Position + offset, info.Title, info.Color, alpha, bars);
    }

    static int GetLevel(int value, int levels)
    {
        int percentage = System.Math.Clamp(value, 1, 100);
        return System.Math.Clamp((percentage * levels + 99) / 100, 1, levels);
    }

    internal void DrawWorldLocationText(RenderTarget target, MapViewHoverInfo info, Vector2 offset,
        float alpha, bool showOwnershipFlag = false)
    {
        Player? controllingPlayer = info.WorldLocation?.ControllingPlayer;
        bool isCity = info.WorldLocation?.IsCity == true;
        ISprite? ownershipFlag = showOwnershipFlag && !isCity
            ? controllingPlayer?.Flag.Object
            : null;
        PixelColor locationColor = isCity && controllingPlayer != null
            ? controllingPlayer.Color
            : info.Color;
        int foodPerDay = info.WorldLocation?.GetFoodProductionRate().FoodPerDay ?? 0;
        bool hasVisited = player?.HasVisited(info.WorldLocation) == true;
        bool hasRadio = player != null && RadioIntel.HasRadio(player);
        string? dangerIcon = hasVisited || hasRadio ? info.WorldLocation?.Danger?.Type switch
        {
            "gas" => FontIcons.Toxic,
            "radiation" => FontIcons.Radiation,
            _ => null
        } : null;

        if (info.WorldLocation?.Player != player)
        {
            int baseWater = info.WorldLocation?.Source.BaseWater ?? 0;
            bool showResourceInfo = hasVisited && !info.WorldLocation!.IsCity;
            List<GuiTextBar> radioBars = new(3);
            if (showResourceInfo)
                radioBars.Add(new GuiTextBar(GuiTextBarType.BlueBar, baseWater));
            if (game != null && player != null &&
                RadioIntel.IsAvailable(player, info.WorldLocation!))
            {
                RadioReport report = RadioIntel.Create(game, player, info.WorldLocation!);
                radioBars.Add(new GuiTextBar(GuiTextBarType.Dots, report.Defenders));
                if (report.Threat > 0)
                    radioBars.Add(new GuiTextBar(GuiTextBarType.RedBar, report.Threat));
            }
            textBars.Draw(target, info.Position + offset, info.Title, locationColor, alpha,
                radioBars, dangerIcon,
                ownershipFlag, showBackground: true);
            return;
        }

        List<GuiTextBar> bars = new(4);
        if (foodPerDay > 0)
            bars.Add(new GuiTextBar(GuiTextBarType.RedBar, foodPerDay));
        int npcCount = info.WorldLocation.CampNPC.Count(character =>
            character.Player == player && !character.IsDead);
        if (npcCount > 0)
            bars.Add(new GuiTextBar(GuiTextBarType.Dots, npcCount));
        AddTrapIcons(bars, info.WorldLocation);
        bars.Add(new GuiTextBar(GuiTextBarType.BlueBar, info.WorldLocation.Source.Water));
        textBars.Draw(target, info.Position + offset, info.Title, locationColor, alpha, bars,
            dangerIcon, ownershipFlag, showBackground: true);
    }

    internal void DrawEntranceText(RenderTarget target, MapViewHoverInfo info, Vector2 offset, float alpha,
        bool showInventoryHint)
    {
        if (!showInventoryHint || info.Room == null)
        {
            textBars.Draw(target, info.Position + offset, info.Title, info.Color, alpha,
                System.Array.Empty<GuiTextBar>());
            return;
        }

        int foodValue = 0;
        foreach (Item item in info.Room.Items)
            if (item.FoodValue > 0)
                foodValue += item.FoodValue;

        int foodUnits = foodValue / 3;
        List<GuiTextBar> bars = new(4);
        if (foodUnits > 0)
            bars.Add(new GuiTextBar(GuiTextBarType.RedBar, foodUnits));
        AddTrapIcons(bars, mapState, info.Room);
        if (info.Room.IsWaterSource)
            bars.Add(new GuiTextBar(GuiTextBarType.BlueBar, mapState.Source.Reserve));
        textBars.Draw(target, info.Position + offset, info.Title, info.Color, alpha, bars);
    }

    static void AddTrapIcons(List<GuiTextBar> bars, Location location, Room? room = null)
    {
        var production = location.Production;
        if (production == null ||
            (room != null && location.GetFoodProductionRate().FoodPerDay <= 0))
            return;

        if (room != null)
        {
            Room? preferredRoom = location.Rooms.FirstOrDefault(candidate => candidate.Items
                .Any(item => item.Type.Production == production));
            Room? productionRoom = preferredRoom != null && !preferredRoom.Items.IsFull
                ? preferredRoom
                : location.Rooms.FirstOrDefault(candidate => !candidate.Items.IsFull);
            if (room != productionRoom)
                return;
        }

        int toolCount = location.GetProductionToolCount(production);

        int trapLevel = GetTrapLevel(production);
        int activeToolCount = System.Math.Min(2,
            System.Math.Min(toolCount, production.MaxToolCount));
        for (int i = 0; i < activeToolCount && trapLevel > 0; i++)
            bars.Add(new GuiTextBar(GuiTextBarType.Dots, trapLevel));
    }

    static int GetTrapLevel(Production production)
    {
        int icon = production.Produce.ID switch
        {
            "item_maggots" => 1,
            "item_rats" => 2,
            "item_snake" => 3,
            "item_meat" => 4,
            _ => 0
        };
        return icon;
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
