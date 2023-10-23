using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Framework.GUI
{
    public class WindowList : List<Window>
    {
        int layer = 0;
        internal int Layer
        {
            get { return layer; }
            set
            {
                int diff = value - layer;
                layer = value;
                foreach (Window window in this)
                {
                    window.Layer += diff;
                }
            }
        }

        public Window Last
        {
            get { return base[Count - 1]; }
        }

        Container parent;
        public WindowList(Container Parent)
        {
            parent = Parent;
        }

        public static WindowList operator +(WindowList left, Window right)
        {
            lock (left)
            {
                left.Add(right);
                right.Layer = right.Layer + left.layer + 1;
                right.parent = left.parent;
            }
            return left;
        }

        public static WindowList operator -(WindowList Left, Window Right)
        {
            lock (Left)
            {
                Left.Remove(Right);
            }
            return Left;
        }

        public Window[] GetGroup(int Group)
        {
            List<Window> list = new List<Window>();
            foreach (Window window in this)
            {
                if (window.Group == Group)
                    list.Add(window);
            }

            return list.ToArray();
        }
    }

    public class Container : Window
    {
        bool sizeSet = false;

        protected GuiImage? background;
        public GuiImage? Background
        {
            get { return background; }
            set 
            { 
                background = value;
                if (background != null)
                {
                    Size = new Vector2(background.Width, background.Height);
                }
            }
        }

        public override int Layer
        {
            get { return layer; }
            set { layer = value; windows.Layer = value; }
        }

        protected WindowList windows;
        public WindowList Windows
        {
            get { return windows; }
            set { windows = value; }
        }

        public Container(Module App)
            : base(App)
        {
            windows = new WindowList(this);
            windows.Layer = layer;
        }

        internal override void Render(RenderTarget Target)
        {
            if (!visible)
                return;

            Target.Layer = Layer;
            base.Render(Target);
            Target.Layer = Layer;

            RenderTarget thisTarget = Target.GetSubBuffer(Boundings);

            if (background != null)
            {
                if (!sizeSet)
                {
                    if (background.IsLoaded)
                    {
                        if (Size.x == 0) // only set if not done already
                            Size = new Vector2(background.Width, background.Height);
                        sizeSet = true;
                    }
                }
                if (Size.x != 0)
                    thisTarget.DrawSprite((Size - background.Size) / 2, background);
                else
                    thisTarget.DrawSprite(background);
            }

            lock (windows)
            {
                foreach (Window window in windows)
                {
                    if (window.IsVisible)
                        window.Render(thisTarget);
                }
            }
        }

        internal override void Update(float elapsed)
        {
            if (!visible)
                return;

            background?.Update(elapsed);

            foreach (Window window in windows)
            {
                window.Update(elapsed);
            }

            base.Update(elapsed);
        }

        internal override bool MouseClick(Vector2 Position, MouseButton Button)
        {
            if (!visible)
                return false;

            foreach (Window window in windows)
            {
                if (window.IsVisible && window.MouseClick(Position - this.Position, Button))
                    return true;
            }

            return base.MouseClick(Position, Button);
        }

        internal override bool MouseMove(Vector2 Position)
        {
            if (!visible)
                return false;

            foreach (Window window in windows)
            {
                if (window.IsVisible)
                    window.MouseMove(Position - this.Position);
            }

            return base.MouseMove(Position);
        }

        internal override void ModalLeave()
        {
            foreach (Window window in windows)
                window.ModalLeave();
        }

        internal override void KeyPress(char Key)
        {
            if (!visible)
                return;

            foreach (Window window in windows)
            {
                if (window.IsVisible)
                    window.KeyPress(Key);
            }

            base.KeyPress(Key);
        }

        public override void OnResizeScreen()
        {
            base.OnResizeScreen();

            foreach (Window window in windows)
                window.OnResizeScreen();
        }
    }
}
