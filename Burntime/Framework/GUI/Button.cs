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
        protected GuiImage image;
        protected GuiImage hoverImage;
        public GuiImage DownImage;
        protected GuiString text;
        protected  GuiFont font;
        public GuiFont HoverFont;
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

        public GuiImage Image
        {
            get { return image; }
            set
            {
                image = value;
            }
        }

        public GuiImage HoverImage
        {
            get { return hoverImage; }
            set
            {
                hoverImage = value;
            }
        }

        public GuiString Text
        {
            get { return text; }
            set { text = value; }
        }

        public GuiFont Font
        {
            get { return font; }
            set { font = value; }
        }

        public void SetTextOnly()
        {
            if (font != null && text != null)
                Size = font.GetRect(0, 0, text).Size;
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
