using System;
using System.Collections.Generic;
using Math = System.Math;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Remaster.GUI;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.Maps;

// Entrance hit regions and touch presentation; entry rules remain in LocationScene.
sealed class MapViewOverlayTouch : IMapViewOverlay, IMapViewEntranceOverlay
{
    const int ItemRange = 20;
    const int CharacterRange = 35;

    readonly Module app;
    readonly MapView view;
    readonly MapViewOverlayHoverText hoverText;
    Location? location;
    Player? player;
    Character? character;
    Character? destinationOwner;
    IMapObject? destination;
    readonly Dictionary<int, bool> nearby = new();
    readonly Dictionary<int, float> opacity = new();
    readonly Dictionary<IMapObject, float> objectOpacity = new();
    readonly Dictionary<int, MapViewHoverInfo> entranceInfo = new();
    readonly List<TouchEntranceTarget> targets = new();
    readonly List<TouchObjectTarget> objectTargets = new();
    readonly List<MapViewHoverTextEntry> hoverEntries = new();

    bool isVisible = true;
    public bool IsVisible
    {
        get => isVisible;
        set
        {
            if (isVisible == value)
                return;
            isVisible = value;
            if (!isVisible)
                OnHide();
        }
    }

    public MapViewOverlayTouch(Module app, MapView view,
        MapViewOverlayHoverText hoverText)
    {
        this.app = app;
        this.view = view;
        this.hoverText = hoverText;
    }

    internal int Destination => destination is EntranceObject entrance
        ? entrance.Number
        : -1;
    bool Active => location != null;

    public void Reset()
    {
        view.HoveredObject = null;
        if (location != null)
            location.Hover = null;
        OnHide();
    }

    void OnHide()
    {
        nearby.Clear();
        opacity.Clear();
        objectOpacity.Clear();
        entranceInfo.Clear();
        targets.Clear();
        objectTargets.Clear();
        hoverEntries.Clear();
        hoverText.SetAdditionalInfo(hoverEntries);
        destination = null;
        destinationOwner = null;
        character = null;
        location = null;
        player = null;
    }

    public void SelectDestination(IMapObject target, Character owner)
    {
        destination = target;
        destinationOwner = owner;
        hoverText.SelectTarget(target, owner);
        if (target is EntranceObject entrance)
            opacity[entrance.Number] = 1;
        if (location != null)
            HoverDestination();
    }

    public void ClearDestination()
    {
        destination = null;
        destinationOwner = null;
        hoverText.ClearTarget();
        view.HoveredObject = null;
        if (location != null)
            location.Hover = null;
    }

    public void UpdateOverlay(WorldState world, float elapsed)
    {
        if (!IsVisible)
            return;

        view.HoveredObject = null;
        var nextLocation = world.CurrentLocation as Location;
        var nextPlayer = world.CurrentPlayer as Player;
        var nextCharacter = nextPlayer?.SelectedCharacter;
        if (location != nextLocation || character != nextCharacter)
            Reset();
        location = nextLocation;
        player = nextPlayer;
        character = nextCharacter;
        if (!Active || character == null) return;
        if (destination != null && destinationOwner != character)
        {
            ClearDestination();
        }
        else if (destination != null)
        {
            HoverDestination();
        }

        hoverEntries.Clear();
        entranceInfo.Clear();
        int count = Math.Min(location!.Map.Entrances.Length, location.Rooms.Count);
        for (int i = 0; i < count; i++)
        {
            var entrance = location.Map.Entrances[i];
            var condition = location.Rooms[i].EntryCondition;
            Rect region = condition?.HasRegionOnMap == true ? condition.RegionOnMap : entrance.Area;
            float range = Math.Max(15, condition?.MaxDistanceOnMap ?? 15);
            bool wasNear = nearby.GetValueOrDefault(i);
            bool isNear = TouchEntranceLayout.IsNearby(region.Distance(character.Position), range, wasNear);
            nearby[i] = isNear;
            float desired = isNear || i == Destination ? 1 : 0;
            float current = opacity.GetValueOrDefault(i);
            opacity[i] = Math.Clamp(current + Math.Sign(desired - current) * Math.Max(0, elapsed) / .2f, 0, 1);
            MapViewHoverInfo info = CreateEntranceInfo(i);
            entranceInfo[i] = info;
            if (i != Destination && opacity[i] > .05f)
                hoverEntries.Add(new MapViewHoverTextEntry(info, opacity[i]));
        }


        var visibleObjects = new HashSet<IMapObject>();
        foreach (DroppedItem item in location.Items.MapObjects)
            if ((item.Position - character.Position).Length < ItemRange)
                visibleObjects.Add(item);

        foreach (Character candidate in location.Characters)
        {
            if (candidate != character && !candidate.IsDead &&
                (!candidate.IsPlayerCharacter || !candidate.Player.IsDead) &&
                (candidate.Position - character.Position).Length < CharacterRange)
            {
                visibleObjects.Add(candidate);
            }
        }
        foreach (Character candidate in player!.Party)
            if (candidate != character && !candidate.IsDead &&
                (candidate.Position - character.Position).Length < CharacterRange)
                visibleObjects.Add(candidate);

        foreach (IMapObject obj in visibleObjects)
        {
            float current = objectOpacity.GetValueOrDefault(obj);
            objectOpacity[obj] = Math.Clamp(current + Math.Max(0, elapsed) / .2f, 0, 1);
        }
        foreach (IMapObject obj in new List<IMapObject>(objectOpacity.Keys))
        {
            if (!visibleObjects.Contains(obj))
            {
                float alpha = Math.Clamp(objectOpacity[obj] - Math.Max(0, elapsed) / .2f, 0, 1);
                if (alpha == 0)
                    objectOpacity.Remove(obj);
                else
                    objectOpacity[obj] = alpha;
            }
        }

        objectTargets.Clear();
        foreach ((IMapObject obj, float alpha) in objectOpacity)
        {
            if (alpha <= .05f)
                continue;
            if (ReferenceEquals(obj, destination))
                continue;
            PixelColor color = obj is Character character
                ? MapViewOverlayHoverText.GetCharacterColor(character)
                : new PixelColor(180, 152, 112);
            var info = new MapViewHoverInfo(obj, app.ResourceManager, color);
            hoverEntries.Add(new MapViewHoverTextEntry(info, alpha));
            objectTargets.Add(new TouchObjectTarget(obj, GetTextBounds(info)));
        }
        hoverText.SetAdditionalInfo(hoverEntries);
    }

