using Burntime.Remaster.Logic;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Remaster.Maps
{
    class MapViewOverlayPlayer : IMapViewOverlay
    {
        Map map;
        Module app;

        public bool IsVisible
        {
            get { return true; }
            set { }
        }

        public MapViewOverlayPlayer(Module app)
        {
            this.app = app;
        }

        public void MouseMoveOverlay(Vector2 position)
        {
        }

        public void UpdateOverlay(WorldState world, float elapsed)
        {
            map = (world as ClassicGame).World.Map;
        }

        public void RenderOverlay(RenderTarget target, Vector2 offset, Vector2 size)
        {
            if (map is null) return;

            ClassicGame game = (ClassicGame)app.GameState;
            for (int i = 0; i < game.World.Players.Count; i++)
            {
                Player p = game.World.Players[i];
                if (p.IsDead)
                    continue;

                int[] icons = new int[] { 17, 16, 19, 18 };

                ISprite sprite = app.ResourceManager.GetImage("syst.raw?" + icons[p.IconID]);
                ISprite faceSprite = app.IsNewGfx ? app.ResourceManager.GetImage("gfx/objects/player_icons.png?" + p.FaceID) : null;

                Vector2 pos = new Vector2();

                if (p.IsTraveling)
                {
                    Vector2 start = map.Entrances[p.Location].Area.Center + offset;
                    Vector2 end = map.Entrances[p.Destination].Area.Center + offset;

                    pos = end + ((start - end) * p.RemainingDays / p.TravelDays);
                }
                else
                {
                    pos.x = map.Entrances[p.Location].Area.Position.x + offset.x;
                    pos.y = map.Entrances[p.Location].Area.Center.y + offset.y;

                    for (int j = 0; j < i; j++)
                    {
                        if (game.World.Players[j].Location == p.Location)
                            pos += 3;
                    }
                }

                target.DrawSprite(pos + new Vector2(-sprite.Width / 2, -15), sprite);
                if (faceSprite is not null)
                {
                    target.Layer++;
                    target.DrawSprite(pos + new Vector2(-sprite.Width / 2, -15), faceSprite);
                }

                target.Layer++;
            }
        }

        public IMapObject GetObjectAt(Vector2 position)
        {
            return null;
        }
    }
}
