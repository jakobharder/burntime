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
    public IMapObject? Object { get; init; }

    public MapViewHoverInfo(String title, Vector2 position, PixelColor color, Room room = null)
    {
        Title = title;
        Position = new Vector2(position.x, position.y - 9);
        Color = color;
        Room = room;
        Object = room;
    }

    public MapViewHoverInfo(IMapObject obj, IResourceManager manager, PixelColor color)
    {
        Title = obj.GetTitle(manager);
        Position = new Vector2(obj.MapArea.Left + obj.MapArea.Width / 2, obj.MapArea.Top - 10);
        Color = color;
        Room = obj as Room;
        Character = obj as Character;
        Object = obj;
    }
}

internal readonly record struct MapViewHoverTextEntry(MapViewHoverInfo Info,
    float Alpha, bool ShowInventoryHint = false);

class MapViewOverlayHoverText : IMapViewOverlay
{
    ClassicGame game;
    Location mapState;
    Player player;
    IResourceManager resMan;
    readonly GuiTextBars textBars;
    Character? announcedCharacter;
    MapViewHoverInfo? announcedCharacterInfo;
    float announcementRemaining;
    IMapObject? commandTarget;
    Character? commandOwner;
    Location? commandLocation;
    readonly List<MapViewHoverTextEntry> additionalInfo = [];

    const float AnnouncementFadeDuration = 0.35f;

    public bool IsVisible { get; set; } = true;
    public bool ShowAllEntrances { get; set; }

    public MapViewOverlayHoverText(Module App)
    {
        resMan = App.ResourceManager;
        textBars = new GuiTextBars(App);
    }

    public void MouseMoveOverlay(Vector2 Position)
    {
    }

    public void AnnounceCharacter(Character character, float duration)
    {
        announcedCharacter = character;
        announcedCharacterInfo = new MapViewHoverInfo(character, resMan,
            GetCharacterColor(character));
        announcementRemaining = duration;
    }

    public void SetAdditionalInfo(IEnumerable<MapViewHoverTextEntry> entries)
    {
        additionalInfo.Clear();
        additionalInfo.AddRange(entries);
    }

    public void SelectTarget(IMapObject target, Character owner)
    {
        commandTarget = target;
        commandOwner = owner;
        commandLocation = mapState;
    }

    public void ClearTarget()
    {
        commandTarget = null;
        commandOwner = null;
        commandLocation = null;
    }

    public void UpdateOverlay(WorldState world, float elapsed)
    {
        game = world as ClassicGame;
        mapState = world.CurrentLocation as Location;
        player = world.CurrentPlayer as Player;
        if (commandTarget != null)
        {
            if (mapState != commandLocation || player?.SelectedCharacter != commandOwner)
                ClearTarget();
        }

        if (announcementRemaining > 0 && announcedCharacter != null &&
            announcedCharacterInfo != null && !announcedCharacter.IsDead)
        {
            announcementRemaining = System.Math.Max(0, announcementRemaining - elapsed);
            announcedCharacterInfo.Position = new Vector2(
                announcedCharacter.MapArea.Left + announcedCharacter.MapArea.Width / 2,
                announcedCharacter.MapArea.Top - 10);
        }
        else
        {
            ClearAnnouncement();
        }
    }

