using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.Maps
{
    class MapViewOverlayCharacters : IMapViewOverlay
    {
        Location mapState;
        Player? _currentPlayer;
        Module app;
        SpriteAnimation ani;
        bool debugRender;

        ISprite _shadow;

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

        private bool RenderShadow => app.IsNewGfx;

        public MapViewOverlayCharacters(Module App)
        {
            app = App;

            ani = new SpriteAnimation(2);
            ani.Speed = 10.0f;

            _shadow = app.ResourceManager.GetImage("gfx/char_shadow.png");

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
            _currentPlayer = world.CurrentPlayer as Player;
            selectedCharacter = _currentPlayer == null ? null : _currentPlayer.SelectedCharacter;

            ani.Update(elapsed);
        }

        public void RenderOverlay(RenderTarget target, Vector2 offset, Vector2 size)
        {
            if (mapState != null)
            {
                List<Character> characters = new List<Character>();

                foreach (Character chr in mapState.Characters)
                {
                    // skip if dead
                    if (chr.IsDead && chr.DeadAnimationFinished
                        || chr.IsPlayerCharacter && chr.Player.IsDead)
                    {
                        continue;
                    }

                    characters.Add(chr);
                }

                if (_currentPlayer != null)
                {
                    for (int i = 0; i < _currentPlayer.Group.Count; i++)
                        characters.Add(_currentPlayer.Group[i]);
                }

                if (RenderShadow)
                {
                    foreach (Character chr in characters)
                    {
                        Vector2 pos = chr.Position + offset;
                        if (chr is Dog)
                        {
#warning TODO don't hardcode
                            pos.x -= 7;
                            pos.y -= 5;
                        }
                        else
                        {
                            pos.x -= 8;
                            pos.y -= 4;
                        }
                        target.DrawSprite(pos, _shadow);
                    }
                }

                float layer = target.Layer;
                float mapHeight = (mapState.Map.MapData.Height * mapState.Map.MapData.TileSize.y);

                foreach (Character chr in characters)
                {
                    Vector2 pos = chr.Position + offset;
                    // align character sprite to bottom center
                    pos.x -= chr.Body.Object.Width / 2;
                    pos.y -= chr.Body.Object.Height;

                    if (!chr.IsDead && SelectedCharacter == chr && chr.Animation == 0)
                        chr.Body.Object.Animation.Frame = ani.Frame;
                    else
                        chr.Body.Object.Animation.Frame = chr.Animation;
                    target.Layer = layer + (chr.Position.y / mapHeight);
                    target.DrawSprite(pos, chr.Body);

                    if (debugRender)
                    {
                        RenderTarget lineTarget = target.GetSubBuffer(new Rect(Vector2.Zero, target.Size));
                        lineTarget.Offset = offset;
                        chr.Path.DebugRender(lineTarget);
                    }
                }

                target.Layer = layer;
            }
        }

        public IMapObject GetObjectAt(Vector2 position)
        {
            if (mapState == null)
                return null;

            IMapObject obj = null;

            foreach (Character chr in mapState.Characters)
            {
                if (chr.IsDead || chr.IsPlayerCharacter && chr.Player.IsDead)
                    continue;

                Vector2 distance = chr.Position - position;
                // align to bottom center
                distance.y -= chr.Body.Object.Height / 2;

                if (distance.Length < 10)
                    obj = chr;
            }

            if (_currentPlayer != null)
            {
                for (int i = 0; i < _currentPlayer.Group.Count; i++)
                {
                    Vector2 distance = _currentPlayer.Group[i].Position - position;
                    // align to bottom center
                    distance.y -= _currentPlayer.Group[i].Body.Object.Height / 2;

                    if (distance.Length < 10)
                        obj = _currentPlayer.Group[i];
                }
            }

            return obj;
        }
    }
}
