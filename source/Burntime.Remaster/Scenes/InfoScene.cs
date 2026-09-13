using System;
using System.Collections.Generic;
using System.Linq;
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
        readonly Button exitButton;
        readonly InputPromptHandle exitPrompt;
        readonly TooltipWindow productionTooltip;
        const int ProductionTooltipHeaderIndex = 65;
        const int ProductionTooltipEntryIndex = 66;
        const int ProductionTooltipNoneIndex = 67;
        const int ProductionTooltipPromptIndex = 68;
        const int ProductionTooltipPreviousIndex = 73;
        const int ProductionTooltipNextIndex = 74;

        public InfoScene(Module App)
            : base(App)
        {
            Background = "info.pac";
            Music = "info";
            Size = new Vector2(320, 200);
            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;

            exitButton = new Button(App);
            exitButton.Position = new Vector2(5, 174);
            exitButton.Command += OnButtonBack;
            //btn.SetHover(TextDB.Singleton.GetString(TextRegion.MenuStrings, 4), ColorTable.HoverGray);
            exitButton.Image = "gfx/mapbutton.png";
            exitButton.HoverImage = "gfx/mapbuttonh.png";
            Windows += exitButton;

            Button button = new Button(App);
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
            image.Position = new Vector2(229, 150);
            Windows += image;

            danger = new Image(App);
            danger.Position = new Vector2(224, 27);
            Windows += danger;

            production = new ItemWindow(App);
            production.Position = new Vector2(225, 105);
            production.ItemID = "";
            production.ShowHoverText = false;
            production.LeftClickEvent += NextProduction;
            production.RightClickEvent += PreviousProduction;
            Windows += production;

            Windows += productionTooltip = new TooltipWindow(App)
            {
                Position = new Vector2(225, 137),
                HorizontalAlignment = PositionAlignment.Right,
                VerticalAlignment = PositionAlignment.Right,
                MinimumWidth = 100,
                Layer = 40
            };
            productionTooltip.Hide();

            grid = new ItemGridWindow(App);
            grid.Position = new Vector2(137, 105);
            grid.Spacing = new Vector2(0, 6);
            grid.Grid = new Vector2(1, 2);
            Windows += grid;

            titleFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(156, 156, 156), new PixelColor(76, 32, 4));
            font = new GuiFont(BurntimeClassic.FontName, ClassicColors.Gray, new PixelColor(92, 92, 96));

            items = new SortedList<string, int>();

            Windows += new InputPromptOverlay(app, Prompts,
                InputPromptColorScheme.Muted);
            exitPrompt = exitButton.Prompts.Add(InputAction.Back, "",
                app.Language == "de" ? new Vector2(56, 7) : new Vector2(50, 4),
                horizontalAlignment: PositionAlignment.Left,
                verticalAlignment: PositionAlignment.Center,
                showBackground: false, horizontalPadding: 2);
            fighterImage = "syssze.raw?32";
            doctorImage = "syssze.raw?16";
            technicianImage = "syssze.raw?48";
        }

        public override void OnResizeScreen(bool reload = false)
        {
            base.OnResizeScreen(reload);

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

            font.DrawText(target, new Vector2(224, 86), txt[406], TextAlignment.Left, VerticalTextAlignment.Top);

            txt.AddArgument("|J", loc.GetFoodProductionRate().FoodPerDay);
            txt.AddArgument("|D", loc.Source.Water);

            font.DrawText(target, new Vector2(265, 117), txt[421], TextAlignment.Left, VerticalTextAlignment.Top);
            font.DrawText(target, new Vector2(265, 152), txt[422], TextAlignment.Left, VerticalTextAlignment.Top);

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
            exitPrompt.UpdatePosition(app.Language == "de" ? new Vector2(56, 7) : new Vector2(50, 4));

            int city = classic.InfoCity;
            Location loc = classic.Game.World.Locations[city];

            Music = loc.Danger?.Type switch
            {
                "gas" => "info_toxic",
                "radiation" => "info_irradiated",
                _ => "info"
            };

            if (loc.Danger == null)
                danger.Background = null;
            else
            {
                danger.Background = loc.Danger.InfoIcon.ID;
            }

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

        public override void OnUpdate(float elapsed)
        {
            UpdateProductionTooltip();
        }

        void UpdateProductionTooltip()
        {
            bool show = productionID >= 0 &&
                (production.IsMouseHovered || app.LastInputMode != InputMode.Mouse);
            if (!show)
            {
                if (productionTooltip.IsVisible)
                    productionTooltip.Hide();
                return;
            }

            BurntimeClassic classic = app as BurntimeClassic;
            Location location = classic.Game.World.Locations[classic.InfoCity];
            if (location.Production == null)
            {
                productionTooltip.Hide();
                return;
            }

            Production[] allowedProductions = location.ValidProductions.ToArray();
            string[] entries = allowedProductions.Select(production =>
            {
                string[] tools = location.Rooms.SelectMany(room => room.Items)
                    .Concat(production.AllowInventory
                        ? location.CampNPC.SelectMany(npc => npc.Items)
                        : Enumerable.Empty<Item>())
                    .Where(item => item.Type.Production == production)
                    .GroupBy(item => item.ID)
                    .Select(group => group.Count() > 1
                        ? $"{group.First().Title} x{group.Count()}"
                        : group.First().Title)
                    .ToArray();
                string toolList = tools.Length > 0
                    ? string.Join("/", tools)
                    : app.ResourceManager.GetString("tooltip",
                        ProductionTooltipNoneIndex);
                TextHelper entryText = new(app, "tooltip");
                entryText.AddArgument("{product}", production.Produce.Title);
                entryText.AddArgument("{tools}", toolList);
                return entryText.Get(ProductionTooltipEntryIndex);
            })
                .Where(line => line.Length > 0)
                .ToArray();
            productionTooltip.Header = app.ResourceManager.GetString("tooltip",
                ProductionTooltipHeaderIndex);
            productionTooltip.Text = string.Join('\n', entries);
            if (app.LastInputMode == InputMode.Mouse)
            {
                productionTooltip.Prompt = new InputPrompt(
                    InputAction.Secondary,
                    app.ResourceManager.GetString("tooltip",
                        ProductionTooltipPreviousIndex))
                {
                    MouseControl = MouseButton.Right
                };
                productionTooltip.SecondaryPrompt = new InputPrompt(
                    InputAction.Primary,
                    app.ResourceManager.GetString("tooltip",
                        ProductionTooltipNextIndex))
                {
                    MouseControl = MouseButton.Left
                };
            }
            else
            {
                productionTooltip.Prompt = new InputPrompt(
                    InputPattern.HorizontalNavigation,
                    app.ResourceManager.GetString("tooltip",
                        ProductionTooltipPromptIndex));
                productionTooltip.SecondaryPrompt = null;
            }
            productionTooltip.Status = null;
            productionTooltip.RefreshLayout();
            if (!productionTooltip.IsVisible)
                productionTooltip.Show();
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

        public override bool OnVKeyPress(SystemKey key)
        {
            if (key != SystemKey.Escape)
                return false;

            OnButtonBack();
            return true;
        }

        public override bool OnInputAction(InputAction action)
        {
            if (action == InputAction.Back)
            {
                OnButtonBack();
                return true;
            }

            if (action.IsUp())
            {
                OnButtonListUp();
                return true;
            }

            if (action.IsDown())
            {
                OnButtonListDown();
                return true;
            }

            if (action.IsLeft())
            {
                PreviousProduction();
                return true;
            }

            if (action.IsRight())
            {
                NextProduction();
                return true;
            }

            return false;
        }

        public override bool OnKeyPress(char key)
        {
            return false;
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

        void NextProduction()
        {
            // TODO move to logic
            BurntimeClassic classic = app as BurntimeClassic;
            int city = classic.InfoCity;

            Location loc = classic.Game.World.Locations[city];
            if (productionID >= 0 && productionID + 1 < loc.AvailableProducts.Length &&
                loc.AvailableProducts[productionID + 1] >= 0)
            {
                productionID++;
                loc.Production = classic.Game.Productions[loc.AvailableProducts[productionID]];
                production.ItemID = loc.Production.Produce.ID;
            }
        }

        void PreviousProduction()
        {
            // TODO move to logic
            BurntimeClassic classic = app as BurntimeClassic;
            int city = classic.InfoCity;

            Location loc = classic.Game.World.Locations[city];
            if (productionID > 0 && productionID < loc.AvailableProducts.Length)
            {
                productionID--;
                loc.Production = classic.Game.Productions[loc.AvailableProducts[productionID]];
                production.ItemID = loc.Production.Produce.ID;
            }
        }
    }
}
