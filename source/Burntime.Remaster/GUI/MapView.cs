using Burntime.Remaster.Logic;
using Burntime.Remaster.Maps;
using Burntime.Data.BurnGfx;
using Burntime.Framework;
using Burntime.Framework.Event;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using System;
using System.Collections.Generic;

namespace Burntime.Remaster.GUI;

public interface IMapEntranceHandler
{
    String GetEntranceTitle(int Number);
    bool OnClickEntrance(int Number, MouseButton Button);
}

public class MapScrollArgs : EventArgs
{
    public MapScrollArgs(Vector2 offset)
    {
        this.offset = offset;
    }

    Vector2 offset;
    public Vector2 Offset
    {
        get { return offset; }
    }
}

public class ObjectArgs : EventArgs
{
    public ObjectArgs(IMapObject obj, Vector2 position, MouseButton button)
    {
        this.obj = obj;
        this.button = button;
        this.position = position;
    }

    IMapObject obj;
    public IMapObject Object
    {
        get { return obj; }
    }

    MouseButton button;
    public MouseButton Button
    {
        get { return button; }
    }

    Vector2 position;
    public Vector2 Position
    {
        get { return position; }
    }
}

public class MapView : Window
{
    public event EventHandler<ObjectArgs> ClickObject;
    public event EventHandler<MapScrollArgs> Scroll;

    Vector2 mousePosition = new Vector2();

    List<Maps.IMapViewOverlay> overlays = new List<Maps.IMapViewOverlay>();
    public List<Maps.IMapViewOverlay> Overlays
    {
        get { return overlays; }
        set { overlays = value; }
    }

    IMapEntranceHandler handler;
    MouseClickEvent mouseClickEvent;
    public MouseClickEvent MouseClickEvent
    {
        get { return mouseClickEvent; }
        set { mouseClickEvent = value; }
    }

    public ParticleEngine Particles { get; } = new ParticleEngine();
    public bool Enabled { get; set; }
    public event ClickHandler ContextMenu;

    public MapView(IMapEntranceHandler Handler, Module App)
        : base(App)
    {
        Enabled = true;
        handler = Handler;
        CaptureAllMouseMove = true;
        CaptureAllMouseClicks = true;

        BurntimeClassic classic = app as BurntimeClassic;
        DebugView = classic.Settings["debug"].GetBool("show_routes") && classic.Settings["debug"].GetBool("enable_cheats");
    }

    MapData map;
    public MapData Map
    {
        get { return map; }
        set { map = value; }
    }

    WayData ways;
    public WayData Ways
    {
        get { return ways; }
        set { ways = value; }
    }

    public Location Location { get; set; }
    public Player Player { get; set; }

    public bool DebugView = false;

    Vector2f _position = new Vector2f();
    public Vector2 ScrollPosition
    {
        get { return new Vector2(_position); }
        set { _position = value; }
    }

    float scrollSpeed = 70;

    Vector2f border = new Vector2f();
    int entrance = -1;

    public int ActiveEntrance
    {
        get { return entrance; }
    }

    public override void OnRender(RenderTarget Target)
    {
        base.OnRender(Target);
        if (map is null) return;

        Vector2 offset = ScrollPosition;

        // pre-fetch all tiles for smooth scrolling
        foreach (var tile in Map.Tiles)
            tile.Image.Touch();

        Rect visible = new Rect(-offset / map.TileSize, Size / map.TileSize + 2);
        visible = visible.Intersect(new Rect(0, 0, map.Width, map.Height));

        foreach (Vector2 pos in visible)
        {
            Vector2 trans = pos * map.TileSize + offset;
            Target.DrawSprite(trans, map[pos.x, pos.y].Image);
        }

        Target.Layer++;

        if (DebugView)
        {
            foreach (Vector2 pos in new Rect(0, 0, map.Mask.Width, map.Mask.Height))
            {
                if (!map.Mask[pos])
                    Target.RenderRect(offset + pos * map.Mask.Resolution, Vector2.One * map.Mask.Resolution, new PixelColor(50, 255, 0, 0));
            }
            for (int i = 0; i < map.Entrances.Length; i++)
                Target.RenderRect(map.Entrances[i].Area.Position + offset, map.Entrances[i].Area.Size, new PixelColor(50, 0, 0, 255));
        }

        List<int> renderWays = new List<int>();

        int e = entrance;
        if (e != -1)
        {
            if (DebugView && ways != null)
            {
                foreach (int way in ways.Cross[e].Ways)
                    renderWays.Add(way);
            }
        }

        foreach (int way in renderWays)
        {
            if (ways.Ways[way].Images.Length > 0)
            {
                Vector2 pos = ways.Ways[way].Position + offset;

                foreach (var sprite in ways.Ways[way].Images)
                {
                    Target.DrawSprite(pos, sprite);
                    pos.x += 32;
                }
            }
        }

        Target.Layer++;

        var old = Target.Layer;
        Target.Layer += 30;

        foreach (Maps.IMapViewOverlay overlay in overlays)
        {
            Target.Layer++;
            overlay.RenderOverlay(Target, offset, Size);
        }

        Target.Layer++;
        Target.Offset += ScrollPosition;
        Particles.Render(Target);
        Target.Offset -= ScrollPosition;

        Target.Layer = old;
    }

