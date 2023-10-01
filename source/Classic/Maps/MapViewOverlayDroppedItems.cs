using Burntime.Classic.Logic;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Classic.Maps
{
    class MapViewOverlayDroppedItems : IMapViewOverlay
    {
        ISprite[] icons;
        Location mapState;
        Module app;

        public bool IsVisible
        {
            get { return true; }
            set { }
        }

        public MapViewOverlayDroppedItems(Module app)
        {
            this.app = app;

            icons = new ISprite[2];
            icons[0] = app.ResourceManager.GetImage("munt.raw?3");
            icons[1] = app.ResourceManager.GetImage("munt.raw?4");
        }

        public void MouseMoveOverlay(Vector2 position)
        {
            if (mapState == null)
                return;

            IMapObject obj = GetObjectAt(position);
            if (obj != null)
                mapState.Hover = new MapViewHoverInfo(obj, app.ResourceManager, new PixelColor(180, 152, 112));
        }

        public void UpdateOverlay(WorldState world, float elapsed)
        {
            mapState = world.CurrentLocation as Location;
        }

        public void RenderOverlay(IRenderTarget target, Vector2 offset, Vector2 size)
        {
            if (mapState != null)
            {
                for (int i = 0; i < mapState.Items.Count; i++)
                {
                    DroppedItem item = mapState.Items.MapObjects[i];
                    target.DrawSprite(item.Position + offset - new Vector2(4, 2), icons[item.Icon]);
                }
            }
        }

        public IMapObject GetObjectAt(Vector2 position)
        {
            if (mapState == null)
                return null;

            for (int i = 0; i < mapState.Items.Count; i++)
            {
                DroppedItem item = mapState.Items.MapObjects[i];
                Vector2 distance = item.Position - position;

                if (distance.Length < 10)
                    return item;
            }

            return null;
        }
    }
}
