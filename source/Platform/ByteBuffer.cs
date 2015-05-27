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
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.IO;

using Burntime.Platform.Graphics;

namespace Burntime.Platform
{
    public interface IByteBuffer
    {
        int Width
        {
            get;
        }

        int Height
        {
            get;
        }

        PixelColor[] Data
        {
            get;
        }
    }

    public class ByteBuffer : IByteBuffer
    {
        int width;
        public int Width
        {
            get
            {
                return width;
            }
            set
            {
                width = value;
            }
        }

        int height;
        public int Height
        {
            get
            {
                return height;
            }
            set
            {
                height = value;
            }
        }

        int stride;
        public int Stride
        {
            get
            {
                return stride;
            }
            set
            {
                stride = value;
            }
        }

        int offset;
        public int Offset
        {
            get
            {
                return offset;
            }
            set
            {
                offset = value;
            }
        }

        PixelColor[] data;
        public PixelColor[] Data
        {
            get
            {
                return data;
            }
            set
            {
                data = value;
            }
        }

        public ByteBuffer(int Width, int Height, PixelColor[] Data)
        {
            width = Width;
            height = Height;
            data = Data;
            offset = 0;
            stride = width;
        }

        public ByteBuffer SubBuffer(int x, int y, int width, int height)
        {
            if (x < 0)
                x = 0;
            if (y < 0)
                y = 0;
            ByteBuffer b = new ByteBuffer(System.Math.Min(width, this.width - x), System.Math.Min(height, this.height - y), data);
            b.offset = Offset + x + Stride * y;
            b.Stride = stride;
            return b;
        }

        public void DrawBuffer(ByteBuffer Buffer, int offx, int offy)
        {
            DrawBuffer(Buffer, offx, offy, -1);
        }

        public void DrawBuffer(ByteBuffer Buffer, int offx, int offy, int colorkey)
        {
            if (colorkey == -1)
            {
                for (int y = offy; y < offy + Buffer.Height; y++)
                {
                    if (y < 0 || y >= height)
                        continue;

                    int start = 0;
                    if (offx < 0)
                        start = -offx;
                    int length = Buffer.Width;
                    length -= start;
                    if (offx + start + length >= width)
                        length = width - offx - start;

                    if (length <= 0)
                        continue;

                    Array.Copy(Buffer.Data, Buffer.Offset + start * 3 + Buffer.Stride * (y - offy), data, Offset + (offx + start) * 3 + y * Stride, length);
                }
            }
            else
            {
                for (int y = offy; y < offy + Buffer.Height; y++)
                {
                    for (int x = offx; x < offx + Buffer.Width; x++)
                    {
                        if (x < 0 || y < 0 || x >= width || y >= height)
                            continue;

                        if (Buffer.Data[Buffer.Offset + (x - offx) + Buffer.Stride * (y - offy)].b == colorkey &&
                            Buffer.Data[Buffer.Offset + (x - offx) + Buffer.Stride * (y - offy)].g == colorkey &&
                            Buffer.Data[Buffer.Offset + (x - offx) + Buffer.Stride * (y - offy)].r == colorkey)
                            continue;

                        data[Offset + x + Stride * y] = Buffer.Data[Buffer.Offset + (x - offx) + Buffer.Stride * (y - offy)];
                    }
                }
            }
        }

        public void DrawPixel(int x, int y, byte r, byte g, byte b)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;
            data[Offset + x + Stride * y].b = b;
            data[Offset + x + Stride * y].g = g;
            data[Offset + x + Stride * y].r = r;
            data[Offset + x + Stride * y].a = 255;
        }