    MapViewHoverInfo CreateEntranceInfo(int number, PixelColor? color = null)
    {
        var entrance = location!.Map.Entrances[number];
        Room room = location.Rooms[number];
        PixelColor textColor = color ?? ClassicColors.LightGray;
        return player != null && location.AreEntrancesBlockedFor(player)
            ? new MapViewHoverInfo(app.ResourceManager.GetString("newburn?103"),
                entrance.Area.Center, textColor, room)
            : new MapViewHoverInfo(room, app.ResourceManager,
                textColor);
    }

    void HoverDestination()
    {
        view.HoveredObject = destination;
        location!.Hover = destination is EntranceObject entrance
            ? CreateEntranceInfo(entrance.Number)
            : new MapViewHoverInfo(destination!, app.ResourceManager,
                destination is Character character
                    ? MapViewOverlayHoverText.GetCharacterColor(character)
                    : new PixelColor(180, 152, 112));
    }

    void Layout(Vector2 offset, Vector2 size)
    {
        targets.Clear();
        if (!Active || size.x < 25 || size.y < 25) return;
        int count = Math.Min(location!.Map.Entrances.Length, location.Rooms.Count);
        for (int i = 0; i < count; i++)
        {
            var area = location.Map.Entrances[i].Area + offset;
            if (area.Right <= 0 || area.Bottom <= 0 || area.Left >= size.x || area.Top >= size.y) continue;
            var anchor = new Vector2(Math.Clamp(area.Center.x, 5, size.x - 6), Math.Clamp(area.Center.y, 5, size.y - 6));
            Rect? label = null;
            if ((i == Destination || opacity.GetValueOrDefault(i) > .05f) &&
                entranceInfo.TryGetValue(i, out MapViewHoverInfo? info))
            {
                label = GetTextBounds(info) + offset;
            }
            targets.Add(new(i, anchor,
                new Rect(anchor - new Vector2(12, 12), new Vector2(25, 25)), label));
        }
    }

    Rect GetTextBounds(MapViewHoverInfo info)
    {
        var font = app.ResourceManager.GetFont(BurntimeClassic.FontName,
            ClassicColors.MenuTextHover);
        Vector2 size = font == null
            ? new Vector2(25, 25)
            : new Vector2(Math.Max(25, font.GetWidth(info.Title) + 8),
                Math.Max(25, font.GetHeight() + 8));
        return new Rect(info.Position - size / 2, size);
    }

    public int HitTestEntrance(Vector2 position, Vector2 offset, Vector2 size)
    {
        if (!IsVisible)
            return -1;
        if (!new Rect(Vector2.Zero, size).PointInside(position))
            return -1;
        if (location == null)
            UpdateOverlay(app.GameState, 0);
        Layout(offset, size);
        return TouchEntranceLayout.HitTest(targets, position);
    }

    public void RenderOverlay(RenderTarget target, Vector2 offset, Vector2 size)
    {
        Layout(offset, size);
        foreach (var entry in targets)
        {
            bool selected = entry.Number == Destination;
            var color = selected ? ClassicColors.MenuTextHover : ClassicColors.LightGray;
            // A static, outlined glint stays legible against bright and dark terrain.
            target.RenderRect(entry.Anchor - new Vector2(1, 3), new Vector2(3, 7), new PixelColor(210, 15, 15, 15));
            target.RenderRect(entry.Anchor - new Vector2(3, 1), new Vector2(7, 3), new PixelColor(210, 15, 15, 15));
            target.RenderRect(entry.Anchor - new Vector2(0, 2), new Vector2(1, 5), color);
            target.RenderRect(entry.Anchor - new Vector2(2, 0), new Vector2(5, 1), color);
        }
    }

    public void MouseMoveOverlay(Vector2 position) { }
    public IMapObject GetObjectAt(Vector2 position)
    {
        foreach (TouchObjectTarget target in objectTargets)
            if (target.Label.PointInside(position))
                return target.Object;
        return null!;
    }
}

internal readonly record struct TouchEntranceTarget(int Number, Vector2 Anchor, Rect Marker, Rect? Label);
internal readonly record struct TouchObjectTarget(IMapObject Object, Rect Label);

internal static class TouchEntranceLayout
{
    internal static bool IsNearby(float distance, float interactionRange, bool wasNear) =>
        distance <= interactionRange + (wasNear ? 42 : 30);

    internal static int HitTest(IReadOnlyList<TouchEntranceTarget> targets, Vector2 position)
    {
        foreach (var target in targets)
            if (target.Label?.PointInside(position) == true) return target.Number;
        int selected = -1;
        float distance = float.MaxValue;
        foreach (var target in targets)
        {
            float candidate = (position - target.Anchor).Length;
            if (target.Marker.PointInside(position) && candidate < distance)
            {
                selected = target.Number;
                distance = candidate;
            }
        }
        return selected;
    }
}