    public override bool OnMouseMove(Vector2 position)
    {
        if (!Enabled)
            return false;

        const int Margin = 4;
        const int BigMargin = 10;

        border.x = 0;
        border.y = 0;

        border.x += (position.x < Margin) ? 1 : 0;
        border.x -= (position.x > Size.x - Margin) ? 1 : 0;
        border.x += (position.x < BigMargin) ? 1 : 0;
        border.x -= (position.x > Size.x - BigMargin) ? 1 : 0;
        border.y += (position.y < Margin) ? 1 : 0;
        border.y -= (position.y > Size.y - Margin) ? 1 : 0;
        border.y += (position.y < BigMargin) ? 1 : 0;
        border.y -= (position.y > Size.y - BigMargin) ? 1 : 0;

        bool found = false;
        for (int i = 0; i < map.Entrances.Length; i++)
        {
            if (map.Entrances[i].Area.PointInside(position - (Vector2)this._position))
            {
                entrance = i;
                found = true;
            }
        }
        if (!found)
            entrance = -1;

        mousePosition = position - ScrollPosition;

        if (_rightClickMove.HasValue)
        {
            _position += (Vector2f)(position - _rightClickMove.Value);
            _position.Max(0);
            _position.Min(Boundings.Size - map.TileSize * map.Size);

            _moveTotal += (position - _rightClickMove.Value).Length;
            _rightClickMove = position;
        }

        return true;
    }

    public override void OnMouseEnter()
    {
        if (_rightClickMove.HasValue && !app.Engine.DeviceManager.Mouse.IsRightDown)
            _rightClickMove = null;

        base.OnMouseEnter();
    }

    public override void OnActivate()
    {
        _rightClickMove = null;

        base.OnActivate();
    }

    public override bool OnMouseClick(Vector2 position, MouseButton button)
    {
        if (!Enabled)
            return false;

        if (button == MouseButton.Right)
        {
            if (_moveTotal < 10)
                ContextMenu?.Invoke(position, button);
            _rightClickMove = null;
            _moveTotal = 0;
        }

        // end all capture here
        if (!Boundings.PointInside(position + Position))
            return false;

        if (entrance != -1 && handler != null)
            if (handler.OnClickEntrance(entrance, button))
                return true;

        IMapObject mostTopObj = null;
        foreach (Maps.IMapViewOverlay overlay in overlays)
        {
            IMapObject obj = overlay.GetObjectAt(position - ScrollPosition);
            if (obj != null)
                mostTopObj = obj;
        }

        if (mostTopObj != null)
        {
            if (ClickObject != null)
                ClickObject.Invoke(this, new ObjectArgs(mostTopObj, position, button));
            return true;
        }

        if (mouseClickEvent != null && button == MouseButton.Left)
        {
            mouseClickEvent.Execute(position - ScrollPosition, button);
            return true;
        }

        return false;
    }

    float _moveTotal;
    Vector2? _rightClickMove = null;
    public override bool OnMouseDown(Vector2 position, MouseButton button)
    {
        if (button == MouseButton.Right)
        {
            _moveTotal = 0;
            _rightClickMove = position;
        }

        return true;
    }

    public override void OnUpdate(float Elapsed)
    {
        if (!_rightClickMove.HasValue && border != Vector2f.Zero)
        {
            _position += border * Elapsed * scrollSpeed;
            _position.Max(0);
            _position.Min(Boundings.Size - map.TileSize * map.Size);
            Scroll?.Invoke(this, new MapScrollArgs(_position));
        }

        // map is smaller than screen, center it
        if (map.TileSize.x * map.Size.x < Boundings.Size.x)
            _position.x = (Boundings.Size.x - map.TileSize.x * map.Size.x) / 2;
        if (map.TileSize.y * map.Size.y < Boundings.Size.y)
            _position.y = (Boundings.Size.y - map.TileSize.y * map.Size.y) / 2;

        if (handler != null)
        {
            ClassicGame game = app.GameState as ClassicGame;
            game.World.ActiveLocationObj.Hover = null;

            foreach (Maps.IMapViewOverlay overlay in overlays)
            {
                overlay.MouseMoveOverlay(mousePosition);
                overlay.UpdateOverlay(game, Elapsed);
            }

            if (entrance != -1)
            {
                if (game.MainMapView)
                {
                    Burntime.Data.BurnGfx.MapEntrance e = game.World.Map.Entrances[entrance];
                    game.World.ActiveLocationObj.Hover = new MapViewHoverInfo(app.ResourceManager.GetString(e.TitleId), e.Area.Center, new PixelColor(212, 212, 212));
                }
                else if (entrance < game.World.ActiveLocationObj.Rooms.Count)
                {
                    game.World.ActiveLocationObj.Hover = new MapViewHoverInfo(game.World.ActiveLocationObj.Rooms[entrance], app.ResourceManager, new PixelColor(212, 212, 212));
                }
            }
        }

        Particles.Update(Elapsed);
    }

    public void CenterTo(Vector2 centerTo)
    {
        _position = -centerTo + (Boundings.Size / 2);
        _position.Max(0);
        _position.Min(Boundings.Size - map.TileSize * map.Size);
        Scroll?.Invoke(this, new MapScrollArgs(_position));
    }
}
