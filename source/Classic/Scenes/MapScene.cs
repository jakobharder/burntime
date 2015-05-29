
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Classic.GUI;
using Burntime.Data.BurnGfx;

namespace Burntime.Classic
{
    public class MapScene : Scene, IMapEntranceHandler
    {
        ClassicMapView view;
        MapGuiWindow gui;
        MenuWindow menu;
        Image cursorAni;

        private bool infoMode
        {
            get { if (view.Player == null) return false; return view.Player.InfoMode; }
            set { if (view.Player != null) view.Player.InfoMode = value; }
        }

        public bool debugNoTravel = false;

        public MapScene(Module App)
            : base(App)
        {
            Size = app.Engine.GameResolution;

            view = new ClassicMapView(this, App);
            view.Position = new Vector2(16, 0);
            view.Size = new Vector2(Size.x - 32, Size.y - 40);
            //view.Position = new Vector2(0, 0);
            //view.Size = new Vector2(Size.x, Size.y - 16);
            view.Overlays.Add(new Maps.MapViewOverlayFlags(app));
            view.Overlays.Add(new Maps.MapViewOverlayPlayer(app));
            view.Overlays.Add(new Maps.MapViewOverlayHoverText(app));
            view.Scroll += new EventHandler<MapScrollArgs>(view_Scroll);
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

            cursorAni = new Image(App);
            cursorAni.Background = "burngfxani@syst.raw?24-27";
            cursorAni.Background.Animation.Progressive = false;
            cursorAni.Layer += 59;
            Windows += cursorAni;

            gui = new MapGuiWindow(App);
            gui.Layer += 60;
            Windows += gui;

            BurntimeClassic classic = app as BurntimeClassic;
            debugNoTravel = classic.Settings["debug"].GetBool("no_travel") && classic.Settings["debug"].GetBool("enable_cheats");
        }

        void view_Scroll(object sender, MapScrollArgs e)
        {
            ClassicGame game = app.GameState as ClassicGame;
            game.World.ActivePlayerObj.MapScrollPosition = e.Offset;
        }

        public override bool OnMouseClick(Vector2 Position, MouseButton Button)
        {
            if (Button == MouseButton.Right)
            {
                menu.Show(Position, view.Boundings);
            }
            return true;
        }

        public override bool OnKeyPress(char key)
        {
            if (key == '9')
            {
                view.Player.Character.Food = 9;
                view.Player.Character.Water = 5;
                view.Player.Character.Health = 100;
                return true;
            }

            return base.OnKeyPress(key);
        }

        public override void OnRender(RenderTarget Target)
        {
            if (app.MouseImage != null)
            {
                cursorAni.Position = app.DeviceManager.Mouse.Position + new Vector2(8, 11);

                int layer = Target.Layer;
                Target.Layer = gui.Layer - 1;
                Target.DrawSprite(app.DeviceManager.Mouse.Position, app.MouseImage);
                Target.Layer = layer;
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
        }

        protected override void OnActivateScene(object parameter)
        {
            app.RenderMouse = false;
            app.MouseBoundings = view.Boundings;

            ClassicGame game = app.GameState as ClassicGame;

            if (BurntimeClassic.Instance.PreviousPlayerId != -1 &&
                BurntimeClassic.Instance.PreviousPlayerId != game.CurrentPlayerIndex)
            {
                // play player changed sound
                BurntimeClassic.Instance.Engine.Music.PlayOnce("06_MUS 06_HSC.ogg");
            }
            BurntimeClassic.Instance.PreviousPlayerId = game.CurrentPlayerIndex;

            view.Ways = (WayData)game.World.Ways.WayData;
            view.Map = (MapData)game.World.Map.MapData;
            view.Player = game.World.ActivePlayerObj;
            if (game.World.ActivePlayerObj.RefreshMapScrollPosition)
                view.CenterTo(view.Map.Entrances[game.World.ActivePlayerObj.Location].Area.Center);
            else
                view.ScrollPosition = game.World.ActivePlayerObj.MapScrollPosition;
            gui.UpdatePlayer();

            game.World.ActivePlayerObj.OnMainMap = true;

            game.MainMapView = true;

            // refresh travel/info cursor
            if (infoMode)
                OnMenuInfo();
            else
                OnMenuTravel();
        }

        protected override void OnInactivateScene()
        {
            app.RenderMouse = true;
            app.MouseBoundings = null;
        }

        public override bool OnVKeyPress(Keys key)
        {
            if (key == Keys.Pause)
            {
                app.SceneManager.SetScene("PauseScene");
                return true;
            }

            return false;
        }

        public void OnMenuInfo()
        {
            infoMode = true;
            cursorAni.Background = "burngfxani@syst.raw?20-23";
            cursorAni.Background.Animation.Progressive = false;

            menu.RemoveLine(0);
            menu.AddLine(0, "@burn?360", (CommandHandler)OnMenuTravel);
        }

        public void OnMenuTravel()
        {
            infoMode = false;
            cursorAni.Background = "burngfxani@syst.raw?24-27";
            cursorAni.Background.Animation.Progressive = false;

            menu.RemoveLine(0);
            menu.AddLine(0, "@burn?351", (CommandHandler)OnMenuInfo);
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
                if (infoMode)
                {
                    // only show if current location or owned by player
                    if (clickedLocation.Player == player ||
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
                    if (debugNoTravel)
                    {
                        player.Location = clickedLocation;
                        player.Character.Position = -Vector2.One;
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
