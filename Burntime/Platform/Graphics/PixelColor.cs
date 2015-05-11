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
