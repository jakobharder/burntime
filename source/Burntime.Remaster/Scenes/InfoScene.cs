using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Remaster.GUI;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Data;

namespace Burntime.Remaster.Scenes
{
    class InfoScene : Scene
    {
        GuiFont titleFont;
        GuiFont font;
        Image danger;
        ItemWindow production;
        //String dangerText;
        ItemGridWindow grid;
        SortedList<string, int> items;
        int[] itemCount = new int[2];
        int productionID;

        int fighter;
        int technicians;
        int doctors;

        GuiImage fighterImage;
        GuiImage technicianImage;
        GuiImage doctorImage;

        public InfoScene(Module App)
            : base(App)
        {
            Background = "info.pac";
            Music = "info";
            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;


            Button button = new Button(App);
            button.Position = new Vector2(5, 174);
            button.Command += OnButtonBack;
            //btn.SetHover(TextDB.Singleton.GetString(TextRegion.MenuStrings, 4), ColorTable.HoverGray);
            button.Image = "gfx/mapbutton.png";
            button.HoverImage = "gfx/mapbuttonh.png";
            Windows += button;

            button = new Button(App);
            button.Position = new Vector2(107, 122);
            button.Command += OnButtonListUp;
            button.HoverImage = "gfx/up.png";
            Windows += button;
            button = new Button(App);
            button.Position = new Vector2(107, 141);
            button.Command += OnButtonListDown;
            button.HoverImage = "gfx/down.png";
            Windows += button;

            Image image = new Image(App);
            image.Background = "inf.ani?0-2";
            image.Background.Animation.Speed = 6.0f;
            image.Position = new Vector2(4, 105);
            Windows += image;

            image = new Image(App);
            image.Background = "inf.ani?4-6";
            image.Background.Animation.Speed = 6;
            image.Background.Animation.Progressive = false;
            image.Position = new Vector2(234, 150);
            Windows += image;

            danger = new Image(App);
            danger.Position = new Vector2(229, 27);
            Windows += danger;

            production = new ItemWindow(App);
            production.Position = new Vector2(230, 105);
            production.ItemID = "";
            production.LeftClickEvent += OnProductionLeft;
            production.RightClickEvent += OnProductionRight;
            Windows += production;

            grid = new ItemGridWindow(App);
            grid.Position = new Vector2(137, 105);
            grid.Spacing = new Vector2(0, 6);
            grid.Grid = new Vector2(1, 2);
            Windows += grid;

            titleFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(156, 156, 156), new PixelColor(76, 32, 4));
            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(184, 184, 184), new PixelColor(92, 92, 96));

            items = new SortedList<string, int>();