        public void DrawPixel(int x, int y, byte a, byte r, byte g, byte b)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;
            data[Offset + x + Stride * y].b = b;
            data[Offset + x + Stride * y].g = g;
            data[Offset + x + Stride * y].r = r;
            data[Offset + x + Stride * y].a = a;
        }

        public void DrawLine(int x1, int y1, int x2, int y2, int r, int g, int b)
        {
            int diffx = x1 - x2;
            int diffy = y1 - y2;

            if (System.Math.Abs(diffx) > System.Math.Abs(diffy))
            {
                for (int x = x1; x <= x2; x++)
                {
                    DrawPixel(x, y1 + (y2 - y1) * (x - x1) / (x2 - x1), (byte)r, (byte)g, (byte)b);
                }
            }
            else
            {
                for (int y = y1; y <= y2; y++)
                {
                    DrawPixel(x1 + (x2 - x1) * (y - y1) / (y2 - y1), y, (byte)r, (byte)g, (byte)b);
                }
            }
        }
        public void GridRect(int x, int y, int w, int h)
        {
            int i = 0;

            for (int iy = y; iy < y + h; iy++)
            {
                if (iy < 0 || iy >= height)
                    continue;


                for (int ix = x; ix < x + w; ix++)
                {
                    if (ix < 0 || ix >= width)
                        continue;

                    if (i % 2 == 0)
                    {
                        data[Offset + ix + stride * iy].r = 100;
                        data[Offset + ix + stride * iy].g = 100;
                        data[Offset + ix + stride * iy].b = 100;
                    }

                    i++;
                }
                i++;
            }
        }

        public void FillRect(int x, int y, int w, int h, int c)
        {
            //for (int iy = y; iy < y + h; iy++)
            //{
            //    if (iy < 0 || iy >= height)
            //        continue;

            //    for (int ix = x; ix < x + w; ix++)
            //    {
            //        if (ix < 0 || ix >= width)
            //            continue;

            //        data[Offset + ix * 3 + stride * iy] = ColorTable.Last.GetColor(c).B;
            //        data[Offset + ix * 3 + stride * iy + 1] = ColorTable.Last.GetColor(c).G;
            //        data[Offset + ix * 3 + stride * iy + 2] = ColorTable.Last.GetColor(c).R;
            //    }
            //}
        }

        public void DarkenRect(int x, int y, int w, int h)
        {
            DarkenRect(x, y, w, h, 50);
        }

        public void DarkenRect(int x, int y, int w, int h, int v)
        {
            for (int iy = y; iy < y + h; iy++)
            {
                if (iy < 0 || iy >= height)
                    continue;

                for (int ix = x; ix < x + w; ix++)
                {
                    if (ix < 0 || ix >= width)
                        continue;


                    int b = data[offset + ix + stride * iy].b;
                    b = System.Math.Max(0, b - v);
                    data[Offset + ix + stride * iy].b = (byte)b;
                    int g = data[offset + ix + stride * iy].g;
                    g = System.Math.Max(0, g - v);
                    data[Offset + ix+ stride * iy].g = (byte)g;
                    int r = data[offset + ix + stride * iy].r;
                    r = System.Math.Max(0, r - v);
                    data[Offset + ix + stride * iy].r = (byte)r;
                    
                    
                    //int index = ColorTable.Last.Find(r, g, b);
                    //index = Math.Max(0, index - 4);

                    //data[offset + ix * 3 + stride * iy] = ColorTable.Last.GetColor(index).B;
                    //data[offset + ix * 3 + stride * iy + 1] = ColorTable.Last.GetColor(index).G;
                    //data[offset + ix * 3 + stride * iy + 2] = ColorTable.Last.GetColor(index).R;
                }
            }
        }

        public void DrawRect(int x1, int y1, int x2, int y2, byte r, byte g, byte b)
        {
            for (int x = x1; x <= x2; x++)
            {
                DrawPixel(x, y1, r, g, b);
                DrawPixel(x, y2, r, g, b);
            }
            for (int y = y1; y <= y2; y++)
            {
                DrawPixel(x1, y, r, g, b);
                DrawPixel(x2, y, r, g, b);
            }
        }

        public void Write(Stream Stream, int Stride)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Stream.WriteByte(data[x + y * width].b);
                    Stream.WriteByte(data[x + y * width].g);
                    Stream.WriteByte(data[x + y * width].r);
                    Stream.WriteByte(data[x + y * width].a);
                }

                for (int i = 0; i < (Stride - width * 4); i++)
                    Stream.WriteByte(0);
            }
        }
    }
}
