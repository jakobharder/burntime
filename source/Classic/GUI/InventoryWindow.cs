using System;
using System.Collections.Generic;
using Burntime.Classic.Logic;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Classic.GUI
{
    enum InventorySide
    {
        Left,
        Right,
        None
    }

    class InventoryWindow : Container
    {
        sealed class InventoryPage
        {
            public Character Character;
            public int Offset = 0;
            public int PageNumber = 0;

            public InventoryPage(Character character, int pageNumber)
            {
                Character = character;
                PageNumber = pageNumber;
            }
        }

        GuiImage back;
        List<InventoryPage> pages;
        int activePageIndex;
        InventoryPage activePage;
        Vector2 basePos;
        ICharacterCollection group;
        Character leader;
        FaceWindow face;
        GuiFont font;
        bool side;

        GuiFont nameFont;
        String pageName;
        Vector2 namePosition;

        Button[] pageButtons = new Button[5];
        int[] pageIndices = new int[5];

        ItemGridWindow grid;
        public ItemGridWindow Grid
        {
            get { return grid; }
            set { grid = value; }
        }

        public Character ActiveCharacter
        {
            get { if (activePage == null) return null; return activePage.Character; }
        }

        public LogicEvent LeftClickItemEvent;
        public LogicEvent RightClickItemEvent;

        public InventoryWindow(Module App, InventorySide Side)
            : base(App)
        {
            side = (Side == InventorySide.Right);

            back = side ? "inv.raw?2" : "gfx/inventory_left.png";

            basePos = new Vector2(15, 15);
            Size = new Vector2(back.Width, back.Height) + basePos;

            face = new FaceWindow(App);
            face.Position = side ? (basePos + new Vector2(67, 0)) : basePos;
            face.DisplayOnly = true;
            Windows += face;

            grid = new ItemGridWindow(App);
            grid.LockPositions = true;
            grid.Position = new Vector2(side ? 9 : 19, side ? 72 : 83) + basePos;
            grid.Spacing = new Vector2(4, side ? 16 : 5);
            grid.Grid = new Vector2(3, 2);
            grid.LeftClickItemEvent += OnLeftClickItem;
            grid.RightClickItemEvent += OnRightClickItem;
            Windows += grid;

            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(128, 136, 192));
            font.Borders = TextBorders.Screen;
            nameFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(240, 64, 56));
            nameFont.Borders = TextBorders.Screen;

            pageName = "";

            for (int i = 0; i < 5; i++)
            {
                pageButtons[i] = new Button(App);
                pageButtons[i].Image = "munt.raw?" + (5 + i);
                if (side)
                    pageButtons[i].Position = basePos + new Vector2(120 + 3 * i, 142 - 18 * i);
                else
                    pageButtons[i].Position = basePos + new Vector2(-3 * i, 142 - 18 * i);
                pageButtons[i].Hide();
                pageButtons[i].Command += new CommandHandler(OnPage, i);
                pageIndices[i] = i;
                Windows += pageButtons[i];
            }

            pages = new List<InventoryPage>();
            activePageIndex = 0;
            activePage = null;
        }

        public void SetGroup(Character leader)
        {
            this.group = leader.GetGroup();
            this.leader = leader;

            activePageIndex = 0;

            Refresh();
        }

        public int FreeSlots
        {
            get { return group.GetFreeSlotCount(); }
        }

        void Refresh()
        {
            lock (this)
            {
                //activePageIndex = 0;
                pages.Clear();
                for (int i = 0; i < pageIndices.Length; i++)
                    pageIndices[i] = i;

                // trader group
                if (group[0].Class == CharClass.Trader)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        InventoryPage page = new InventoryPage(group[0], i);
                        page.Offset = i * 6;
                        pages.Add(page);
                        pageButtons[i].Show();
                    }
                    for (int i = 2; i < pageButtons.Length; i++)
                        pageButtons[i].Hide();
                }
                // most likely player group
                else
                {
                    int count = 0;

                    for (int i = 0; i < group.Count; i++)
                    {
                        // show only if in range
                        if (group.IsInRange(leader, group[i]))
                        {
                            InventoryPage page = new InventoryPage(group[i], i);
                            pages.Add(page);
                            pageButtons[count].Image = "munt.raw?" + (5 + i);
                            pageButtons[count].Show();

                            count++;
                        }
                    }
                    for (int i = count; i < pageButtons.Length; i++)
                        pageButtons[i].Hide();
                }
                face.FaceID = pages[activePageIndex].Character.FaceID;
                activePage = pages[activePageIndex];

                OnSelectPage();
            }
        }

        public void OnSelectPage()
        {
            grid.Clear();

            if (activePage != null)
            {
                for (int i = activePage.Offset; i < activePage.Character.Items.Count && grid.Count < 6; i++)
                    grid.Add(activePage.Character.Items[i]);
                if (activePage.Character.Weapon != null)
                    grid.Selection.Add(activePage.Character.Weapon);
                if (activePage.Character.Protection != null)
                    grid.Selection.Add(activePage.Character.Protection);

                face.FaceID = activePage.Character.FaceID;
            }
        }

        public override void OnRender(IRenderTarget Target)
        {
            if (side)
            {
                for (int i = pages.Count - 1; i >= 0; i--)
                    Target.DrawSprite(basePos + new Vector2(3, -2) * i, back);
            }
            else
            {
                for (int i = pages.Count - 1; i >= 0; i--)
                    Target.DrawSprite(basePos + -3 * i, back);
            }

            TextHelper txt = new TextHelper(app, "burn");

            txt.AddArgument("|B", activePage.Character.Health);
            txt.AddArgument("|A", activePage.Character.Experience);
            txt.AddArgument("|C", activePage.Character.Water);
            txt.AddArgument("|D", activePage.Character.Food);

            txt.AddArgument("{attack}", (int)activePage.Character.AttackValue);
            txt.AddArgument("{defense}", (int)activePage.Character.DefenseValue);

            int fontSpacing = 10;

            Vector2 textPos = new Vector2();
            textPos.x = basePos.x + (side ? 11 : 73);
            textPos.y = basePos.y + 11;
            font.DrawText(Target, textPos, txt[40 + (int)activePage.Character.Class], TextAlignment.Left, VerticalTextAlignment.Top);
            textPos.y += fontSpacing;
            font.DrawText(Target, textPos, txt[380], TextAlignment.Left, VerticalTextAlignment.Top);
            textPos.y += fontSpacing;
            font.DrawText(Target, textPos, txt[381], TextAlignment.Left, VerticalTextAlignment.Top);
            textPos.y += fontSpacing;

            if (activePage.Character.Class != CharClass.Trader)
            {
                font.DrawText(Target, textPos, txt[403], TextAlignment.Left, VerticalTextAlignment.Top);
                textPos.y += fontSpacing;
                font.DrawText(Target, textPos, txt[402], TextAlignment.Left, VerticalTextAlignment.Top);
                textPos.y += fontSpacing;

                font.DrawText(Target, textPos, txt.Get("newburn?100"), TextAlignment.Left, VerticalTextAlignment.Top);
                textPos.x = basePos.x + 20;
                font.DrawText(Target, textPos, txt.Get("newburn?99"), TextAlignment.Left, VerticalTextAlignment.Top);
                textPos.y += fontSpacing;

                if (activePage.Character.Protection != null)
                {
                    string text = "";
                    foreach (var protection in activePage.Character.Protection.Type.Protection)
                    {
                        var p = protection.Object;
                        text += (int)(System.Math.Round(p.Rate * 100));
                        if (p.Type == "gas")
                            text += app.ResourceManager.GetString("newburn?101");
                        else
                            text += app.ResourceManager.GetString("newburn?102");
                    }
                    font.DrawText(Target, textPos, text, TextAlignment.Left, VerticalTextAlignment.Top);
                }
                textPos.y += fontSpacing;
            }

            pageName = "";
            for (int i = 0; i < pageButtons.Length; i++)
            {
                if (pageButtons[i].IsHover && pageIndices[i] >= 0)
                {
                    namePosition = pageButtons[i].Position + new Vector2(6, -9);
                    pageName = pages[pageIndices[i]].Character.Name;
                }
            }

            Target.Layer += 5;

            if (pageName != "")
            {
                nameFont.DrawText(Target, namePosition, pageName, TextAlignment.Center, VerticalTextAlignment.Top);
            }

            Target.Layer -= 5;
        }

        void OnLeftClickItem(Framework.States.StateObject State)
        {
            if (LeftClickItemEvent != null)
            {
                LeftClickItemEvent.Execute(State);
            }
        }

        void OnRightClickItem(Framework.States.StateObject State)
        {
            if (RightClickItemEvent != null)
            {
                RightClickItemEvent.Execute(State);
            }
        }

        void OnPage(int index)
        {
            if (index > 0)
            {
                pageIndices[0] = pageIndices[index];
                for (int i = 1; i < pageIndices.Length; i++)
                    pageIndices[i] = (pageIndices[0] >= i) ? (i - 1) : i;

                for (int i = 0; i < pages.Count; i++)
                    pageButtons[i].Image = "munt.raw?" + (5 + pages[pageIndices[i]].PageNumber);

                activePageIndex = pageIndices[0];
                activePage = pages[activePageIndex];

                OnSelectPage();
            }
        }
    }
}
