using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Framework.Network;

namespace Burntime.Remaster.Scenes
{
    class WaitScene : Scene, IMapMusicContinuationScene, ISceneTransitionBridge
    {
        GuiFont font;
        float timer = 0;
        const float WAIT_DISPLAY_DELAY = 10;

        ITurnNews news;
        bool hadDeaths;

        public WaitScene(Module App)
            : base(App)
        {
            Size = new Burntime.Platform.Vector2(320, 200);
            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
            //Background = new SpriteImage(App, "blz.pac");
            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(255, 255, 255));
        }

        public override void OnResizeScreen(bool reload = false)
        {
            base.OnResizeScreen(reload);

            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
        }

        public override void OnRender(RenderTarget Target)
        {
            if (timer >= WAIT_DISPLAY_DELAY)
                font.DrawText(Target, new Vector2(160, 100), new GuiString("@newburn?10"), TextAlignment.Center, VerticalTextAlignment.Center);
        }

        public override void OnUpdate(float Elapsed)
        {
            BurntimeClassic classic = app as BurntimeClassic;

            classic.ActiveClient = GameClient.NoClient;

            news = classic.GameServer.PopNews();

            if (news != null)
            {
                if (news is DeathNews death)
                {
                    hadDeaths = true;
                    classic.SetImageScene("scenes/death.txt", subtitleArgument: death.Name,
                        finishClient: true);
                }
                else if (news is VictoryNews victory)
                {
                    hadDeaths = false;
                    classic.SetImageScene("scenes/victory.txt", subtitleArgument: victory.Name,
                        finishClient: true);
                }
            }
            else
            {
                bool gameOver = true;

                for (int i = 0; i < classic.Clients.Count; i++)
                {
                    if (classic.Clients[i].IsReady)
                        classic.ActiveClient = classic.Clients[i];
                    gameOver &= (classic.Clients[i].IsGameOver || classic.Clients[i].State == GameClientState.Dead);
                }

                if (gameOver)
                {
                    hadDeaths = false;
                    app.Server.Stop();
                    app.SceneManager.SetScene("MenuScene");
                }
                else if (classic.ActiveClient.IsReady)
                {
                    ClassicGame game = app.GameState as ClassicGame;
                    game.World.ActivePlayer = classic.ActiveClient.Player;

                    // Consume the death batch once, after its scenes and before resuming play.
                    bool offerVictory = hadDeaths && game.World.VictoryCondition.Object
                        .CanOfferLastRivalVictory(game.World.ActivePlayerObj);
                    hadDeaths = false;
                    if (offerVictory)
                    {
                        app.SceneManager.SetScene("LastRivalScene");
                        return;
                    }

                    if (!game.World.ActivePlayerObj.OnMainMap)
                        app.SceneManager.SetScene("LocationScene");
                    else
                        app.SceneManager.SetScene("MapScene");
                }

                if (timer < WAIT_DISPLAY_DELAY)
                    timer += Elapsed;
            }
        }

        //public override bool OnMouseClick(Vector2 Position, MouseButton Button)
        //{
        //    app.SceneManager.PreviousScene();
        //    return true;
        //}

        protected override void OnActivateScene(object parameter)
        {
            app.RenderMouse = false;
            timer = 0;
        }

        protected override void OnInactivateScene()
        {
            app.RenderMouse = true;
        }
    }
}
