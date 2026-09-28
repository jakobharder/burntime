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
        public override bool IsTouchTarget => !string.IsNullOrEmpty(ItemID);
        GuiFont font;
        String text;
        CommandEvent leftClickEvent;
        CommandEvent rightClickEvent;

        static List<ItemWindow> hover = new List<ItemWindow>();
        public bool ShowHoverText { get; set; } = true;

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
            font = new GuiFont(BurntimeClassic.FontName, ClassicColors.MenuTextHover);
            font.Borders = TextBorders.Screen;
            text = null;
        }

        string itemID = "";
        Item? item;
        string? displayedSprite;
        public Item? Item
        {
            get => item;
            set { item = value; itemID = value?.ID ?? ""; RefreshItem(); }
        }
        public string? TooltipText => item?.TooltipText ?? text;
        public string ItemID
        {
            get { return item?.ID ?? itemID; }
            set
            {
                item = null;
                itemID = value;
                RefreshItem();
            }
        }

        void RefreshItem()
        {
            string? sprite = null;
            if (!string.IsNullOrEmpty(ItemID))
            {
                sprite = item?.Sprite ?? BurntimeClassic.Instance.Game.ItemTypes[ItemID].Sprite;
                text = item?.TooltipText ?? BurntimeClassic.Instance.Game.ItemTypes[ItemID].Title;
            }
            else text = null;
            if (sprite != displayedSprite)
            {
                // Keep null typed as GuiImage: a null string still invokes
                // the implicit resource conversion and crashes in ResourceID.
                if (string.IsNullOrEmpty(sprite))
                    Background = null;
                else
                    Background = sprite;
                displayedSprite = sprite;
            }
        }

        public override void OnMouseEnter()
        {
            base.OnMouseEnter();

            // set hovered item globally
            hover.Add(this);
        }

        public override void OnMouseLeave()
        {
            base.OnMouseLeave();

            hover.Remove(this);
        }

        public override bool OnMouseClick(Vector2 Position, MouseButton Button)
        {
            // prevent from clicking two overlapping items at a time
            if ((app.LastInputMode == InputMode.Touch
                ? GetTopMostItemAt(this.Position + Position) : GetTopMostItem()) != this)
                return false;

            Window[] group = Parent.Windows.GetGroup(Group);
            int index = 0;
            for (; index < group.Length; index++)
            {
                if (group[index] == this)
                    break;
            }

            if (Parent is ItemGridWindow grid)
                grid.FocusItem(index);

            if (Button == MouseButton.Left && leftClickEvent != null)
            {
                leftClickEvent.Execute(index);
                return true;
            }
            else if (Button == MouseButton.Right && rightClickEvent != null)
            {
                rightClickEvent.Execute(index);
                return true;
            }
            return false;
        }

        public bool IsTouchSelected => Parent is ItemGridWindow grid && grid.IsFocused(this);

        public override bool OnTouchTap(Vector2 position)
        {
            if (string.IsNullOrEmpty(ItemID) || GetTopMostItemAt(Position + position) != this)
                return false;

            if (Parent is not ItemGridWindow grid)
                return false;

            if (grid.FocusItem(this))
                OnMouseClick(position, MouseButton.Left);
            return true;
        }

        public override bool OnTouchLongPress(Vector2 position) =>
            OnMouseClick(position, MouseButton.Right);

        public override void OnRender(RenderTarget Target)
        {
            if (item != null) RefreshItem();
            base.OnRender(Target);

            bool showLegacyHoverText = ShowHoverText ||
                app is BurntimeClassic classic && !classic.ShowUIHints;
            if (showLegacyHoverText && (app.LastInputMode == InputMode.Touch ? IsTouchSelected : GetTopMostItem() == this) && text != null)
            {
                Target.Layer += 5;
                RenderTarget bigger = Target.GetSubBuffer(new Rect(-50, -50, 132, 132));
                font.DrawText(bigger, new Vector2(66, 41), text, TextAlignment.Center, VerticalTextAlignment.Top);
            }
        }

        private ItemWindow? GetTopMostItemAt(Vector2 position)
        {
            ItemWindow? top = null;
            foreach (var sibling in Parent.Windows)
                if (sibling is ItemWindow item && item.IsVisible &&
                    !string.IsNullOrEmpty(item.ItemID) && item.Boundings.PointInside(position))
                    top = item;
            return top;
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

        public bool IsMouseHovered => GetTopMostItem() == this;
    }
}
