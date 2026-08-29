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
    MapViewHoverInfo info;

    public int EntranceNumber { get; private set; } = -1;
    public IMapObject Object { get; private set; }
    public Vector2? Position { get; private set; }
    public bool IsVisible { get; set; } = true;

    public MapViewOverlayNearbyAction(Module app)
    {
        this.app = app;
    }

    public void MouseMoveOverlay(Vector2 position)
    {
    }

    public void UpdateOverlay(WorldState world, float elapsed)
    {
        EntranceNumber = -1;
        Object = null;
        Position = null;
        info = null;

        if (app.LastInputMode == InputMode.Mouse)
            return;

        if (world.CurrentLocation is not Location location ||
            world.CurrentPlayer is not Player player ||
            player.SelectedCharacter == null)
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
            info = new MapViewHoverInfo(location.Rooms[i], app.ResourceManager, BurntimeClassic.LightGray);
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
                character.IsPlayerCharacter && character.Player.IsDead)
                continue;

            float distance = (character.Position - selectedCharacter.Position).Length;
            if (distance >= CharacterRange || distance >= closestDistance)
                continue;

            PixelColor color;
            if (character.Player != null)
            {
                color = character.Player.Group.Contains(character)
                    ? character.Player.Color
                    : character.Player.ColorDark;
            }
            else
            {
                color = new PixelColor(252, 220, 0);
            }

            closestDistance = distance;
            EntranceNumber = -1;
            Object = character;
            Position = character.Position;
            info = new MapViewHoverInfo(character, app.ResourceManager, color);
        }
    }

    public void RenderOverlay(RenderTarget target, Vector2 offset, Vector2 size)
    {
        if (!IsVisible || info == null)
            return;

        const int topMargin = 8;
        var textTarget = target.GetSubBuffer(new Rect(0, topMargin, target.Width, target.Height - topMargin));
        Font font = app.ResourceManager.GetFont(BurntimeClassic.FontName, info.Color);
        font.DrawText(textTarget, info.Position + offset - new Vector2(0, topMargin), info.Title, TextAlignment.Center);
    }

    public IMapObject GetObjectAt(Vector2 position)
    {
        return null;
    }
}
