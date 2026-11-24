using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform.Graphics;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.GUI
{
    public enum ExchangeResult
    {
        Ok,
        Ng,
        None
    }

    class ExchangeWindow : Container
    {
        String title;
        GuiFont font;
        ExchangeResult exchangeResult;
        readonly bool compact;
        internal const int CompactHeight = 64;
        internal const int CompactAreaHeight = 160;

        ItemGridWindow grid;
        public ItemGridWindow Grid
        {
            get { return grid; }
            set { grid = value; }
        }

        public String Title
        {
            get { return title; }
            set { title = value; }
        }

        public ExchangeResult ExchangeResult
        {
            get { return exchangeResult; }
            set { exchangeResult = value; }
        }

        public LogicEvent LeftClickItemEvent = null;
        public LogicEvent RightClickItemEvent = null;

        public ExchangeWindow(Module App)
            : base(App)
        {
            compact = App.SmallDeviceUi;
            if (!compact)
                Background = "inv.raw?1";

            grid = new ItemGridWindow(App);
            grid.Position = new Vector2(8, 19);
            grid.Spacing = new Vector2(4, 7);
            if (compact)
            {
                Size = new Vector2(96, CompactHeight);
                grid.Position = Vector2.Zero;
                grid.Spacing = Vector2.Zero;
            }
            grid.Grid = new Vector2(3, 2);
            grid.LeftClickItemEvent += OnLeftClickItem;
            grid.RightClickItemEvent += OnRightClickItem;
            Windows += grid;

            font = new GuiFont(BurntimeClassic.FontName, ClassicColors.InventoryText);
            font.Borders = TextBorders.Screen;
        }

        public override void OnRender(RenderTarget Target)
        {
            if (compact)
            {
                Target.RenderRect(Vector2.Zero, Size, new PixelColor(128, 0, 0, 0));
                return;
            }
            font.DrawText(Target, new Vector2(63, 6), title, TextAlignment.Center, VerticalTextAlignment.Top);
            Target.Layer++;
            Target.RenderRect(new Vector2(10, 7), new Vector2(12, 5), StatusColor);
            Target.Layer--;
        }

        internal PixelColor StatusColor => exchangeResult switch
        {
            ExchangeResult.Ok => ClassicColors.StatusSuccess,
            ExchangeResult.Ng => ClassicColors.StatusFailure,
            _ => ClassicColors.StatusInactive
        };

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
    }
}
