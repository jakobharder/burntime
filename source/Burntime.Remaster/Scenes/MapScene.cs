using System;
using Burntime.Data.BurnGfx;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Remaster.GUI;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster
{
    public class MapScene : Scene, IMapEntranceHandler
    {
        ClassicMapView view;
        IMapGuiWindow gui;
        MenuWindow menu;
        Image _cursorAni;
        readonly DialogWindow _dialog;

        private bool _infoMode
        {
            get { if (view.Player == null) return false; return view.Player.InfoMode; }
            set { if (view.Player != null) view.Player.InfoMode = value; }
        }

        bool _debugNoTravel = false;

        public MapScene(Module App)
            : base(App)
        {
            Size = app.Engine.Resolution.Game;
            BurntimeClassic classic = app as BurntimeClassic;

            gui = classic.NewGui ? new MainUiLeftWindow(App) : new MainUiOriginalWindow(App);

            view = new ClassicMapView(this, App);
            gui.SetMapRenderArea(view, Size);

            view.Overlays.Add(new Maps.MapViewOverlayFlags(app));
            view.Overlays.Add(new Maps.MapViewOverlayPlayer(app));
            view.Overlays.Add(new Maps.MapViewOverlayHoverText(app));
            view.Scroll += new EventHandler<MapScrollArgs>(view_Scroll);
            view.ContextMenu += View_OnContextMenu;
            Windows += view;

            menu = new MenuWindow(App);
            menu.Layer += 50;
            menu.AddLine("@burn?351", (CommandHandler)OnMenuInfo);
            menu.AddLine("@burn?367", (CommandHandler)OnMenuInventory);
            menu.AddLine("@burn?359", (CommandHandler)OnMenuStatistics);
            menu.AddLine("@burn?361", (CommandHandler)OnMenuOptions);
            menu.AddLine("@burn?357", (CommandHandler)OnMenuTurn);
            menu.Hide();
            Windows += menu;

            _cursorAni = new Image(App);
            _cursorAni.Background = "burngfxani@syst.raw?24-27";
            _cursorAni.Background.Animation.Progressive = false;
            _cursorAni.Layer += 59;
            Windows += _cursorAni;

            gui.Layer += classic.NewGui ? 40 : 60;
            Windows += gui;

            _dialog = new DialogWindow(app);
            _dialog.Position = view.Position + (view.Size - _dialog.Size) / 2 - new Vector2(0, 10);
            _dialog.Hide();
            _dialog.Layer += 55;
            _dialog.WindowHide += new EventHandler(OnDialogHidden);
            _dialog.WindowShow += new EventHandler(OnDialogShown);
            Windows += _dialog;
        }

        private void View_OnContextMenu(Vector2 position, MouseButton button)
        {
            menu.Show(position, view.Boundings);
        }

        void OnDialogShown(object? sender, EventArgs e)
        {
            _cursorAni.Hide();
        }

        void OnDialogHidden(object? sender, EventArgs e)
        {
            _cursorAni.Show();
        }

        public override void OnResizeScreen()
        {
            base.OnResizeScreen();

            Size = app.Engine.Resolution.Game;
            gui.SetMapRenderArea(view, Size);
            app.MouseBoundings = view.Boundings;
        }

        void view_Scroll(object sender, MapScrollArgs e)
        {
            ClassicGame game = app.GameState as ClassicGame;
            game.World.ActivePlayerObj.MapScrollPosition = e.Offset;
        }

        private string enteredText = string.Empty;
        public override bool OnKeyPress(char key)
        {
            if (app.GameState is not ClassicGame game)
                return false;

            if (!game.CheatsEnabled)
            {
                enteredText += key;
                if (enteredText.ToLower().EndsWith("petko", StringComparison.OrdinalIgnoreCase))
                {
                    game.CheatsEnabled = true;

                    var cheatMessage = Conversation.Simple(game.ResourceManager, "newburn?46");
                    _dialog.SetCharacter(view.Player.Character, cheatMessage);
                    _dialog.Show();
                }
                if (enteredText.Length > 30)
                    enteredText = enteredText[5..];
            }
            else
            {
                if (key == '9')
                {
                    view.Player.Character.Food = 9;
                    view.Player.Character.Water = 5;
                    view.Player.Character.Health = 100;
                    return true;
                }
                else if (key == 'q')
                {
                    ToggleFastTravel();
                    return true;
                }
            }

            return base.OnKeyPress(key);
        }

        private void ToggleFastTravel()
        {
            if (!_debugNoTravel)
                OnFastTravel();
            else
                OnMenuTravel();
        }

        public override void OnRender(RenderTarget Target)
        {
            if (app.MouseImage != null)
            {
                _cursorAni.Position = app.DeviceManager.Mouse.Position + new Vector2(8, 11);

                if (!BurntimeClassic.Instance.NewGui)
                {
                    var layer = Target.Layer;
                    Target.Layer = gui.Layer - 1;
                    Target.DrawSprite(app.DeviceManager.Mouse.Position, app.MouseImage);
                    Target.Layer = layer;
                }
            }
        }

        public override void OnUpdate(float Elapsed)
        {
            ClassicGame game = app.GameState as ClassicGame;
            game.World.Update(Elapsed);

            if (game.World.Time <= 0)
            {
                app.ActiveClient.Finish();
                app.SceneManager.SetScene("WaitScene");
            }

            var hoverLocation = view.ActiveEntrance >= 0 ? BurntimeClassic.Instance.Game.World.Locations[view.ActiveEntrance] : null;
            var player = BurntimeClassic.Instance.Game.World.ActivePlayerObj;
            gui.ExpectedTravelDays = hoverLocation is null ? 0 : player.GetTravelDays(player.Location, hoverLocation);
        }

        protected override void OnActivateScene(object parameter)
        {
            if (!BurntimeClassic.Instance.NewGui)
            {
                app.RenderMouse = false;
            }
            app.MouseBoundings = view.Boundings;

            ClassicGame game = app.GameState as ClassicGame;

            if (BurntimeClassic.Instance.PreviousPlayerId != -1 &&
                BurntimeClassic.Instance.PreviousPlayerId != game.CurrentPlayerIndex)
            {
                // play player changed sound
                BurntimeClassic.Instance.Engine.Music.PlayOnce("sounds/change.ogg");
            }
            BurntimeClassic.Instance.PreviousPlayerId = game.CurrentPlayerIndex;

            view.Ways = (WayData)game.World.Ways.WayData;
            view.Map = (MapData)game.World.Map.MapData;
            view.Player = game.World.ActivePlayerObj;
            //if (game.World.ActivePlayerObj.RefreshMapScrollPosition)
                view.CenterTo(view.Map.Entrances[game.World.ActivePlayerObj.Location].Area.Center);
            //else
            //    view.ScrollPosition = game.World.ActivePlayerObj.MapScrollPosition;
            gui.UpdatePlayer();

            game.World.ActivePlayerObj.OnMainMap = true;

            game.MainMapView = true;

            // refresh travel/info cursor
            if (_infoMode)
                OnMenuInfo();
            else
                OnMenuTravel();
        }

        protected override void OnInactivateScene()
        {
            if (!BurntimeClassic.Instance.NewGui)
            {
                app.RenderMouse = true;
            }
            app.MouseBoundings = null;
        }

        public override bool OnVKeyPress(SystemKey key)
        {
            if (key == SystemKey.Pause)
            {
                app.SceneManager.SetScene("PauseScene");
                return true;
            }

            return false;
        }

        public void OnMenuInfo()
        {
            _infoMode = true;
            _debugNoTravel = false;
            _cursorAni.Background = "burngfxani@syst.raw?20-23";
            _cursorAni.Background.Animation.Progressive = false;

            menu.RemoveLine(0);
            menu.AddLine(0, "@burn?360", (CommandHandler)OnMenuTravel);
        }

        public void OnMenuTravel()
        {
            _infoMode = false;
            _debugNoTravel = false;
            _cursorAni.Background = "burngfxani@syst.raw?24-27";
            _cursorAni.Background.Animation.Progressive = false;

            menu.RemoveLine(0);
            menu.AddLine(0, "@burn?351", (CommandHandler)OnMenuInfo);
        }

        public void OnFastTravel()
        {
            _infoMode = false;
            _debugNoTravel = true;
            _cursorAni.Background = "burngfxani@syst.raw?4-7";
            _cursorAni.Background.Animation.Progressive = false;

            menu.RemoveLine(0);
            menu.AddLine(0, "@burn?360", (CommandHandler)OnMenuTravel);
        }

        public void OnMenuInventory()
        {
            BurntimeClassic classic = app as BurntimeClassic;
            classic.InventoryBackground = -1;
            classic.InventoryRoom = null;
            classic.PickItems = null;
            app.SceneManager.SetScene("InventoryScene", classic.Game.World.ActivePlayerObj.Character);
        }

        public void OnMenuStatistics()
        {
            app.SceneManager.SetScene("StatisticsScene");
        }

        public void OnMenuOptions()
        {
            app.SceneManager.SetScene("OptionsScene");
        }

        public void OnMenuTurn()
        {
            app.SceneManager.SetScene("WaitScene");
            app.SceneManager.BlockBlendIn();
            app.ActiveClient.Finish();
            app.SceneManager.UnblockBlendIn();
        }

        public String GetEntranceTitle(int Number)
        {
            return app.ResourceManager.GetString("burn?" + Number);
        }

        public bool OnClickEntrance(int Number, MouseButton Button)
        {
            Logic.Player player = BurntimeClassic.Instance.Game.World.ActivePlayerObj;
            Logic.Location clickedLocation = BurntimeClassic.Instance.Game.World.Locations[Number];

            if (Button == MouseButton.Left)
            {
                if (_infoMode)
                {
                    // only show if current location or owned by player
                    if (BurntimeClassic.Instance.Game.CheatsEnabled ||
                        clickedLocation.Player == player ||
                        (!clickedLocation.IsCity && Number == player.Location.Id && clickedLocation.Player == null))
                    {
                        BurntimeClassic.Instance.InfoCity = Number;
                        app.SceneManager.SetScene("InfoScene");
                    }
                    else
                        return false;
                }
                else
                {
                    if (_debugNoTravel)
                    {
                        player.Location = clickedLocation;
                        player.Character.Position = clickedLocation.EntryPoint;
                        player.RefreshScrollPosition = true;

                        ToggleFastTravel();
                    }

                    if (player.Location == clickedLocation)
                    {
                        app.SceneManager.SetScene("LocationScene");
                    }
                    else if (player.Location.Neighbors.Contains(clickedLocation))
                    {
                        // check travel allowance
                        if (player.CanTravel(player.Location, clickedLocation))
                        {
                            player.Travel(clickedLocation);
                            OnMenuTurn();
                        }
                    }
                }
                return true;
            }
            return false;
        }
    }
}
