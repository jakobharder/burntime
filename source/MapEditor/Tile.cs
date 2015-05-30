
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
using System.Drawing;

namespace MapEditor
{
    public class Tile
    {
        public const int FIRST_ID = 0;
        public const int LAST_ID = 62;
        public const int FIRST_SUBSET = 1;
        public const int LAST_SUBSET = 99;

        public Size Size;
        public Image Image;
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
    }
}
