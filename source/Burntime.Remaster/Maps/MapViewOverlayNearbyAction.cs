using System.Collections.Generic;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Interaction;

namespace Burntime.Remaster.Maps;

class MapViewOverlayNearbyAction : IMapViewOverlay
{
    const int ItemRange = 20;
    const int CharacterRange = 30;

    readonly Module app;
    readonly MapViewOverlayHoverText hoverText;
    MapViewHoverInfo info;
    Player? player;
    Location? location;
    Character? announcedCharacter;
    MapViewHoverInfo? announcedInfo;
    float announcementRemaining;

    const float AnnouncementFadeDuration = 0.35f;

    public int EntranceNumber { get; private set; } = -1;
    public IMapObject Object { get; private set; }
    public Vector2? Position { get; private set; }
    public bool IsVisible { get; set; } = true;

    public MapViewOverlayNearbyAction(Module app, MapViewOverlayHoverText hoverText)
    {
        this.app = app;
        this.hoverText = hoverText;
    }

    public void MouseMoveOverlay(Vector2 position)
    {
    }

    public void AnnounceCharacter(Character character, float duration)
    {
        announcedCharacter = character;
        announcedInfo = new MapViewHoverInfo(character, app.ResourceManager,
            GetCharacterColor(character));
        announcementRemaining = duration;
    }

    public void UpdateOverlay(WorldState world, float elapsed)
    {
        player = world.CurrentPlayer as Player;
        location = world.CurrentLocation as Location;
        EntranceNumber = -1;
        Object = null;
        Position = null;
        info = null;

        if (announcementRemaining > 0 && announcedCharacter != null && announcedInfo != null &&
            !announcedCharacter.IsDead)
        {
            announcementRemaining = System.Math.Max(0, announcementRemaining - elapsed);
            announcedInfo.Position = new Vector2(
                announcedCharacter.MapArea.Left + announcedCharacter.MapArea.Width / 2,
                announcedCharacter.MapArea.Top - 10);
        }
        else
        {
            announcedCharacter = null;
            announcedInfo = null;
            announcementRemaining = 0;
        }

        if (app.LastInputMode == InputMode.Mouse)
            return;

        if (location == null ||
            player == null || player.SelectedCharacter == null)
            return;

        Character selectedCharacter = player.SelectedCharacter;
        float closestDistance = float.MaxValue;

        int entranceCount = System.Math.Min(location.Map.Entrances.Length, location.Rooms.Count);
        for (int i = 0; i < entranceCount; i++)
        {
            var entrance = location.Map.Entrances[i];
            var entranceObject = new EntranceObject(entrance, i);
            var interaction = new InteractionObject(entranceObject, location.Rooms[i].EntryCondition, null);
            if (!interaction.IsInRange(selectedCharacter.Position))
                continue;

            float distance = entrance.Area.Distance(selectedCharacter.Position);
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            EntranceNumber = i;
            Object = null;
            Position = entrance.Area.Center;
            info = location.AreEntrancesBlockedFor(player)
                ? new MapViewHoverInfo(app.ResourceManager.GetString("newburn?103"), entrance.Area.Center, BurntimeClassic.LightGray, location.Rooms[i])
                : new MapViewHoverInfo(location.Rooms[i], app.ResourceManager, BurntimeClassic.LightGray);
        }

        foreach (DroppedItem item in location.Items.MapObjects)
        {
            float distance = (item.Position - selectedCharacter.Position).Length;
            if (distance >= ItemRange || distance >= closestDistance)
                continue;

            closestDistance = distance;
            EntranceNumber = -1;
            Object = item;
            Position = item.Position;
            info = new MapViewHoverInfo(item, app.ResourceManager, new PixelColor(180, 152, 112));
        }

        var characters = new HashSet<Character>();
        foreach (Character character in location.Characters)
            characters.Add(character);
        foreach (Character character in player.Group)
            characters.Add(character);

        foreach (Character character in characters)
        {
            if (character == selectedCharacter || character.IsDead ||
                character.IsPlayerCharacter && character.Player.IsDead ||
                app.LastInputMode is (InputMode.Keyboard or InputMode.Gamepad) &&
                player.Group.Contains(character))
                continue;

            float distance = (character.Position - selectedCharacter.Position).Length;
            if (distance >= CharacterRange || distance >= closestDistance)
                continue;

            closestDistance = distance;
            EntranceNumber = -1;
            Object = character;
            Position = character.Position;
            info = new MapViewHoverInfo(character, app.ResourceManager,
                GetCharacterColor(character));
        }
    }

    public void RenderOverlay(RenderTarget target, Vector2 offset, Vector2 size)
    {
        if (!IsVisible)
            return;

        const int topMargin = 8;
        var textTarget = target.GetSubBuffer(new Rect(0, topMargin, target.Width, target.Height - topMargin));
        if (info != null)
            DrawInfo(textTarget, info, offset - new Vector2(0, topMargin), 1);
        if (announcedInfo != null)
        {
            float alpha = System.Math.Min(1,
                announcementRemaining / AnnouncementFadeDuration);
            DrawInfo(textTarget, announcedInfo, offset - new Vector2(0, topMargin), alpha);
        }
    }

    void DrawInfo(RenderTarget target, MapViewHoverInfo drawInfo, Vector2 offset, float alpha)
    {
        if (drawInfo.Character != null)
        {
            hoverText.DrawCharacterText(target, drawInfo, offset, alpha);
            return;
        }
        if (drawInfo.Room != null)
        {
            hoverText.DrawEntranceText(target, drawInfo, offset, alpha,
                showInventoryHint: location?.Player == player);
            return;
        }

        Font font = app.ResourceManager.GetFont(BurntimeClassic.FontName, drawInfo.Color);
        font.DrawText(target, drawInfo.Position + offset, drawInfo.Title,
            TextAlignment.Center, VerticalTextAlignment.Center, alpha);
    }

    static PixelColor GetCharacterColor(Character character)
    {
        if (character.Player != null)
        {
            return character.Player.Group.Contains(character)
                ? character.Player.Color
                : character.Player.ColorDark;
        }

        return new PixelColor(252, 220, 0);
    }

    public IMapObject GetObjectAt(Vector2 position)
    {
        return null;
    }
}
