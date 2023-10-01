using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Classic.Logic;

namespace Burntime.Classic.Maps
{
    class MapViewOverlayCharacters : IMapViewOverlay
    {
        Location mapState;
        Player player;
        Module app;
        SpriteAnimation ani;
        bool debugRender;

        Character selectedCharacter;
        public Character SelectedCharacter
        {
            get { return selectedCharacter; }
        }

        public bool IsVisible
        {
            get { return true; }
            set { }
        }

        public MapViewOverlayCharacters(Module App)
        {
            app = App;

            ani = new SpriteAnimation(2);
            ani.Speed = 10.0f;

            debugRender = app.Settings["debug"].GetBool("show_path") && app.Settings["debug"].GetBool("enable_cheats");
        }

        public void MouseMoveOverlay(Vector2 position)
        {
            if (mapState == null)
                return;

            Character hoveredChar = (Character)GetObjectAt(position);
            if (hoveredChar != null)
            {
                PixelColor color;
                if (hoveredChar.Player != null)
                {
                    if (hoveredChar.Player.Group.Contains(hoveredChar))
                        color = hoveredChar.Player.Color;
                    else
                        color = hoveredChar.Player.ColorDark;
                }
                else
                {
                    color = new PixelColor(252, 220, 0);
                }

                mapState.Hover = new MapViewHoverInfo(hoveredChar, app.ResourceManager, color);
            }
        }

        public void UpdateOverlay(WorldState world, float elapsed)
        {
            mapState = world.CurrentLocation as Location;
            player = world.CurrentPlayer as Player;
            selectedCharacter = player == null ? null : player.SelectedCharacter;

            ani.Update(elapsed);
        }

        public void RenderOverlay(IRenderTarget target, Vector2 offset, Vector2 size)
        {
            if (mapState != null)
            {
                List<Character> characters = new List<Character>();

                foreach (Character chr in mapState.Characters)
                    characters.Add(chr);

                if (player != null)
                {
                    for (int i = 0; i < player.Group.Count; i++)
                        characters.Add(player.Group[i]);
                }

                foreach (Character chr in characters)
                {
                    // skip if dead
                    if (chr.IsDead && chr.DeadAnimationFinished ||
                        chr.IsPlayerCharacter && chr.Player.IsDead)
                    {
                        continue;
                    }

                    Vector2 pos = chr.Position + offset;
                    // align character sprite to bottom center
                    pos.x -= chr.Body.Object.Width / 2;
                    pos.y -= chr.Body.Object.Height;

                    if (!chr.IsDead && SelectedCharacter == chr && chr.Animation == 0)
                        chr.Body.Object.Animation.Frame = ani.Frame;
                    else
                        chr.Body.Object.Animation.Frame = chr.Animation;
                    target.DrawSprite(pos, chr.Body);

                    if (debugRender)
                    {
                        IRenderTarget lineTarget = target.GetSubBuffer(new Rect(Vector2.Zero, target.Size));
                        lineTarget.Offset = offset;
                        chr.Path.DebugRender(lineTarget);
                    }
                }
            }
        }

        public IMapObject GetObjectAt(Vector2 position)
        {
            if (mapState == null)
                return null;

            IMapObject obj = null;

            foreach (Character chr in mapState.Characters)
            {
                if (chr.IsDead)
                    continue;

                Vector2 distance = chr.Position - position;
                // align to bottom center
                distance.y -= chr.Body.Object.Height / 2;

                if (distance.Length < 10)
                    obj = chr;
            }

            if (player != null)
            {
                for (int i = 0; i < player.Group.Count; i++)
                {
                    Vector2 distance = player.Group[i].Position - position;
                    // align to bottom center
                    distance.y -= player.Group[i].Body.Object.Height / 2;

                    if (distance.Length < 10)
                        obj = player.Group[i];
                }
            }

            return obj;
        }
    }
}
