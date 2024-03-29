﻿using Burntime.Platform;
using Burntime.Platform.Graphics;
using System.Collections.Generic;

namespace Burntime.Framework.GUI;

public class Container : Window
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

        public Window Last => base[Count - 1];

        readonly Container parent;
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

    protected GuiImage? background;
    public GuiImage? Background
    {
        get => background;
        set
        {
            background = value;
            if (background is not null)
                base.Size = background.Size;
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

    bool _autoSize = true;
    public override Vector2 Size
    {
        get => base.Size;
        set { base.Size = value; _autoSize = value == Vector2.Zero; }
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
            if (_autoSize && background.IsLoaded)
                base.Size = background.Size;

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
            window.MouseMove(Position - this.Position);

        return base.MouseMove(Position);
    }

    internal override void ModalLeave()
    {
        foreach (Window window in windows)
            window.ModalLeave();
    }

    internal override bool MouseDown(Vector2 position, MouseButton button)
    {
        if (!visible)
            return false;

        foreach (Window window in windows)
        {
            if (window.IsVisible && window.MouseDown(position - Position, button))
                return true;
        }

        return base.MouseDown(position, button);
    }

    internal override bool MouseUp(Vector2 position, MouseButton button)
    {
        if (!visible)
            return false;

        foreach (Window window in windows)
        {
            if (window.IsVisible && window.MouseUp(position - Position, button))
                return true;
        }

        return base.MouseUp(position, button);
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
