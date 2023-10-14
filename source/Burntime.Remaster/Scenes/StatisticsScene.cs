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

        public StatisticsScene(Module App)
            : base(App)
        {
            Background = "blz.pac";
            Music = "17_MUS 17_HSC.ogg";
            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(212, 212, 212), new PixelColor(92, 92, 96));
            CaptureAllMouseClicks = true;
        }

        public override void OnResizeScreen()
        {
            base.OnResizeScreen();

            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
        }

        public override void OnRender(RenderTarget Target)
        {

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
