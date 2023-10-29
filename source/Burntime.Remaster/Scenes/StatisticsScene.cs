using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.Scenes
{
    class StatisticsScene : Scene
    {
        GuiFont font;

        const int MaxLocations = 16;

        int maggots;
        int meat;
        int rats;
        int snakes;
        int locations;
        int controlledCities;
        int cities;

        int fighter;
        int doctor;
        int technician;

        readonly GuiImage _rightBottom, _rightTop;
        readonly GuiImage _leftBottom, _leftTop;
        readonly ISprite _background, _background2;
        readonly ISprite _backgroundClassic;

        public StatisticsScene(Module App)
            : base(App)
        {
            //Background = "blz.pac";
            Size = new Vector2(320, 200);
            Music = "score";
            Position = (app.Engine.Resolution.Game - Size) / 2;
            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(212, 212, 212), new PixelColor(92, 92, 96));
            CaptureAllMouseClicks = true;

            _rightBottom = (GuiImage)"gfx/backgrounds/stats_rightbottom.png";
            _rightTop = (GuiImage)"gfx/backgrounds/stats_righttop.png";
            _leftBottom = (GuiImage)"gfx/backgrounds/stats_leftbottom.png";
            _leftTop = (GuiImage)"gfx/backgrounds/stats_lefttop.png";
            _background = app.ResourceManager.GetImage("gfx/backgrounds/statistics.png");
            _background2 = app.ResourceManager.GetImage("gfx/backgrounds/statistics2.png");
            _backgroundClassic = app.ResourceManager.GetImage("blz.pac");
        }

        public override void OnResizeScreen()
        {
            base.OnResizeScreen();

            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
        }

        public override void OnRender(RenderTarget Target)
        {
            if (app.IsNewGfx)
            {
                Vector2 gameSize = app.Engine.Resolution.Game;
                gameSize.Max(_background.Size);

                Vector2 offset = (app.Engine.Resolution.Game - _background.Size) / 2;
                offset.Min(0);
                offset -= Position;
                Target.DrawSprite(offset, _background);

                Target.Layer++;
                Target.DrawSprite(gameSize - _rightBottom.Size + offset + Vector2.One, _rightBottom);
                Target.DrawSprite(new Vector2(gameSize.x - _rightTop.Width + 1, 0) + offset, _rightTop);
                Target.DrawSprite(offset, _leftTop);
                Target.DrawSprite(new Vector2(0, gameSize.y - _leftBottom.Height + 1) + offset, _leftBottom);
            }
            else
            {
                Target.DrawSprite(_backgroundClassic);
            }

            int y = -1;

            TextHelper txt = new TextHelper(app, "burn");
            txt.AddArgument("|J", (app as BurntimeClassic).Game.World.Day);
            txt.AddArgument("|B", fighter);
            txt.AddArgument("|G", technician);
            txt.AddArgument("|H", doctor);
            txt.AddArgument("|C", maggots);
            txt.AddArgument("|D", rats);
            txt.AddArgument("|E", snakes);
            txt.AddArgument("|F", meat);
            txt.AddArgument("|A", locations);
            txt.AddArgument("|I", controlledCities);
            txt.AddArgument("|K", cities);

            for (int i = 0; i < 16; i++)
            {
                String str = txt[569 + i];

                int x = Target.Width / 2 - font.GetWidth(str) / 2 - 10;
                font.DrawText(Target, new Vector2(x, y), str, TextAlignment.Left, VerticalTextAlignment.Top);
                y += 11;
            }
        }

        public override bool OnMouseClick(Vector2 Position, MouseButton Button)
        {
            app.SceneManager.PreviousScene();
            return true;
        }

        public override bool OnVKeyPress(SystemKey key)
        {
            app.SceneManager.PreviousScene();
            return true;
        }

        protected override void OnActivateScene(object parameter)
        {
            app.RenderMouse = false;

            BurntimeClassic classic = app as BurntimeClassic;
            ClassicWorld world = classic.Game.World;

            maggots = 0;
            rats = 0;
            snakes = 0;
            meat = 0;

            fighter = 0;
            doctor = 0;
            technician = 0;

            locations = 0;
            cities = 0;
            controlledCities = 0;

            for (int i = 0; i < world.Locations.Count; i++)
            {
                Location l = world.Locations[i];
                // add controlled city
                if (l.IsCity)
                {
                    bool controlled = true;
                    foreach (Location n in l.Neighbors)
                    {
                        if (!n.IsCity && n.Player != world.ActivePlayerObj)
                        {
                            controlled = false;
                            break;
                        }
                    }

                    cities++;
                    if (controlled)
                        controlledCities++;
                }

                // add player camp info to stats
                if (l.Player == world.ActivePlayerObj)
                {
                    locations ++;

                    for (int j = 0; j < l.Rooms.Count; j++)
                    {
                        maggots += l.Rooms[j].Items.GetCount(classic.Game.ItemTypes["item_maggots"]);
                        rats += l.Rooms[j].Items.GetCount(classic.Game.ItemTypes["item_rats"]);
                        snakes += l.Rooms[j].Items.GetCount(classic.Game.ItemTypes["item_snake"]);
                        meat += l.Rooms[j].Items.GetCount(classic.Game.ItemTypes["item_meat"]);
                    }

                    for (int j = 0; j < l.Characters.Count; j++)
                    {
                        if (l.Characters[j].Player == world.ActivePlayerObj)
                        {
                            switch (l.Characters[j].Class)
                            {
                                case CharClass.Mercenary: fighter++; break;
                                case CharClass.Doctor: doctor++; break;
                                case CharClass.Technician: technician++; break;
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < world.ActivePlayerObj.Group.Count; i++)
            {
                switch (world.ActivePlayerObj.Group[i].Class)
                {
                    case CharClass.Mercenary: fighter++; break;
                    case CharClass.Doctor: doctor++; break;
                    case CharClass.Technician: technician++; break;
                }
            }
        }

        protected override void OnInactivateScene()
        {
            app.RenderMouse = true;
        }
    }
}
