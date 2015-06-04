
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Platform
{
    [Serializable]
    public struct Rect : IEnumerable
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
        public int Width
        {
            get { return Right - Left; }
            set { Right = Left + value; }
        }
        public int Height
        {
            get { return Bottom - Top; }
            set { Bottom = Top + value; }
        }

        public static Rect operator +(Rect left, Vector2 right)
        {
            return new Rect(left.Position + right, left.Size);
        }

        public static Rect operator -(Rect left, Vector2 right)
        {
            return new Rect(left.Position - right, left.Size);
        }

        public Vector2 Center
        {
            get { return new Vector2((Left + Right) / 2, (Top + Bottom) / 2); }
        }

        public Vector2 Position
        {
            get { return new Vector2(Left, Top); }
            set
            {
                int w = Width;
                int h = Height;
                Left = value.x; Top = value.y;
                Right = Left + w;
                Bottom = Top + h;
            }
        }

        public Vector2 Size
        {
            get { return new Vector2(Width, Height); }
            set { Right = Left + value.x; Bottom = Top + value.y; }
        }

        public Rect(int left, int top, int width, int height)
        {
            Left = left;
            Top = top;
            Right = left + width;
            Bottom = top + height;
        }

        public Rect(Vector2 Position, Vector2 Size)
        {
            Left = Position.x;
            Top = Position.y;
            Right = Left + Size.x;
            Bottom = Top + Size.y;
        }

        public Rect(Rect rc)
        {
            Left = rc.Left;
            Top = rc.Top;
            Right = rc.Right;
            Bottom = rc.Bottom;
        }

        public bool PointInside(Vector2 point)
        {
            return (point.x >= Left && point.x < Right && point.y >= Top && point.y < Bottom);
        }

        public float Distance(Vector2 point)
        {
            if (PointInside(point))
                return 0;

            if (point.x < Left)
            {
                if (point.y < Top)
                {
                    return new Vector2f(point.x - Left, point.y - Top).Length;
                }
                else if (point.y > Bottom)
                {
                    return new Vector2f(point.x - Left, point.y - Bottom).Length;
                }
                else
                {
                    return System.Math.Abs(Left - point.x);
                }
            }
            else
            {
                if (point.y < Top)
                {
                    return new Vector2f(point.x - Right, point.y - Top).Length;
                }
                else if (point.y > Bottom)
                {
                    return new Vector2f(point.x - Right, point.y - Bottom).Length;
                }
                else
                {
                    return System.Math.Abs(Right - point.x);
                }
            }
        }

        public Rect Intersect(Rect Right)
        {
            Rect rc = new Rect(0, 0, 0, 0);
            rc.Left = System.Math.Max(Left, Right.Left);
            rc.Top = System.Math.Max(Top, Right.Top);
            rc.Right = System.Math.Min(this.Right, Right.Right);
            rc.Bottom = System.Math.Min(Bottom, Right.Bottom);
            if (rc.Left > rc.Right)
                rc.Left = rc.Right;
            if (rc.Top > rc.Bottom)
                rc.Top = rc.Bottom;
            return rc;
        }

        public Rect Merge(Rect right)
        {
            Rect rc = new Rect(0, 0, 0, 0);
            rc.Left = System.Math.Min(Left, right.Left);
            rc.Top = System.Math.Min(Top, right.Top);
            rc.Right = System.Math.Max(this.Right, right.Right);
            rc.Bottom = System.Math.Max(Bottom, right.Bottom);
            return rc;
        }

        public IEnumerator GetEnumerator()
        {
            return new RectEnumerator(Size, Position);
        }

        internal sealed class RectEnumerator : IEnumerator
        {
            Vector2 pos;
            Vector2 size;
            Vector2 offset;

            public RectEnumerator(Vector2 Size, Vector2 Position)
            {
                size = Size;
                offset = Position;
                pos = new Vector2(-1, 0);
            }

            public object Current
            {
                get { return offset + pos; }
            }

            public void Reset()
            {
                pos = new Vector2(-1, 0);
            }

            public bool MoveNext()
            {
                if (size.x <= 0 || size.y <= 0)
                    return false;

                pos.x++;
                while (pos.x >= size.x)
                {
                    pos.x = 0;
                    pos.y++;
                }

                return pos.y < size.y;
            }
        }
    }

    [Serializable]
    public struct Rectf
    {
        public float Left;
        public float Right;
        public float Top;
        public float Bottom;
        public float Width
        {
            get { return Right - Left; }
            set { Right = Left + value; }
        }
        public float Height
        {
            get { return Bottom - Top; }
            set { Bottom = Top + value; }
        }

        public static Rectf operator +(Rectf left, Vector2f right)
        {
            return new Rectf(left.Position + right, left.Size);
        }

        public static Rectf operator -(Rectf left, Vector2f right)
        {
            return new Rectf(left.Position - right, left.Size);
        }

        public Vector2f Center
        {
            get { return new Vector2f((Left + Right) / 2, (Top + Bottom) / 2); }
        }

        public Vector2f Position
        {
            get { return new Vector2f(Left, Top); }
            set 
            {
                float w = Width;
                float h = Height;
                Left = value.x; Top = value.y;
                Right = Left + w;
                Bottom = Top + h;
            }
        }

        public Vector2f Size
        {
            get { return new Vector2f(Width, Height); }
            set { Right = Left + value.x; Bottom = Top + value.y; }
        }

        public Rectf(float left, float top, float width, float height)
        {
            Left = left;
            Top = top;
            Right = left + width;
            Bottom = top + height;
        }

        public Rectf(Vector2f Position, Vector2f Size)
        {
            Left = Position.x;
            Top = Position.y;
            Right = Left + Size.x;
            Bottom = Top + Size.y;
        }

        public Rectf(Rectf rc)
        {
            Left = rc.Left;
            Top = rc.Top;
            Right = rc.Right;
            Bottom = rc.Bottom;
        }

        public bool PointInside(Vector2f point)
        {
            return (point.x >= Left && point.x < Right && point.y >= Top && point.y < Bottom);
        }

        public float Distance(Vector2f point)
        {
            if (PointInside(point))
                return 0;

            if (point.x < Left)
            {
                if (point.y < Top)
                {
                    return new Vector2f(point.x - Left, point.y - Top).Length;
                }
                else if (point.y > Bottom)
                {
                    return new Vector2f(point.x - Left, point.y - Bottom).Length;
                }
                else
                {
                    return System.Math.Abs(Left - point.x);
                }
            }
            else
            {
                if (point.y < Top)
                {
                    return new Vector2f(point.x - Right, point.y - Top).Length;
                }
                else if (point.y > Bottom)
                {
                    return new Vector2f(point.x - Right, point.y - Bottom).Length;
                }
                else
                {
                    return System.Math.Abs(Right - point.x);
                }
            }
        }

        public Rectf Intersect(Rectf Right)
        {
            Rectf rc = new Rectf(0, 0, 0, 0);
            rc.Left = System.Math.Max(Left, Right.Left);
            rc.Top = System.Math.Max(Top, Right.Top);
            rc.Right = System.Math.Min(this.Right, Right.Right);
            rc.Bottom = System.Math.Min(Bottom, Right.Bottom);
            if (rc.Left > rc.Right)
                rc.Left = rc.Right;
            if (rc.Top > rc.Bottom)
                rc.Top = rc.Bottom;
            return rc;
        }
    }
}
