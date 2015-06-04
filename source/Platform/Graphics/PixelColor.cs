
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
using System.Runtime.InteropServices;

namespace Burntime.Platform
{
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
    public struct PixelColor
    {
        public static PixelColor White = new PixelColor(255, 255, 255);
        public static PixelColor Black = new PixelColor(0, 0, 0);

        public byte b, g, r, a;

        public PixelColor(int r, int g, int b)
        {
            this.r = (byte)r;
            this.g = (byte)g;
            this.b = (byte)b;
            this.a = 255;
        }

        public PixelColor(int a, int r, int g, int b)
        {
            this.r = (byte)r;
            this.g = (byte)g;
            this.b = (byte)b;
            this.a = (byte)a;
        }

        public bool IsZero
        {
            get { return r == 0 && g == 0 && b == 0 && a == 0; }
        }

        public int ToInt()
        {
            return (a << 24) + (r << 16) + (g << 8) + b;
        }

        public static bool operator ==(PixelColor Left, PixelColor Right)
        {
            return Left.Equals(Right);
        }

        public static bool operator !=(PixelColor Left, PixelColor Right)
        {
            return !Left.Equals(Right);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            PixelColor c = (PixelColor)obj;
            return c.a == a && c.r == r && c.g == g && c.b == b;
        }

        public override int GetHashCode()
        {
            return a.GetHashCode() + r.GetHashCode() + g.GetHashCode() + b.GetHashCode();
        }
    }
}
