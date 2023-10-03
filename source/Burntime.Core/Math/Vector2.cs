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

        public void Min(int min)
        {
            if (x < min) x = min;
            if (y < min) y = min;
        }

        public void Min(Vector2 min)
        {
            if (x < min.x) x = min.x;
            if (y < min.y) y = min.y;
        }

        public void Max(int max)
        {
            if (x > max) x = max;
            if (y > max) y = max;
        }

        public void Max(Vector2 max)
        {
            if (x > max.x) x = max.x;
            if (y > max.y) y = max.y;
        }

        /// <summary>
        /// Clamp between min and max.
        /// </summary>
        public void Clamp(Vector2 min, Vector2 max)
        {
            if (x < min.x) x = min.x;
            if (y < min.y) y = min.y;
            if (x > max.x) x = max.x;
            if (y > max.y) y = max.y;
        }

        /// <summary>
        /// Clamp between min and one below max.
        /// </summary>
        public void ClampMaxExcluding(Vector2 minIncluding, Vector2 maxExcluding)
        {
            if (x < minIncluding.x) x = minIncluding.x;
            if (y < minIncluding.y) y = minIncluding.y;
            if (x >= maxExcluding.x) x = maxExcluding.x - 1;
            if (y >= maxExcluding.y) y = maxExcluding.y - 1;
        }

        /// <summary>
        /// Clamp within rectangle
        /// </summary>
        public void Clamp(Rect rect)
        {
            if (x < rect.Left) x = rect.Left;
            if (y < rect.Top) y = rect.Top;
            if (x >= rect.Right) x = rect.Right - 1;
            if (y >= rect.Bottom) y = rect.Bottom - 1;
        }

        public int GetIndex(Vector2 borders)
        {
            return x + y * borders.x;
        }

        public int Count
        {
            get { return x * y; }
        }

        public float Ratio => y == 0 ? 1 : (float)x / (float)y;
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

        public void Min(float min)
        {
            if (x < min) x = min;
            if (y < min) y = min;
        }

        public void Min(Vector2f min)
        {
            if (x < min.x) x = min.x;
            if (y < min.y) y = min.y;
        }

        public void Max(float Max)
        {
            if (x > Max) x = Max;
            if (y > Max) y = Max;
        }

        public void Max(Vector2f max)
        {
            if (x > max.x) x = max.x;
            if (y > max.y) y = max.y;
        }

        /// <summary>
        /// Clamp between min and max.
        /// </summary>
        public void Clamp(Vector2 min, Vector2 max)
        {
            if (x < min.x) x = min.x;
            if (y < min.y) y = min.y;
            if (x > max.x) x = max.x;
            if (y > max.y) y = max.y;
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

        public float Ratio => y == 0 ? 1 : x / y;
    }

}
