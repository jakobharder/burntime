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
        public Action? OpenInventory { get; set; }
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
        internal const int CompactWidth = 66;
        internal const int CompactPartyPromptY = 59;
        readonly GuiFont _compactFont;
        readonly GuiFont _compactWarningFont;
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

            _compactFont = new GuiFont("font-small.txt", ClassicColors.HudText);
            _compactWarningFont = new GuiFont("font-small.txt", ClassicColors.HudWarning);
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
                DisplayOnly = true,
                TouchAction = () => OpenInventory?.Invoke(),
                MaxDisplayWidth = app.SmallDeviceUi ? CompactWidth - 4 : 0
            };
            _playerFace.Layer++;
            ApplyLayout();
        }

        public override void OnResizeScreen(bool reload = false)
        {
            base.OnResizeScreen(reload);

            Size = app.Engine.Resolution.Game;
            ApplyLayout();
        }

        void ApplyLayout()
        {
            if (app.SmallDeviceUi)
            {
                _uiElement1.Hide();
                _uiElement2.Hide();
                _playerFace.Position = new Vector2(2, 2);
                return;
            }
            _uiElement1.Show();
            _uiElement2.Show();
            _uiElement1.Position = new Vector2(Size.x / 2 - 60, Size.y - 40);
            _uiElement2.Position = new Vector2(Size.x / 2 - 42, 0);
            _playerFace.Position = new Vector2(Size.x / 2 - 31, Size.y - 56);
        }

        public override void OnUpdate(float elapsed)
        {
            base.OnUpdate(elapsed);
            if (!app.SmallDeviceUi || app.GameState is not ClassicGame game || game.World.ActivePlayer < 0)
                return;
            Player player = game.World.ActivePlayerObj;
            bool hasSelectionControls = (app.LastInputMode is InputMode.Keyboard or InputMode.Gamepad) &&
                (player.Party.Count > 1 || player.SelectedCharacter != null && !player.Party.Contains(player.SelectedCharacter));
            int portraitWidth = hasSelectionControls ? 54 : CompactWidth - 4;
            _playerFace.MaxDisplayWidth = portraitWidth;
            _playerFace.Position = new Vector2((CompactWidth - portraitWidth) / 2, 2);
        }

        public override void SetMapRenderArea(MapView mapView, Vector2 size)
        {
            if (app.SmallDeviceUi)
            {
                mapView.SetViewport(new Vector2(CompactWidth, 0),
                    new Vector2(System.Math.Max(1, size.x - CompactWidth), size.y));
                return;
            }
            int sideMargin = ((BurntimeClassic)app).IpadLayout ? 0 : 16;
            mapView.SetViewport(new Vector2(sideMargin, 0),
                new Vector2(size.x - sideMargin * 2, size.y - 40));
        }

        public override void OnRender(RenderTarget Target)
        {
            base.OnRender(Target);
            if (app.SmallDeviceUi)
            {
                RenderCompact(Target);
                return;
            }

            if (!((BurntimeClassic)app).IpadLayout)
            {
                Target.RenderRect(new Vector2(0, 0), new Vector2(16, Size.y - 40), new PixelColor(0, 0, 0));
                Target.RenderRect(new Vector2(Size.x - 16, 0), new Vector2(17, Size.y - 40), new PixelColor(0, 0, 0));
            }
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

        void RenderCompact(RenderTarget target)
        {
            target.RenderRect(Vector2.Zero, new Vector2(CompactWidth, Size.y), PixelColor.Black);
            target.RenderRect(new Vector2(CompactWidth - 1, 0), new Vector2(1, Size.y),
                new PixelColor(48, 48, 72));
            target.Layer += 10;
            ClassicGame game = (ClassicGame)app.GameState;
            Player player = game.World.ActivePlayerObj;
            Character selected = player.SelectedCharacter ?? player.Character;
            const int left = 4;
            const int width = CompactWidth - left * 2;
            _playerFont.DrawText(target, new Vector2(CompactWidth / 2, 60),
                FitText(_playerFont, selected.Name, width), TextAlignment.Center, VerticalTextAlignment.Top);

            target.RenderRect(new Vector2(left - 1, 70), new Vector2(width + 2, 6),
                new PixelColor(56, 56, 80));
            target.RenderRect(new Vector2(left, 71),
                new Vector2(width * System.Math.Clamp(selected.Health, 0, 100) / 100, 4),
                new PixelColor(240, 64, 56));

            var day = new TextHelper(app, "burn");
            day.AddArgument("|A", game.World.Day);
            _compactFont.DrawText(target, new Vector2(left, 84), day[404]);
            if (player.Party.Count > 1)
            {
                int index = 0;
                for (int i = 0; i < player.Party.Count; i++)
                    if (player.Party[i] == selected) { index = i; break; }
                _compactFont.DrawText(target, new Vector2(CompactWidth - left, 84),
                    player.SingleMode ? $"{index + 1}/{player.Party.Count}"
                        : $"{app.ResourceManager.GetString("burn?356")} {player.Party.Count}", TextAlignment.Right);
            }
            target.RenderRect(new Vector2(left, 90), new Vector2(width, 3), new PixelColor(40, 40, 56));
            target.RenderRect(new Vector2(left, 90),
                new Vector2((int)(System.Math.Clamp(game.World.Time, 0, 1) * width), 3),
                new PixelColor(240, 64, 56));

            var text = new TextHelper(app, "newburn");
            int water = player.Party.GetLowestWaterWithInventory();
            int food = player.Party.GetLowestFoodWithInventory();
            string status = "";
            bool warning = true;
            if (water == 0) status = text[38];
            else if (food == 0) status = text[39];
            else if (ExpectedTravelDays > 0 && water < ExpectedTravelDays) status = text[38];
            else if (ExpectedTravelDays > 0 && food < ExpectedTravelDays) status = text[39];
            else if (game.World.ActiveLocationObj.Danger is { } danger)
            {
                status = danger.InfoString;
                warning = player.Party.IsInDanger();
            }
            GuiFont statusFont = warning ? _compactWarningFont : _compactFont;
            statusFont.DrawText(target, new Vector2(left, 102), FitText(statusFont, status, width));
            if (ExpectedTravelDays > 0)
            {
                text.AddArgument("|J", ExpectedTravelDays);
                _compactFont.DrawText(target, new Vector2(left, 113), FitText(_compactFont, text[104], width));
            }
        }

        static string FitText(GuiFont font, string text, int width)
        {
            if (font.GetWidth(text) <= width) return text;
            while (text.Length > 0 && font.GetWidth(text + "...") > width)
                text = text[..^1];
            return text + "...";
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
