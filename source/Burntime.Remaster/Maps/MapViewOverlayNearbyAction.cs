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
    const int CharacterRange = 35;
    const int CharacterReleaseRange = 50;

    readonly struct TargetKey
    {
        public int EntranceNumber { get; }
        public IMapObject? Object { get; }

        public TargetKey(int entranceNumber, IMapObject? obj)
        {
            EntranceNumber = entranceNumber;
            Object = obj;
        }

        public bool Matches(TargetKey other) =>
            EntranceNumber == other.EntranceNumber &&
            ReferenceEquals(Object, other.Object);
    }

    sealed class TargetCandidate
    {
        public TargetKey Key { get; init; }
        public float Distance { get; init; }
        public Vector2 Position { get; init; }
        public required MapViewHoverInfo Info { get; init; }
    }

    readonly Module app;
    readonly MapViewOverlayHoverText hoverText;
    MapViewHoverInfo info;
    Player? player;
    Location? location;
    Character? announcedCharacter;
    MapViewHoverInfo? announcedInfo;
    float announcementRemaining;
    readonly List<TargetCandidate> candidates = [];
    readonly List<TargetKey> cycleTargets = [];
    TargetKey? lockedTarget;
    Character? targetOwner;
    int pendingCycleDirection;

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

    public void CycleTarget(int direction)
    {
        if (player?.SelectedCharacter == null)
            return;
        if (candidates.Count == 0)
        {
            pendingCycleDirection = direction;
            return;
        }

        Character selectedCharacter = player.SelectedCharacter;
        if (targetOwner != selectedCharacter || cycleTargets.Count == 0)
        {
            lockedTarget = null;
            cycleTargets.Clear();
            foreach (TargetCandidate candidate in candidates)
                cycleTargets.Add(candidate.Key);
        }
        else
        {
            cycleTargets.RemoveAll(key => !IsAvailable(key));
            foreach (TargetCandidate candidate in candidates)
                if (FindKeyIndex(cycleTargets, candidate.Key) == -1)
                    cycleTargets.Add(candidate.Key);
        }

        if (cycleTargets.Count == 0)
            return;

        TargetKey current = lockedTarget ?? candidates[0].Key;
        int currentIndex = FindKeyIndex(cycleTargets, current);
        if (currentIndex == -1)
            currentIndex = direction > 0 ? -1 : 0;

        int targetIndex = (currentIndex + (direction < 0 ? -1 : 1) +
            cycleTargets.Count) % cycleTargets.Count;
        lockedTarget = cycleTargets[targetIndex];
        targetOwner = selectedCharacter;

        TargetCandidate? selected = FindCandidate(lockedTarget.Value) ??
            CreateRetainedCharacterCandidate(lockedTarget.Value, selectedCharacter);
        if (selected != null)
            Apply(selected);
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
        {
            ClearTargetLock();
            candidates.Clear();
            return;
        }

        if (location == null ||
            player == null || player.SelectedCharacter == null)
        {
            ClearTargetLock();
            return;
        }

        Character selectedCharacter = player.SelectedCharacter;
        if (targetOwner != null && targetOwner != selectedCharacter)
            ClearTargetLock();

        candidates.Clear();

        int entranceCount = System.Math.Min(location.Map.Entrances.Length, location.Rooms.Count);
        for (int i = 0; i < entranceCount; i++)
        {
            var entrance = location.Map.Entrances[i];
            var entranceObject = new EntranceObject(entrance, i);
            var interaction = new InteractionObject(entranceObject, location.Rooms[i].EntryCondition, null);
            if (!interaction.IsInRange(selectedCharacter.Position))
                continue;

            float distance = entrance.Area.Distance(selectedCharacter.Position);
            candidates.Add(new TargetCandidate
            {
                Key = new TargetKey(i, null),
                Distance = distance,
                Position = entrance.Area.Center,
                Info = location.AreEntrancesBlockedFor(player)
                    ? new MapViewHoverInfo(app.ResourceManager.GetString("newburn?103"), entrance.Area.Center, ClassicColors.LightGray, location.Rooms[i])
                    : new MapViewHoverInfo(location.Rooms[i], app.ResourceManager, ClassicColors.LightGray)
            });
        }

        foreach (DroppedItem item in location.Items.MapObjects)
        {
            float distance = (item.Position - selectedCharacter.Position).Length;
            if (distance >= ItemRange)
                continue;

            candidates.Add(new TargetCandidate
            {
                Key = new TargetKey(-1, item),
                Distance = distance,
                Position = item.Position,
                Info = new MapViewHoverInfo(item, app.ResourceManager,
                    new PixelColor(180, 152, 112))
            });
        }

        var characters = new HashSet<Character>();
        foreach (Character character in location.Characters)
            characters.Add(character);
        foreach (Character character in player.Party)
            characters.Add(character);

        foreach (Character character in characters)
        {
            if (character == selectedCharacter || character.IsDead ||
                character.IsPlayerCharacter && character.Player.IsDead ||
                app.LastInputMode is (InputMode.Keyboard or InputMode.Gamepad) &&
                player.Party.Contains(selectedCharacter) && player.Party.Contains(character))
                continue;

            float distance = (character.Position - selectedCharacter.Position).Length;
            if (distance >= CharacterRange)
                continue;

            candidates.Add(CreateCharacterCandidate(character, distance));
        }

        candidates.Sort((left, right) => left.Distance.CompareTo(right.Distance));

        int cycleDirection = pendingCycleDirection;
        pendingCycleDirection = 0;
        if (cycleDirection != 0 && candidates.Count > 0)
            CycleTarget(cycleDirection);

        if (lockedTarget.HasValue)
        {
            TargetCandidate? selected = FindCandidate(lockedTarget.Value) ??
                CreateRetainedCharacterCandidate(lockedTarget.Value, selectedCharacter);
            if (selected != null)
            {
                Apply(selected);
                return;
            }

            ClearTargetLock();
        }

        if (candidates.Count > 0)
            Apply(candidates[0]);
    }

    TargetCandidate CreateCharacterCandidate(Character character, float distance) => new()
    {
        Key = new TargetKey(-1, character),
        Distance = distance,
        Position = character.Position,
        Info = new MapViewHoverInfo(character, app.ResourceManager,
            GetCharacterColor(character))
    };

    TargetCandidate? CreateRetainedCharacterCandidate(TargetKey key,
        Character selectedCharacter)
    {
        if (key.Object is not Character character || location == null || player == null ||
            character == selectedCharacter || character.IsDead ||
            character.IsPlayerCharacter && character.Player.IsDead ||
            player.Party.Contains(selectedCharacter) && player.Party.Contains(character) ||
            !location.Characters.Contains(character) && !player.Party.Contains(character))
            return null;

        float distance = (character.Position - selectedCharacter.Position).Length;
        return distance < CharacterReleaseRange
            ? CreateCharacterCandidate(character, distance)
            : null;
    }

    TargetCandidate? FindCandidate(TargetKey key)
    {
        foreach (TargetCandidate candidate in candidates)
            if (candidate.Key.Matches(key))
                return candidate;
        return null;
    }

    bool IsAvailable(TargetKey key) =>
        FindCandidate(key) != null ||
        lockedTarget.HasValue && lockedTarget.Value.Matches(key);

    static int FindKeyIndex(List<TargetKey> targets, TargetKey key)
    {
        for (int i = 0; i < targets.Count; i++)
            if (targets[i].Matches(key))
                return i;
        return -1;
    }

    void Apply(TargetCandidate candidate)
    {
        EntranceNumber = candidate.Key.EntranceNumber;
        Object = candidate.Key.Object;
        Position = candidate.Position;
        info = candidate.Info;
    }

    void ClearTargetLock()
    {
        lockedTarget = null;
        targetOwner = null;
        pendingCycleDirection = 0;
        cycleTargets.Clear();
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
            return character.Player.Party.Contains(character)
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