    public void RenderOverlay(RenderTarget Target, Vector2 Offset, Vector2 Size)
    {
        const int topMargin = 8;

        var textTarget = Target.GetSubBuffer(new Rect(0, topMargin, Target.Width, Target.Height - topMargin));

        MapViewHoverInfo? targetInfo = CreateTargetInfo();
        if (mapState != null && mapState.Hover != null &&
            !IsSameTarget(mapState.Hover, targetInfo))
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

        foreach (MapViewHoverTextEntry entry in additionalInfo)
        {
            if (entry.Info.Character != null)
                DrawCharacterText(textTarget, entry.Info,
                    Offset - new Vector2(0, topMargin), entry.Alpha);
            else
                DrawEntranceText(textTarget, entry.Info,
                    Offset - new Vector2(0, topMargin), entry.Alpha,
                    entry.ShowInventoryHint);
        }

        if (ShowAllEntrances && game?.MainMapView == true)
        {
            for (int i = 0; i < game.World.Locations.Count && i < game.World.Map.Entrances.Length; i++)
            {
                Location location = game.World.Locations[i];
                if (location.Player != player ||
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
                if (mapState.Hover?.Room == room || targetInfo?.Room == room)
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

        if (targetInfo != null)
        {
            if (targetInfo.Character != null)
                DrawCharacterText(textTarget, targetInfo,
                    Offset - new Vector2(0, topMargin), 1);
            else
                DrawEntranceText(textTarget, targetInfo,
                    Offset - new Vector2(0, topMargin), 1,
                    showInventoryHint: mapState?.Player == player);
        }

        if (announcedCharacterInfo != null)
        {
            float alpha = System.Math.Min(1,
                announcementRemaining / AnnouncementFadeDuration);
            DrawCharacterText(textTarget, announcedCharacterInfo,
                Offset - new Vector2(0, topMargin), alpha);
        }
    }

    void ClearAnnouncement()
    {
        announcedCharacter = null;
        announcedCharacterInfo = null;
        announcementRemaining = 0;
    }

    MapViewHoverInfo? CreateTargetInfo()
    {
        if (commandTarget == null || mapState == null)
            return null;

        if (commandTarget is EntranceObject entrance)
        {
            int number = entrance.Number;
            if (number < 0 || number >= mapState.Map.Entrances.Length ||
                number >= mapState.Rooms.Count)
                return null;
            Room room = mapState.Rooms[number];
            return player != null && mapState.AreEntrancesBlockedFor(player)
                ? new MapViewHoverInfo(resMan.GetString("newburn?103"),
                    mapState.Map.Entrances[number].Area.Center,
                    ClassicColors.MapTargetText, room)
                : new MapViewHoverInfo(room, resMan, ClassicColors.MapTargetText);
        }

        return new MapViewHoverInfo(commandTarget, resMan, ClassicColors.MapTargetText);
    }

    static bool IsSameTarget(MapViewHoverInfo hover, MapViewHoverInfo? target) =>
        target != null && hover.Object != null &&
        ReferenceEquals(hover.Object, target.Object);

    internal static PixelColor GetCharacterColor(Character character)
    {
        if (character.Player != null)
        {
            return character.Player.Party.Contains(character)
                ? character.Player.Color
                : character.Player.ColorDark;
        }

        return new PixelColor(252, 220, 0);
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
            List<GuiTextBar> radioBars = new(4);
            if (game != null && player != null &&
                RadioIntel.IsAvailable(player, info.WorldLocation!))
            {
                RadioReport report = RadioIntel.Create(game, player, info.WorldLocation!);
                if (report.Defenders > 0)
                    radioBars.Add(new GuiTextBar(
                        GuiTextBarType.CampNpcs, report.Defenders,
                        SeparatorAfter: true));
                if (report.Food > 0)
                    radioBars.Add(new GuiTextBar(GuiTextBarType.RedBar, report.Food));
                AddTrapIcons(radioBars, info.WorldLocation);
                radioBars.Add(new GuiTextBar(GuiTextBarType.BlueBar, report.Water));
            }
            else if (showResourceInfo)
                radioBars.Add(new GuiTextBar(GuiTextBarType.BlueBar, baseWater));
            textBars.Draw(target, info.Position + offset, info.Title, locationColor, alpha,
                radioBars, dangerIcon,
                ownershipFlag, showBackground: true);
            return;
        }

        List<GuiTextBar> bars = new(4);
        int npcCount = info.WorldLocation.CampNPC.Count(character =>
            character.Player == player && !character.IsDead);
        if (npcCount > 0)
            bars.Add(new GuiTextBar(GuiTextBarType.CampNpcs, npcCount,
                SeparatorAfter: true));
        if (foodPerDay > 0)
            bars.Add(new GuiTextBar(GuiTextBarType.RedBar, foodPerDay));
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
