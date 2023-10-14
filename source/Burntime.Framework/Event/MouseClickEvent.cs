using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;

namespace Burntime.Framework.Event
{
    public class MouseClickEvent : List<MouseClickHandler>
    {
        public static MouseClickEvent operator +(MouseClickEvent Left, MouseClickHandler Right)
        {
            if (Left == null)
                Left = new MouseClickEvent();
            Left.Add(Right);
            return Left;
        }

        public static MouseClickEvent operator +(MouseClickEvent Left, MouseClickInterface Right)
        {
            if (Left == null)
                Left = new MouseClickEvent();
            Left.Add(new MouseClickHandler(Right));
            return Left;
        }

        public static implicit operator MouseClickEvent(MouseClickHandler Right)
        {
            return new MouseClickEvent() + Right;
        }

        public static implicit operator MouseClickEvent(MouseClickInterface Right)
        {
            return new MouseClickEvent() + (MouseClickHandler)Right;
        }

        public void Execute(Vector2 Position, MouseButton Button)
        {
            foreach (MouseClickHandler cmd in this)
            {
                cmd.Execute(Position, Button);
            }
        }
    }

    public delegate void MouseClickInterface(Vector2 Position, MouseButton Button);

    public class MouseClickHandler
    {
        MouseClickInterface method;

        public MouseClickHandler(MouseClickInterface Method)
        {
            method = Method;
        }

        public static implicit operator MouseClickHandler(MouseClickInterface Right)
        {
            return new MouseClickHandler(Right);
        }

        public void Execute(Vector2 Position, MouseButton Button)
        {
            method.Invoke(Position, Button);
        }
    }
}
