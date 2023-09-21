
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
using System.Collections.Generic;
using System.Text;

namespace Burntime.Platform
{
    [Serializable]
    public struct Vector2
    {
        public static readonly Vector2 Zero = new Vector2(0, 0);
        public static readonly Vector2 One = new Vector2(1, 1);

        public int x, y;

        public override string ToString()
        {
            return x.ToString() + "x" + y.ToString();
        }

        //public Vector2()
        //{
        //    x = 0;
        //    y = 0;
        //}

        public Vector2(Vector2 v)
        {
            x = v.x;
            y = v.y;
        }

        public Vector2(int X, int Y)
        {
            x = X;
            y = Y;
        }

        public int Length
        {
            get { return (int)System.Math.Sqrt(x * x + y * y); }
        }

        public static bool operator > (Vector2 left, Vector2 right)
        {
            return left.x > right.x || left.y > right.y;
        }

        public static bool operator <(Vector2 left, Vector2 right)
        {
            return left.x < right.x || left.y < right.y;
        }

        public static Vector2 operator -(Vector2 left, Vector2 right)
        {
            return new Vector2(left.x - right.x, left.y - right.y);
        }

        public static Vector2 operator -(Vector2 left)
        {
            return new Vector2(-left.x, -left.y);
        }

        public static Vector2 operator -(Vector2 left, int right)
        {
            return new Vector2(left.x - right, left.y - right);
        }

        public static Vector2 operator +(Vector2 left, Vector2 right)
        {
            return new Vector2(left.x + right.x, left.y + right.y);
        }

        public static Vector2 operator +(Vector2 left, int right)
        {
            return new Vector2(left.x + right, left.y + right);
        }

        public static Vector2 operator /(Vector2 left, Vector2 right)
        {
            return new Vector2(left.x / right.x, left.y / right.y);
        }

        public static Vector2 operator /(Vector2 left, float right)
        {
            return new Vector2((int)(left.x / right), (int)(left.y / right));
        }

        public static Vector2 operator *(Vector2 left, float right)
        {
            return new Vector2((int)(left.x * right), (int)(left.y * right));
        }

        public static Vector2 operator *(Vector2 left, Vector2 right)
        {
            return new Vector2(left.x * right.x, left.y * right.y);
        }

        public static Vector2 operator %(Vector2 left, Vector2 right)
        {
            return new Vector2(left.x % right.x, left.y % right.y);
        }

        public static bool operator ==(Vector2 left, Vector2 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector2 left, Vector2 right)
        {
            return !left.Equals(right);
        }

        public static implicit operator Vector2f(Vector2 left)
        {
            return new Vector2f(left.x, left.y);
        }

        public static implicit operator Rect(Vector2 left)
        {
            return new Rect(Vector2.Zero, left);
        }

        public override bool Equals(object obj)
        {
            if (obj is Vector2)
            {
                Vector2 v = (Vector2)obj;
                return x == v.x && y == v.y;
            }
            else return false;
        }

        public override int GetHashCode()
        {
            return x.GetHashCode() + y.GetHashCode();
        }

        public void ThresholdLT(int threshold)
        {
            if (x < threshold) x = threshold;
            if (y < threshold) y = threshold;
        }

        public void ThresholdLT(Vector2 threshold)
        {
            if (x < threshold.x) x = threshold.x;
            if (y < threshold.y) y = threshold.y;
        }

        public void ThresholdGT(int threshold)
        {
            if (x > threshold) x = threshold;
            if (y > threshold) y = threshold;
        }

        public void ThresholdGT(Vector2 threshold)
        {
            if (x > threshold.x) x = threshold.x;
            if (y > threshold.y) y = threshold.y;
        }

        public int GetIndex(Vector2 borders)
        {
            return x + y * borders.x;
        }

        public int Count
        {
            get { return x * y; }
        }
    }

    [Serializable]
    public struct Vector2f
    {
        public static readonly Vector2f Zero = new Vector2f(0, 0);

        public float x, y;

        //public Vector2f()
        //{
        //    x = 0;
        //    y = 0;
        //}

        public Vector2f(Vector2f v)
        {
            x = v.x;
            y = v.y;
        }

        public Vector2f(float X, float Y)
        {
            x = X;
            y = Y;
        }

        public float Length
        {
            get { return (float)System.Math.Sqrt(x * x + y * y); }
        }

        public static Vector2f operator -(Vector2f left, Vector2f right)
        {
            return new Vector2f(left.x - right.x, left.y - right.y);
        }

        public static Vector2f operator -(Vector2f left)
        {
            return new Vector2f(-left.x, -left.y);
        }

        public static Vector2f operator -(Vector2f left, float right)
        {
            return new Vector2f(left.x - right, left.y - right);
        }

        public static Vector2f operator +(Vector2f left, Vector2f right)
        {
            return new Vector2f(left.x + right.x, left.y + right.y);
        }

        public static Vector2f operator +(Vector2f left, float right)
        {
            return new Vector2f(left.x + right, left.y + right);
        }

        public static Vector2f operator /(Vector2f left, Vector2f right)
        {
            return new Vector2f(left.x / right.x, left.y / right.y);
        }

        public static Vector2f operator /(Vector2f left, float right)
        {
            return new Vector2f((left.x / right), (left.y / right));
        }

        public static Vector2f operator *(Vector2f left, float right)
        {
            return new Vector2f((left.x * right), (left.y * right));
        }

        public static Vector2f operator *(Vector2f left, Vector2f right)
        {
            return new Vector2f(left.x * right.x, left.y * right.y);
        }

        public static Vector2f operator %(Vector2f left, Vector2f right)
        {
            return new Vector2f(left.x % right.x, left.y % right.y);
        }

        public static bool operator ==(Vector2f left, Vector2f right)
        {
            return left.x == right.x && left.y == right.y;
        }

        public static bool operator !=(Vector2f left, Vector2f right)
        {
            //if (!(left is Vector2f && right is Vector2f))
            //    return false;

            return left.x != right.x || left.y != right.y;
        }

        public static implicit operator Vector2 (Vector2f left)
        {
            return new Vector2((int)(left.x + 0.5f), (int)(left.y + 0.5f));
        }

        public override bool Equals(object obj)
        {
            Vector2f v = (Vector2f)obj;
            return x == v.x && y == v.y;
        }

        public override int GetHashCode()
        {
            return x.GetHashCode() + y.GetHashCode();
        }

        public void ThresholdLT(float threshold)
        {
            if (x < threshold) x = threshold;
            if (y < threshold) y = threshold;
        }

        public void ThresholdLT(Vector2f threshold)
        {
            if (x < threshold.x) x = threshold.x;
            if (y < threshold.y) y = threshold.y;
        }

        public void ThresholdGT(float threshold)
        {
            if (x > threshold) x = threshold;
            if (y > threshold) y = threshold;
        }

        public void ThresholdGT(Vector2f threshold)
        {
            if (x > threshold.x) x = threshold.x;
            if (y > threshold.y) y = threshold.y;
        }

        public void Normalize()
        {
            float l = Length;
            if (l == 0)
            {
                x = 0;
                y = 0;
            }
            else
            {
                x /= l;
                y /= l;
            }
        }
    }

}
