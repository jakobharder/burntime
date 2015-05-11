/*
 *  Burntime Platform
 *  Copyright (C) 2009
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 *  authors: 
 *    Juernjakob Harder (yn.harada@gmail.com)
 * 
*/

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
