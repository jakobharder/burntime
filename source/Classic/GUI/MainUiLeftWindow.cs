using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Classic.Logic;
using Burntime.Classic.GUI;

namespace Burntime.Classic
{
    public class MainUiLeftWindow : IMapGuiWindow
    {
        GuiFont font;
        GuiFont playerColor;
        Player player;
        ProgressWindow foodField;
        ProgressWindow waterField;
        ProgressWindow nameField;
        ProgressWindow timeField;
        readonly FaceWindow face;

        public CommandHandler ShowInventory;
        public CommandHandler SwitchMap;
        public CommandHandler NextTurn;
        public CommandHandler ShowStatistics;

        public MainUiLeftWindow(Module App)
            : base(App)
        {
            Size = app.Engine.Resolution.Game;

            font = new GuiFont(BurntimeClassic.FontName, PixelColor.White);
            playerColor = new GuiFont(BurntimeClassic.FontName, PixelColor.White);

            Windows += face = new(App)
            {
                Position = new Vector2(0, 0),
                FaceID = 0,
                DisplayOnly = true
            };

            Windows += nameField = new(app, "gfx/ui/progress_glass.png", "gfx/ui/progress_glass.png")
            {
                Position = new Vector2(3, 56),
                Color = new PixelColor(208, 0, 0),
                Progress = 1.0f,
                Border = 0
            };

            Windows += timeField = new(app, "gfx/ui/progress_glass.png", "gfx/ui/progress_glass.png")
            {
                Position = new Vector2(3, Size.y - 30),
                Color = new PixelColor(208, 0, 0),
                Progress = 1.0f,
                Border = 0
            };


            //// food field
            //foodField = new ProgressWindow(app, "gfx/ui/progress.png", "gfx/ui/progress_glass.png");
            //foodField.Position = new Vector2(15, Size.y - 15);
            //foodField.Color = new PixelColor(240, 64, 56);
            //foodField.Progress = 1.0f;
            //foodField.Border = 2;//1.5
            //foodField.Layer++;
            //Windows += foodField;

            //// water field
            //waterField = new ProgressWindow(app, "gfx/ui/progress.png", "gfx/ui/progress_glass.png");
            //waterField.Position = new Vector2(57, Size.y - 15);
            //waterField.Color = new PixelColor(120, 132, 184);
            //waterField.Progress = 1.0f;
            //waterField.Border = 2;//1.5
            //waterField.Layer++;
            //Windows += waterField;

            //Button button = new Button(app);
            //button.Image = "gfx/ui/start_button_level1.png";
            //button.HoverImage = "gfx/ui/start_button_level1_down.png";
            //button.Position = new Vector2(2, Size.y - 14);
            //button.Command += new CommandHandler(OnShowInventory);
            //button.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            //button.Text = "I";
            //Windows += button;

            //button = new Button(app);
            //button.Image = "gfx/ui/start_button_level1.png";
            //button.HoverImage = "gfx/ui/start_button_level1_down.png";
            //button.Position = new Vector2(Size.x / 2 - 42 - 14, Size.y - 14);
            //button.Command += new CommandHandler(OnSwitchMap);
            //button.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            //button.Text = "M";
            //Windows += button;

            //button = new Button(app);
            //button.Image = "gfx/ui/start_button_level1.png";
            //button.HoverImage = "gfx/ui/start_button_level1_down.png";
            //button.Position = new Vector2(Size.x / 2 + 42 + 2, Size.y - 14);
            //button.Command += new CommandHandler(OnShowStatistics);
            //button.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            //button.Text = "S";
            //Windows += button;

            //button = new Button(app);
            //button.Image = "gfx/ui/start_button_level1.png";
            //button.HoverImage = "gfx/ui/start_button_level1_down.png";
            //button.Position = new Vector2(Size.x - 14, Size.y - 14);
            //button.Command += new CommandHandler(OnNextTurn);
            //button.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            //button.Text = "T";
            //Windows += button;
        }

        public override void SetMapRenderArea(MapView mapView, Vector2 size)
        {
            mapView.Position = new Vector2(66, 0);
            mapView.Size = new Vector2(size.x - 66, size.y);
        }

        public override void OnRender(RenderTarget Target)
        {
            Target.RenderRect(new Vector2(0, 0), new Vector2(66, Size.y), new PixelColor(0, 0, 0));

            base.OnRender(Target);

            //Target.Layer++;
            //ClassicGame game = app.GameState as ClassicGame;

            //Vector2 health = new Vector2(Size.x / 2 + 64, Size.y - 30);
            //int fullBar = 75;
            //int healthBar = fullBar * game.World.ActivePlayerObj.Character.Health / 100;
            //Target.RenderRect(health, new Vector2(healthBar, 5), new PixelColor(240, 64, 56));

            //Vector2 timebar = new Vector2(Size.x / 2 - 30, 2);
            //int dayTime = (int)(game.World.Time * 60);
            //Target.RenderRect(timebar, new Vector2(dayTime, 3), new PixelColor(240, 64, 56));

            //Vector2 name = new Vector2(Size.x / 2 - 97, Size.y - 30);
            //playerColor.DrawText(Target, name, this.name, TextAlignment.Center, VerticalTextAlignment.Top);

            //Vector2 day = new Vector2(Size.x / 2 + 100, Size.y - 15);
            //TextHelper txt = new TextHelper(app, "burn");
            //txt.AddArgument("|A", game.World.Day);
            //font.DrawText(Target, day, txt[404], TextAlignment.Center, VerticalTextAlignment.Top);

            if (player != null)
            {
                int fontSpacing = 10;
                font.DrawText(Target, new Vector2(30, 70), "NG. " + player.Character.Food, TextAlignment.Left, VerticalTextAlignment.Top);
                font.DrawText(Target, new Vector2(30, 70 + fontSpacing), "WA. " + player.Character.Water, TextAlignment.Left, VerticalTextAlignment.Top);
            }
        }

        public override void OnUpdate(float Elapsed)
        {
            base.OnUpdate(Elapsed);

            if (player != null)
            {
                //nameField.Progress = player.Character.Health / 100.0f;
                //foodField.Progress = player.Character.Food / (float)player.Character.MaxFood;
                //foodField.Text = player.Character.Food.ToString();
                //waterField.Progress = player.Character.Water / (float)player.Character.MaxWater;
                //waterField.Text = player.Character.Water.ToString();
            }

            ClassicGame game = app.GameState as ClassicGame;
            timeField.Progress = game.World.Time;

            TextHelper txt = new TextHelper(app, "burn");
            txt.AddArgument("|A", game.World.Day);
            timeField.Text = txt[404];
        }

        public override void UpdatePlayer()
        {
            ClassicGame game = app.GameState as ClassicGame;
            if (game.World.ActivePlayer == -1)
            {
                nameField.Text = "";
                nameField.Progress = 0;
                foodField.Text = "";
                foodField.Progress = 0;
                waterField.Text = "";
                waterField.Progress = 0;
                player = null;
                face.FaceID = -1;
                return;
            }

            player = game.World.Players[game.World.ActivePlayer];

            nameField.Text = player.Name;
            //nameField.Color = player.Color;
            playerColor = new GuiFont(BurntimeClassic.FontName, player.Color);
            face.FaceID = player.FaceID;
        }

        private void OnShowInventory()
        {
        }

        private void OnSwitchMap()
        {
        }

        private void OnShowStatistics()
        {
        }

        private void OnNextTurn()
        {
        }
    }
}