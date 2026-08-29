using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.Maps;

class MapViewOverlaySelectedLocation : IMapViewOverlay
{
    readonly Module app;
    ClassicGame game;

    public int LocationNumber { get; set; } = -1;
    public bool IsVisible { get; set; } = true;

    public MapViewOverlaySelectedLocation(Module app)
    {
        this.app = app;
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
        var info = new MapViewHoverInfo(title, entrance.Area.Center, BurntimeClassic.LightGray);

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
