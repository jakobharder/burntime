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
        readonly ItemGridTooltip itemTooltip;
        const int ProductionTooltipHeaderIndex = 66;
        const int ProductionTooltipEntryIndex = 67;
        const int ProductionTooltipNoneIndex = 68;
        const int ProductionTooltipPromptIndex = 69;
        const int ProductionTooltipPreviousIndex = 74;
        const int ProductionTooltipNextIndex = 75;
        const int ProductionTooltipAutomaticIndex = 76;
        bool productionTooltipDismissed;
        const int StorageRowHeight = 38;
        static readonly Rect StorageBounds = new(105, 103, 106, 72);
        static readonly Rect ProductionBounds = new(225, 105, 32, 32);
        float touchScrollPixels;
        readonly KineticScroll touchMomentum = new();
        bool productionTouchSelected;
        bool productionSwipeHandled;

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
                Layer = 40
            };
            productionTooltip.Hide();

            grid = new ItemGridWindow(App);
            grid.Position = new Vector2(137, 105);
            grid.Spacing = new Vector2(0, 6);
            grid.Grid = new Vector2(1, 2);
            Windows += grid;
            itemTooltip = new ItemGridTooltip(app,
                () => (app as BurntimeClassic)?.SelectedCharacter);
            itemTooltip.AddGrid(grid);
            Windows += itemTooltip.Window;

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

            Production.Rate production = loc.GetFoodProductionRate();
            TextHelper foodText = new(app, "newburn");
            foodText.AddArgument("|F", loc.GetCurrentProductionStockCount());
            foodText.AddArgument("|D", production.FoodPerDay);
            DrawProductionText(target, new Vector2(263, 117), foodText[82], foodText[81]);

            TextHelper waterText = new(app, "newburn");
            waterText.AddArgument("|F", loc.Source.Reserve);
            waterText.AddArgument("|C", loc.Source.Capacity);
            waterText.AddArgument("|D", loc.Source.Water);
            DrawProductionText(target, new Vector2(263, 152), waterText[80], waterText[81]);

            if (loc.Danger != null)
                font.DrawText(target, new Vector2(251, 68), loc.Danger.InfoString, TextAlignment.Center, VerticalTextAlignment.Top);

        }

        void DrawProductionText(RenderTarget target, Vector2 position,
            string reserve, string daily)
        {
            Vector2 firstLine = new(position.x, position.y - font.LineHeight / 2);
            font.DrawText(target, firstLine, reserve, TextAlignment.Left,
                VerticalTextAlignment.Top);
            font.DrawText(target, new Vector2(firstLine.x,
                firstLine.y + font.LineHeight), daily, TextAlignment.Left,
                VerticalTextAlignment.Top);
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
            productionTooltipDismissed = false;
            productionTouchSelected = false;
            productionSwipeHandled = false;

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
                productionID = -1;
                if (!loc.IsProductionAutomatic)
                {
                    for (int i = 0; i < loc.AvailableProducts.Length; i++)
                    {
                        if (loc.Production.ID == loc.AvailableProducts[i])
                        {
                            productionID = i;
                            break;
                        }
                    }
                }

                production.ItemID = loc.Production.Produce.ID;
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
            touchScrollPixels = 0;
            touchMomentum.Stop();
            RefreshItems();
            UpdateCampNPCs();
        }

        public override void OnUpdate(float elapsed)
        {
            ApplyTouchScroll(touchMomentum.Update(elapsed));
            UpdateProductionTooltip();
            itemTooltip.Update();
        }

        public override bool OnMouseWheel(Vector2 position, int delta)
        {
            if (!StorageBounds.PointInside(position))
                return false;
            MoveItemOffset(-System.Math.Sign(delta));
            return true;
        }

        public override bool OnTouchScroll(Vector2 position, Vector2 delta)
        {
            if (StorageBounds.PointInside(position))
            {
                HideStorageTooltip();
                productionTouchSelected = false;
                productionTooltip.Hide();
                touchMomentum.Stop();
                ApplyTouchScroll(-delta.y);
                return true;
            }

            if (!ProductionBounds.PointInside(position) ||
                System.Math.Abs(delta.x) <= System.Math.Abs(delta.y))
                return false;

            HideStorageTooltip();
            productionTouchSelected = true;
            productionTooltipDismissed = false;
            if (!productionSwipeHandled)
            {
                productionSwipeHandled = true;
                if (delta.x < 0)
                    NextProduction();
                else
                    PreviousProduction();
            }
            return true;
        }

        public override void OnTouchScrollEnd(Vector2 position, Vector2f velocity)
        {
            if (StorageBounds.PointInside(position))
                touchMomentum.Release(-velocity.y);
            if (ProductionBounds.PointInside(position))
                productionSwipeHandled = false;
        }

        public override void OnTouchPress(Vector2 position)
        {
            if (StorageBounds.PointInside(position))
                touchMomentum.Stop();
            if (ProductionBounds.PointInside(position))
                productionSwipeHandled = false;
        }

        public override bool OnTouchTap(Vector2 position)
        {
            if (ProductionBounds.PointInside(position) && production.ItemID != "")
            {
                HideStorageTooltip();
                productionTouchSelected = true;
                productionTooltipDismissed = false;
                return true;
            }

            if (StorageBounds.PointInside(position))
            {
                productionTouchSelected = false;
                productionTooltip.Hide();
            }
            return false;
        }

        void HideStorageTooltip()
        {
            grid.ClearFocus();
            itemTooltip.Window.Hide();
        }

        void ApplyTouchScroll(float pixels)
        {
            touchScrollPixels += pixels;
            int rows = (int)(touchScrollPixels / StorageRowHeight);
            if (rows == 0)
                return;
            touchScrollPixels -= rows * StorageRowHeight;
            if (!MoveItemOffset(rows))
            {
                touchScrollPixels = 0;
                touchMomentum.Stop();
            }
        }

        void UpdateProductionTooltip()
        {
            bool show = !productionTooltipDismissed && production.ItemID != "" &&
                (app.LastInputMode == InputMode.Touch ? productionTouchSelected :
                    production.IsMouseHovered || app.LastInputMode != InputMode.Mouse);
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
            productionTooltip.Status = location.IsProductionAutomatic
                ? app.ResourceManager.GetString("tooltip", ProductionTooltipAutomaticIndex)
                : null;
            if (app.LastInputMode == InputMode.Mouse)
            {
                productionTooltip.Prompt = new InputPrompt(
                    InputAction.Primary,
                    app.ResourceManager.GetString("tooltip", ProductionTooltipNextIndex))
                {
                    MouseControl = MouseButton.Left
                };
                productionTooltip.SecondaryPrompt = new InputPrompt(
                    InputAction.Secondary,
                    app.ResourceManager.GetString("tooltip", ProductionTooltipPreviousIndex))
                {
                    MouseControl = MouseButton.Right
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
                NextProduction();
                return true;
            }

            if (action.IsRight())
            {
                PreviousProduction();
                return true;
            }

            if (action == InputAction.Primary)
            {
                productionTooltipDismissed = true;
                productionTooltip.Hide();
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
            MoveItemOffset(-1);
        }

        void OnButtonListDown()
        {
            MoveItemOffset(1);
        }

        bool MoveItemOffset(int direction)
        {
            int newOffset = System.Math.Clamp(offset + direction, 0,
                System.Math.Max(0, items.Count - 2));
            if (newOffset == offset)
                return false;
            offset = newOffset;
            RefreshItems();
            return true;
        }

        void NextProduction()
        {
            // TODO move to logic
            BurntimeClassic classic = app as BurntimeClassic;
            int city = classic.InfoCity;

            Location loc = classic.Game.World.Locations[city];
            if (productionID + 1 < loc.AvailableProducts.Length &&
                loc.AvailableProducts[productionID + 1] >= 0)
            {
                productionID++;
                loc.SelectProduction(classic.Game.Productions[loc.AvailableProducts[productionID]]);
                production.ItemID = loc.Production.Produce.ID;
                productionTooltipDismissed = false;
            }
        }

        void PreviousProduction()
        {
            // TODO move to logic
            BurntimeClassic classic = app as BurntimeClassic;
            int city = classic.InfoCity;

            Location loc = classic.Game.World.Locations[city];
            if (productionID == 0)
            {
                productionID = -1;
                loc.SelectAutomaticFoodProduction();
                if (loc.Production != null)
                    production.ItemID = loc.Production.Produce.ID;
                productionTooltipDismissed = false;
            }
            else if (productionID > 0 && productionID < loc.AvailableProducts.Length)
            {
                productionID--;
                loc.SelectProduction(classic.Game.Productions[loc.AvailableProducts[productionID]]);
                production.ItemID = loc.Production.Produce.ID;
                productionTooltipDismissed = false;
            }
        }
    }
}
