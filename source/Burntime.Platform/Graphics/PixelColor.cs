using System.Runtime.InteropServices;

namespace Burntime.Platform
{
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
    public struct PixelColor
    {
        public static PixelColor White { get; } = new(255, 255, 255);
        public static PixelColor Black { get; } = new(0, 0, 0);
        public static PixelColor Transparent { get; } = new(0, 255, 255, 255);

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
