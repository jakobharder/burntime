using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.IO;
using Burntime.Data.BurnGfx;

namespace MapEditor
{
    public class Tile
    {
        public const int UPSCALE_WIDTH = 60;
        public const int UPSCALE_HEIGHT = 72;

        public const int FIRST_ID = 0;
        public const int LAST_ID = 62;
        public const int FIRST_SUBSET = 1;
        public const int LAST_SUBSET = 99;

        public Size Size;
        public Image Image;
        public Image Upscaled;
        public bool[] Mask = new bool[16];

        public String Set;
        public byte ID;
        public byte SubSet;

        public new int GetHashCode()
        {
            Bitmap bmp2 = new Bitmap(Image);

            int hash = 0;
            for (int y = 0; y < Image.Height; y++)
            {
                for (int x = 0; x < Image.Width; x++)
                {
                    Color c =  bmp2.GetPixel(x, y);
                    hash += c.R + c.G + c.B;
                }
            }

            return hash;
        }

        public bool IsEqualImage(Tile Tile)
        {
            return IsEqualImage(new Bitmap(Tile.Image));
        }

        public bool IsEqualImage(Bitmap Image)
        {
            if (Image.Width != this.Image.Width ||
                Image.Height != this.Image.Height)
                return false;

            Bitmap bmp = new Bitmap(this.Image);

            for (int y = 0; y < Image.Height; y++)
            {
                for (int x = 0; x < Image.Width; x++)
                {
                    if (Image.GetPixel(x, y) != bmp.GetPixel(x, y))
                        return false;
                }
            }

            return true;
        }

        public void ReadFromText(TextReader reader)
        {
            for (int k = 0; k < 4; k++)
            {
                String line = reader.ReadLine();
                if (line.Length < 4)
                    continue;

                char[] chrs = line.ToCharArray();
                Mask[k * 4 + 0] = (chrs[0] == '1');
                Mask[k * 4 + 1] = (chrs[1] == '1');
                Mask[k * 4 + 2] = (chrs[2] == '1');
                Mask[k * 4 + 3] = (chrs[3] == '1');
            }
        }

        public void WriteToText(TextWriter writer)
        {
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    writer.Write(Mask[y * 4 + x] ? "1" : "0");
                }
                writer.WriteLine();
            }
        }
    }
}
