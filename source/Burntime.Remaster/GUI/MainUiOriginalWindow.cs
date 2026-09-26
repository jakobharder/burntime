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
        readonly GuiFont _promptFont;
        GuiFont _playerFont;
        readonly FaceWindow _playerFace;

        public string PromptText { get; set; } = "";

        readonly Image _uiElement1;
        readonly Image _uiElement2;

        public override int ExpectedTravelDays { get; set; } = 0;

        public MainUiOriginalWindow(Module App)
            : base(App)
        {
            Size = app.Engine.Resolution.Game;

            _standardFont = new GuiFont(BurntimeClassic.FontName, ClassicColors.HudText);
            _playerFont = new GuiFont(BurntimeClassic.FontName, PixelColor.White);
            _warningFont = new GuiFont(BurntimeClassic.FontName, ClassicColors.HudWarning);
            _promptFont = new GuiFont(BurntimeClassic.FontName, ClassicColors.LightGray);

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

        public override void OnResizeScreen(bool reload = false)
        {
            base.OnResizeScreen(reload);

            Size = app.Engine.Resolution.Game;
            _uiElement1.Position = new Vector2(Size.x / 2 - 60, Size.y - 40);
            _uiElement2.Position = new Vector2(Size.x / 2 - 42, 0);
            _playerFace.Position = new Vector2(Size.x / 2 - 31, Size.y - 56);
        }

        public override void SetMapRenderArea(MapView mapView, Vector2 size)
        {
            mapView.SetViewport(new Vector2(16, 0),
                new Vector2(size.x - 32, size.y - 40));
        }

        public override void OnRender(RenderTarget Target)
        {
            base.OnRender(Target);

            Target.RenderRect(new Vector2(0, 0), new Vector2(16, Size.y - 40), new PixelColor(0, 0, 0));
            Target.RenderRect(new Vector2(Size.x - 16, 0), new Vector2(17, Size.y - 40), new PixelColor(0, 0, 0));
            Target.RenderRect(new Vector2(0, Size.y - 40), new Vector2(Size.x + 1, 41), new PixelColor(0, 0, 0));

            Target.Layer++;
            ClassicGame game = app.GameState as ClassicGame;

            int inputPromptMargin = app.LastInputMode == InputMode.Keyboard ? 10 : 0;

            Vector2 health = new Vector2(Size.x / 2 + 64, Size.y - 30);
            int fullBar = 75;
            Player player = game.World.ActivePlayerObj;
            Character selectedCharacter = player.SelectedCharacter ?? player.Character;
            int healthBar = fullBar * System.Math.Clamp(selectedCharacter.Health, 0, 100) / 100;
            Target.RenderRect(health, new Vector2(healthBar, 6), new PixelColor(240, 64, 56));

            Vector2 timebar = new Vector2(Target.Width / 2 - 30, 2);
            int dayTime = (int)(game.World.Time * 60);
            Target.Layer++;
            Target.RenderRect(timebar, new Vector2(dayTime, 3), new PixelColor(240, 64, 56));
            Target.Layer--;

            Target.Layer += 10;

            int portraitLeft = Size.x / 2 - 31;
            int leftTextRight = portraitLeft - 24;
            var name = new Vector2(leftTextRight, Size.y - 30);
            string playerName = selectedCharacter.Name;
            if (player.Party.Count > 1)
            {
                int selectedIndex = 0;
                for (int i = 0; i < player.Party.Count; i++)
                {
                    if (player.Party[i] == selectedCharacter)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
                string partyPosition = $"{selectedIndex + 1}/{player.Party.Count}";
                int nameWidth = _playerFont.GetWidth(playerName);
                _standardFont.DrawText(Target,
                    new Vector2(leftTextRight - nameWidth - _standardFont.GetWidth(" "), name.y),
                    partyPosition, TextAlignment.Right, VerticalTextAlignment.Top);
            }
            _playerFont.DrawText(Target, name, playerName,
                TextAlignment.Right, VerticalTextAlignment.Top);

            var txt = new TextHelper(app, "newburn");
            int secondLineY = Size.y - 16;

            if (ExpectedTravelDays > 0)
            {
                txt.AddArgument("|J", ExpectedTravelDays);
                string travelDuration = txt[104];
                Vector2 duration = new(Size.x / 2 + 61 + inputPromptMargin, secondLineY);
                _standardFont.DrawText(Target, duration, travelDuration,
                    TextAlignment.Left, VerticalTextAlignment.Top);
            }

            Vector2 nutrition = new(leftTextRight - inputPromptMargin, secondLineY);
            var playerGroup = game.World.ActivePlayerObj.Party;
            var currentLocation = game.World.ActiveLocationObj;
            int totalWaterReserve = playerGroup.GetLowestWaterWithInventory();
            int totalFoodReserve = playerGroup.GetLowestFoodWithInventory();

            if (totalWaterReserve == 0)
            {
                _warningFont.DrawText(Target, nutrition, txt[38],
                    TextAlignment.Right, VerticalTextAlignment.Top);
            }
            else if (totalFoodReserve == 0)
            {
                _warningFont.DrawText(Target, nutrition, txt[39],
                    TextAlignment.Right, VerticalTextAlignment.Top);
            }
            else if (ExpectedTravelDays > 0 && totalWaterReserve < ExpectedTravelDays)
            {
                _warningFont.DrawText(Target, nutrition, txt[38],
                    TextAlignment.Right, VerticalTextAlignment.Top);
            }
            else if (ExpectedTravelDays > 0 && totalFoodReserve < ExpectedTravelDays)
            {
                _warningFont.DrawText(Target, nutrition, txt[39],
                    TextAlignment.Right, VerticalTextAlignment.Top);
            }
            else if (playerGroup.IsInDanger())
            {
                _warningFont.DrawText(Target, nutrition, currentLocation.Danger.InfoString,
                    TextAlignment.Right, VerticalTextAlignment.Top);
            }
            else if (currentLocation.Danger is not null)
            {
                _standardFont.DrawText(Target, nutrition, currentLocation.Danger.InfoString,
                    TextAlignment.Right, VerticalTextAlignment.Top);
            }
            else
            {
                txt = new TextHelper(app, "burn");
                txt.AddArgument("|A", game.World.Day);
                _standardFont.DrawText(Target, nutrition, txt[404],
                    TextAlignment.Right, VerticalTextAlignment.Top);
            }

            if (!string.IsNullOrEmpty(PromptText))
            {
                var prompt = new Vector2(4, Size.y - 38);
                _promptFont.DrawText(Target, prompt, PromptText, TextAlignment.Left, VerticalTextAlignment.Top);
            }
        }

        public override void UpdatePlayer()
        {
            ClassicGame game = app.GameState as ClassicGame;
            if (game.World.ActivePlayer == -1)
            {
                _playerFace.FaceID = -1;
                return;
            }

            Player player = game.World.Players[game.World.ActivePlayer];

            _playerFace.FaceID = player.SelectedCharacter?.FaceID ?? player.FaceID;
            _playerFont = new GuiFont(BurntimeClassic.FontName, player.Color);
        }
    }
}
