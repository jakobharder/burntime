using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Framework.GUI
{
    public class Image : Window
    {
        protected GuiImage background;
        public GuiImage Background
        {
            get { return background; }
            set 
            { 
                background = value;
            }
        }

        bool sizeSet = false;

        public Image(Module App)
            : base(App)
        {
        }

        public Image(Module app, String file) 
            : base(app)
        {
            background = file;
        }

        public override void OnRender(IRenderTarget Target)
        {
            if (background != null)
            {
                if (!sizeSet && background.IsLoaded)
                {
                    Size = new Vector2(background.Width, background.Height);
                    sizeSet = true;
                }
                background.Update(Target.Elapsed);
            }
            Target.DrawSprite(background);
        }

        public override void OnUpdate(float Elapsed)
        {
        }

        public void OnSpriteLoaded()
        {
            Size = new Vector2(background.Width, background.Height);
        }
    }
}
