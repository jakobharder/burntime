using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.Maps;

class MapViewOverlaySelectedLocation : IMapViewOverlay
{
    readonly Module app;
    readonly MapViewOverlayHoverText hoverText;
    ClassicGame game;

    public int LocationNumber { get; set; } = -1;
    public bool IsVisible { get; set; } = true;

    public MapViewOverlaySelectedLocation(Module app, MapViewOverlayHoverText hoverText)
    {
        this.app = app;
        this.hoverText = hoverText;
    }

    public void MouseMoveOverlay(Vector2 position)
    {
    }

    public void UpdateOverlay(WorldState world, float elapsed)
    {
        game = world as ClassicGame;
    }

    public void RenderOverlay(RenderTarget target, Vector2 offset, Vector2 size)
    {
        if (!IsVisible || app.LastInputMode == InputMode.Mouse || game == null || LocationNumber < 0 ||
            LocationNumber >= game.World.Map.Entrances.Length)
            return;

        var entrance = game.World.Map.Entrances[LocationNumber];
        string title = app.ResourceManager.GetString(entrance.TitleId);
        var info = new MapViewHoverInfo(title, entrance.Area.Center, BurntimeClassic.LightGray)
        {
            WorldLocation = game.World.Locations[LocationNumber]
        };

        const int topMargin = 8;
        var textTarget = target.GetSubBuffer(new Rect(0, topMargin, target.Width, target.Height - topMargin));
        hoverText.DrawWorldLocationText(textTarget, info, offset - new Vector2(0, topMargin), 1);
    }

    public IMapObject GetObjectAt(Vector2 position)
    {
        return null;
    }
}
