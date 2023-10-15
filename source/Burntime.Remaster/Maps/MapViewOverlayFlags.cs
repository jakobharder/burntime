using Burntime.Remaster.Logic;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Remaster.Maps
{
    class MapViewOverlayFlags : IMapViewOverlay
    {
        ClassicGame state;
        Module app;
        SpriteAnimation ani;

        public bool IsVisible
        {
            get { return true; }
            set { }
        }

        public MapViewOverlayFlags(Module app)
        {
            this.app = app;
            ani = new SpriteAnimation(4);
        }

        public void MouseMoveOverlay(Vector2 position)
        {
        }

        public void UpdateOverlay(WorldState world, float elapsed)
        {
            state = world as ClassicGame;
            ani.Update(elapsed);
        }

        public void RenderOverlay(RenderTarget target, Vector2 offset, Vector2 size)
        {
            if (state != null)
            {
                for (int i = 0; i < state.World.Locations.Count; i++)
                {
                    Location l = state.World.Locations[i];

                    if (l.Player != null)
                    {
                        Rect area = state.World.Map.Entrances[i].Area;
                        l.Player.Flag.Object.Animation.Frame = ani.Frame;
                        target.DrawSprite(new Vector2(area.Right, area.Top) + offset, l.Player.Flag);
                    }
                }
            }
        }

        public IMapObject GetObjectAt(Vector2 position)
        {
            return null;
        }
    }
}