            fighterImage = "syssze.raw?32";
            doctorImage = "syssze.raw?16";
            technicianImage = "syssze.raw?48";
        }

        public override void OnResizeScreen()
        {
            base.OnResizeScreen();

            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
        }

        public override void OnRender(RenderTarget target)
        {
            BurntimeClassic classic = app as BurntimeClassic;
            int city = classic.InfoCity;

            TextHelper txt = new TextHelper(app, "burn");

            Location loc = classic.Game.World.Locations[city];

            titleFont.DrawText(target, new Vector2(193, 7), loc.Title, TextAlignment.Center, VerticalTextAlignment.Top);

            if (itemCount[0] != -1)
                font.DrawText(target, new Vector2(188, 120), itemCount[0].ToString());
            if (itemCount[1] != -1)
                font.DrawText(target, new Vector2(188, 160), itemCount[1].ToString());

            // render npc info
            font.DrawText(target, new Vector2(100, 33), txt[396], TextAlignment.Left, VerticalTextAlignment.Top);
            font.DrawText(target, new Vector2(100, 51), txt[397], TextAlignment.Left, VerticalTextAlignment.Top);
            font.DrawText(target, new Vector2(100, 69), txt[398], TextAlignment.Left, VerticalTextAlignment.Top);

            int max = System.Math.Max(font.GetWidth(txt[396]), font.GetWidth(txt[397]));
            max = System.Math.Max(font.GetWidth(txt[398]), max);

            RenderNPCLine(target, new Vector2(110 + max, 29), fighter, fighterImage);
            RenderNPCLine(target, new Vector2(110 + max, 47), technicians, technicianImage);
            RenderNPCLine(target, new Vector2(110 + max, 65), doctors, doctorImage);

            // render resources
            font.DrawText(target, new Vector2(137, 86), txt[399], TextAlignment.Left, VerticalTextAlignment.Top);

            font.DrawText(target, new Vector2(229, 86), txt[406], TextAlignment.Left, VerticalTextAlignment.Top);

            txt.AddArgument("|J", loc.GetFoodProductionRate().FoodPerDay);
            txt.AddArgument("|D", loc.Source.Water);

            font.DrawText(target, new Vector2(270, 117), txt[421], TextAlignment.Left, VerticalTextAlignment.Top);
            font.DrawText(target, new Vector2(270, 152), txt[422], TextAlignment.Left, VerticalTextAlignment.Top);

            if (loc.Danger != null)
                font.DrawText(target, new Vector2(251, 68), loc.Danger.InfoString, TextAlignment.Center, VerticalTextAlignment.Top);

            txt.ClearArguments();
        }

        private void RenderNPCLine(RenderTarget target, Vector2 position, int npcCount, ISprite image)
        {
            target.Layer += 2;

            for (int i = 0; i < npcCount; i++)
            {
                target.DrawSprite(position, image);
                position.x += 18;
            }

            target.Layer -= 2;
        }

        protected override void OnActivateScene(object parameter)
        {
            BurntimeClassic classic = app as BurntimeClassic;
            int city = classic.InfoCity;

            if (classic.Game.World.Locations[city].Danger == null)
                danger.Background = null;
            else
            {
                danger.Background = classic.Game.World.Locations[city].Danger.InfoIcon.ID;
            }

            Location loc = classic.Game.World.Locations[city];

            if (loc.Production != null)
            {
                for (int i = 0; i < loc.AvailableProducts.Length; i++)
                {
                    if (loc.Production.ID == loc.AvailableProducts[i])
                    {
                        productionID = i;
                        break;
                    }
                }

                production.ItemID = classic.Game.Productions[loc.AvailableProducts[productionID]].Produce.ID;
            }
            else
            {
                productionID = -1;
                production.ItemID = "";
            }

            items.Clear();
            foreach (Room room in loc.Rooms)
            {
                for (int i = 0; i < room.Items.Count; i++)
                {
                    if (items.ContainsKey(room.Items[i].Type))
                        items[room.Items[i].Type]++;
                    else
                        items.Add(room.Items[i].Type, 1);
                }
            }


            offset = 0;
            RefreshItems();
            UpdateCampNPCs();
        }

        int offset = 0;
        private void RefreshItems()
        {
            itemCount[0] = -1;
            itemCount[1] = -1;
            grid.Clear();

            int count = 0;
            foreach (KeyValuePair<string, int> item in items)
            {
                if (count < offset)
                {
                    count++;
                    continue;
                }
                Item it = new Item();
                it.Type = BurntimeClassic.Instance.Game.ItemTypes[item.Key];
                itemCount[count - offset] = item.Value;
                grid.Add(it);
                count++;
                if (count - offset == 2)
                    break;
            }
        }

        private void UpdateCampNPCs()
        {
            fighter = 0;
            technicians = 0;
            doctors = 0;

            BurntimeClassic classic = app as BurntimeClassic;
            int city = classic.InfoCity;
            Location location = classic.Game.World.Locations[city];

            foreach (Character npc in location.CampNPC)
            {
                switch (npc.Class)
                {
                    case CharClass.Technician:
                        technicians ++;
                        break;
                    case CharClass.Doctor:
                        doctors ++;
                        break;
                    case CharClass.Mercenary:
                        fighter ++;
                        break;
                    default:
                        Burntime.Platform.Log.Warning("info screen: unknown npc class");
                        break;
                }
            }
        }

        void OnButtonBack()
        {
            app.SceneManager.PreviousScene();
        }

        void OnButtonListUp()
        {
            if (items.Count <= 1)
                return;

            offset--;
            if (offset < 0)
                offset = 0;

            RefreshItems();
        }

        void OnButtonListDown()
        {
            if (items.Count <= 1)
                return;

            offset++;
            if (offset > items.Count - 2)
                offset = items.Count - 2;

            RefreshItems();
        }

        void OnProductionLeft()
        {
            // TODO move to logic
            BurntimeClassic classic = app as BurntimeClassic;
            int city = classic.InfoCity;

            Location loc = classic.Game.World.Locations[city];
            if (productionID < 3 && loc.AvailableProducts[productionID + 1] >= 0)
            {
                productionID++;
                loc.Production = classic.Game.Productions[loc.AvailableProducts[productionID]];
                production.ItemID = loc.Production.Produce.ID;
            }
        }

        void OnProductionRight()
        {
            // TODO move to logic
            BurntimeClassic classic = app as BurntimeClassic;
            int city = classic.InfoCity;

            Location loc = classic.Game.World.Locations[city];
            if (productionID > 0)
            {
                productionID--;
                loc.Production = classic.Game.Productions[loc.AvailableProducts[productionID]];
                production.ItemID = loc.Production.Produce.ID;
            }
        }
    }
}
