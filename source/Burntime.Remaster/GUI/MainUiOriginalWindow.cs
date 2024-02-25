using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Remaster.Logic;
using Burntime.Remaster.GUI;

namespace Burntime.Remaster
{
    public abstract class IMapGuiWindow : Container
    {
        public IMapGuiWindow(Module App)
            : base(App)
        {

        }

        public abstract void UpdatePlayer();
        public abstract void SetMapRenderArea(MapView mapView, Vector2 size);
        public abstract int ExpectedTravelDays { get; set; }
    }

    public class MainUiOriginalWindow : IMapGuiWindow
    {
        readonly GuiFont _standardFont;
        readonly GuiFont _warningFont;
        GuiFont _playerFont;
        readonly FaceWindow _playerFace;
        string _playerName;

        readonly Image _uiElement1;
        readonly Image _uiElement2;

        public override int ExpectedTravelDays { get; set; } = 0;

        public MainUiOriginalWindow(Module App)
            : base(App)
        {
            Size = app.Engine.Resolution.Game;

            _standardFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            _playerFont = new GuiFont(BurntimeClassic.FontName, PixelColor.White);
            _warningFont = _standardFont;//new GuiFont(BurntimeClassic.FontName, new PixelColor(252, 180, 56));

            Windows += _uiElement1 = new Image(App)
            {
                Background = "munt.raw?1",
                Position = new Vector2(Size.x / 2 - 60, Size.y - 40)
            };

            Windows += _uiElement2 = new Image(App)
            {
                Background = "munt.raw?22",
                Position = new Vector2(Size.x / 2 - 42, 0)
            };

            Windows += _playerFace = new FaceWindow(App)
            {
                Position = new Vector2(Size.x / 2 - 31, Size.y - 56),
                FaceID = 0,
                DisplayOnly = true
            };
            _playerFace.Layer++;
        }

        public override void OnResizeScreen()
        {
            base.OnResizeScreen();

            Size = app.Engine.Resolution.Game;
            _uiElement1.Position = new Vector2(Size.x / 2 - 60, Size.y - 40);
            _uiElement2.Position = new Vector2(Size.x / 2 - 42, 0);
            _playerFace.Position = new Vector2(Size.x / 2 - 31, Size.y - 56);
        }

        public override void SetMapRenderArea(MapView mapView, Vector2 size)
        {
            mapView.Position = new Vector2(16, 0);
            mapView.Size = new Vector2(size.x - 32, size.y - 40);
        }

        public override void OnRender(RenderTarget Target)
        {
            base.OnRender(Target);

            Target.RenderRect(new Vector2(0, 0), new Vector2(16, Size.y - 40), new PixelColor(0, 0, 0));
            Target.RenderRect(new Vector2(Size.x - 16, 0), new Vector2(17, Size.y - 40), new PixelColor(0, 0, 0));
            Target.RenderRect(new Vector2(0, Size.y - 40), new Vector2(Size.x + 1, 41), new PixelColor(0, 0, 0));

            Target.Layer++;
            ClassicGame game = app.GameState as ClassicGame;

            Vector2 health = new Vector2(Size.x / 2 + 64, Size.y - 30);
            int fullBar = 75;
            int healthBar = fullBar * game.World.ActivePlayerObj.Character.Health / 100;
            Target.RenderRect(health, new Vector2(healthBar, 6), new PixelColor(240, 64, 56));

            Vector2 timebar = new Vector2(Target.Width / 2 - 30, 2);
            int dayTime = (int)(game.World.Time * 60);
            Target.Layer++;
            Target.RenderRect(timebar, new Vector2(dayTime, 3), new PixelColor(240, 64, 56));
            Target.Layer--;

            var name = new Vector2(Size.x / 2 - 97, Size.y - 30);
            _playerFont.DrawText(Target, name, this._playerName, TextAlignment.Center, VerticalTextAlignment.Top);

            var txt = new TextHelper(app, "newburn");
            Vector2 nutrition = new(Size.x / 2 - 97, Size.y - 17);

            var playerGroup = game.World.ActivePlayerObj.Group;
            var currentLocation = game.World.ActiveLocationObj;
            int totalWaterReserve = playerGroup.GetLowestWaterWithInventory();
            int totalFoodReserve = playerGroup.GetLowestFoodWithInventory();

            if (totalWaterReserve < ExpectedTravelDays)
            {
                _warningFont.DrawText(Target, nutrition, txt[38],
                    TextAlignment.Center, VerticalTextAlignment.Top);
            }
            else if (totalFoodReserve < ExpectedTravelDays)
            {
                _warningFont.DrawText(Target, nutrition, txt[39],
                    TextAlignment.Center, VerticalTextAlignment.Top);
            }
            else if (playerGroup.IsInDanger())
            {
                _warningFont.DrawText(Target, nutrition, currentLocation.Danger.InfoString,
                    TextAlignment.Center, VerticalTextAlignment.Top);
            }
            else if (totalWaterReserve == 0)
            {
                _warningFont.DrawText(Target, nutrition, txt[38],
                    TextAlignment.Center, VerticalTextAlignment.Top);
            }
            else if (totalFoodReserve == 0)
            {
                _warningFont.DrawText(Target, nutrition, txt[39],
                    TextAlignment.Center, VerticalTextAlignment.Top);
            }
            else if (currentLocation.Danger is not null)
            {
                _standardFont.DrawText(Target, nutrition, currentLocation.Danger.InfoString,
                    TextAlignment.Center, VerticalTextAlignment.Top);
            }

            var day = new Vector2(Size.x / 2 + 100, Size.y - 16);
            txt = new TextHelper(app, "burn");
            txt.AddArgument("|A", game.World.Day);
            _standardFont.DrawText(Target, day, txt[404], TextAlignment.Center, VerticalTextAlignment.Top);
        }

        public override void UpdatePlayer()
        {
            ClassicGame game = app.GameState as ClassicGame;
            if (game.World.ActivePlayer == -1)
            {
                _playerFace.FaceID = -1;
                _playerName = "";
                return;
            }

            Player player = game.World.Players[game.World.ActivePlayer];

            _playerFace.FaceID = player.FaceID;
            _playerName = player.Name;
            _playerFont = new GuiFont(BurntimeClassic.FontName, player.Color);
        }
    }
}