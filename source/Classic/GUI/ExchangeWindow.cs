using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform.Graphics;
using Burntime.Classic.Logic;

namespace Burntime.Classic.GUI
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
            Background = "inv.raw?1";

            grid = new ItemGridWindow(App);
            grid.Position = new Vector2(8, 19);
            grid.Spacing = new Vector2(4, 7);
            grid.Grid = new Vector2(3, 2);
            grid.LeftClickItemEvent += OnLeftClickItem;
            grid.RightClickItemEvent += OnRightClickItem;
            Windows += grid;

            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(128, 136, 192));
            font.Borders = TextBorders.Screen;
        }

        public override void OnRender(IRenderTarget Target)
        {
            font.DrawText(Target, new Vector2(63, 6), title, TextAlignment.Center, VerticalTextAlignment.Top);

            Target.Layer++;

            PixelColor color;
            switch (exchangeResult)
            {
                case ExchangeResult.Ok: color = new PixelColor(0, 156, 0); break;
                case ExchangeResult.Ng: color = new PixelColor(208, 0, 0); break;
                default: color = new PixelColor(72, 72, 116); break;
            }

            Target.RenderRect(new Vector2(10, 7), new Vector2(12, 5), color);

            Target.Layer--;
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
    }
}
