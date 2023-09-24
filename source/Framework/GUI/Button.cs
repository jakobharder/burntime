using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework.Event;

namespace Burntime.Framework.GUI
{
    public class Button : Window
    {
        public VerticalTextAlignment TextVerticalAlign = VerticalTextAlignment.Center;
        public TextAlignment TextHorizontalAlign = TextAlignment.Center;

        protected bool isDown = false;
        protected bool isHover;

        public bool IsHover
        {
            get { return isHover; }
        }

        bool sizeSet = false;

        public CommandEvent Command;

        public GuiImage Image { get; set; }
        public GuiImage HoverImage { get; set; }
        public GuiImage DownImage { get; set; }

        public GuiString Text { get; set; }
        public GuiFont Font { get; set; }
        public GuiFont HoverFont { get; set; }

        public GuiString ToolTipText { get; set; }
        public GuiFont ToolTipFont { get; set; }

        public void SetTextOnly()
        {
            if (Font != null && Text != null)
                Size = Font.GetRect(0, 0, Text).Size;
            TextVerticalAlign = VerticalTextAlignment.Top;
            TextHorizontalAlign = TextAlignment.Left;
            sizeSet = true;
        }

        public Button(Module App)
            : base(App)
        {
        }

        public override void OnRender(RenderTarget Target)
        {
            if (!sizeSet)
            {
                if (Image != null && Image.IsLoaded)
                {
                    sizeSet = true;
                    Size = new Vector2(Image.Width, Image.Height);
                }
                else if (HoverImage != null && HoverImage.IsLoaded)
                {
                    sizeSet = true;
                    Size = new Vector2(HoverImage.Width, HoverImage.Height);
                }
                else if (DownImage != null && DownImage.IsLoaded)
                {
                    sizeSet = true;
                    Size = new Vector2(DownImage.Width, DownImage.Height);
                }
            }

            // preload hover and down images
            if (HoverImage != null)
                HoverImage.Touch();
            if (DownImage != null)
                DownImage.Touch();

            if (isHover && HoverImage != null)
            {
                Target.DrawSprite(HoverImage);
            }
            else if (isDown)
            {
                Target.DrawSprite(DownImage);
            }
            else
            {
                Target.DrawSprite(Image);
            }

            if (Text != null && Font != null)
            {
                Vector2 textpos;
                if (TextHorizontalAlign == TextAlignment.Left)
                    textpos.x = 0;
                else if (TextHorizontalAlign == TextAlignment.Center)
                    textpos.x = Size.x / 2;
                else
                    textpos.x = Size.x;
                if (TextVerticalAlign == VerticalTextAlignment.Top)
                    textpos.y = 0;
                else if (TextVerticalAlign == VerticalTextAlignment.Center)
                    textpos.y = Size.y / 2;
                else
                    textpos.y = Size.y;

                if (isHover && HoverFont != null)
                    HoverFont.DrawText(Target, textpos, Text, TextHorizontalAlign, TextVerticalAlign);
                else
                    Font.DrawText(Target, textpos, Text, TextHorizontalAlign, TextVerticalAlign);
            }

            if (IsHover && ToolTipText is not null && ToolTipFont is not null)
            {
                Vector2 textpos;
                textpos.x = Size.x / 2;
                textpos.y = -2;

                ToolTipFont.DrawText(Target, textpos, ToolTipText, TextAlignment.Center, VerticalTextAlignment.Bottom);
            }
        }

        public override void OnMouseEnter()
        {
            isHover = true;
        }

        public override void OnMouseLeave()
        {
            isHover = false;
        }

        public override bool OnMouseClick(Vector2 Position, MouseButton Button)
        {
            return OnButtonClick();
        }

        public virtual bool OnButtonClick()
        {
            if (Command != null)
            {
                Command.Execute();
                return false;
            }

            return false;
        }
    }
}
