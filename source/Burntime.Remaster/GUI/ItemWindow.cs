using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;

namespace Burntime.Remaster.GUI
{
    class ItemWindow : Image
    {
        GuiFont font;
        String text;
        CommandEvent leftClickEvent;
        CommandEvent rightClickEvent;

        static List<ItemWindow> hover = new List<ItemWindow>();

        public CommandEvent LeftClickEvent
        {
            get { return leftClickEvent; }
            set { leftClickEvent = value; }
        }

        public CommandEvent RightClickEvent
        {
            get { return rightClickEvent; }
            set { rightClickEvent = value; }
        }

        public ItemWindow(Module App)
            : base(App)
        {
            Size = new Vector2(32, 32);
            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(240, 64, 56));
            font.Borders = TextBorders.Screen;
            text = null;
        }

        string itemID;
        public string ItemID
        {
            get { return itemID; }
            set
            {
                itemID = value;
                RefreshItem();
            }
        }

        void RefreshItem()
        {
            if (itemID != "")
            {
                Background = BurntimeClassic.Instance.Game.ItemTypes[itemID].Sprite;
                text = BurntimeClassic.Instance.Game.ItemTypes[itemID].Title;
            }
            else
            {
                Background = null;
                text = null;
            }
        }

        public override void OnMouseEnter()
        {
            base.OnMouseEnter();
            Layer++;

            // set hovered item globally
            hover.Add(this);
        }

        public override void OnMouseLeave()
        {
            base.OnMouseLeave();
            Layer--;

            hover.Remove(this);
        }

        public override bool OnMouseClick(Vector2 Position, MouseButton Button)
        {
            // prevent from clicking two overlapping items at a time
            if (GetTopMostItem() != this)
                return false;

            Window[] group = Parent.Windows.GetGroup(Group);
            int index = 0;
            for (; index < group.Length; index++)
            {
                if (group[index] == this)
                    break;
            }

            if (Button == MouseButton.Left && leftClickEvent != null)
                leftClickEvent.Execute(index);
            else if (Button == MouseButton.Right && rightClickEvent != null)
                rightClickEvent.Execute(index);
            return base.OnMouseClick(Position, Button);
        }

        public override void OnRender(RenderTarget Target)
        {
            base.OnRender(Target);

            // show hover text only for one item
            if (GetTopMostItem() == this && text != null)
            {
                Target.Layer++;
                RenderTarget bigger = Target.GetSubBuffer(new Rect(-50, -50, 132, 132));
                font.DrawText(bigger, new Vector2(66, 41), text, TextAlignment.Center, VerticalTextAlignment.Top);
            }
        }

        private ItemWindow GetTopMostItem()
        {
            if (hover.Count == 0)
                return null;

            for (int i = hover.Count - 1; i >= 0; i--)
            {
                if (!string.IsNullOrEmpty(hover[i].ItemID))
                {
                    return hover[i];
                }
            }

            return null;
        }
    }
}
