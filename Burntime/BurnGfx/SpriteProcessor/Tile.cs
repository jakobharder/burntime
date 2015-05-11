using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;

using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Platform.Graphics;

namespace Burntime.Data.BurnGfx
{
    public class Tile
    {
        public const int WIDTH = 32;
        public const int HEIGHT = 32;
        public static Vector2 SIZE = new Vector2(32, 32);

        protected ushort mask;
        public int Mask
        {
            get
            {
                return mask;
            }
        }

        public int Width
        {
            get
            {
                return WIDTH;
            }
        }

        public int Height
        {
            get
            {
                return HEIGHT;
            }
        }

        public Rectangle SizeRect
        {
            get
            {
                return new Rectangle(0, 0, Width, Height);
            }
        }

        public Vector2 Size
        {
            get { return new Vector2(WIDTH, HEIGHT); }
        }

        int mapId;
        public Sprite Sprite;
        byte[] data;

        //public Tile(ByteBuffer buffer, String id)
        //{
        //    mapId = 0;
        //    Sprite = new Sprite(new SpriteLoaderByteBuffer(id, buffer));
        //    //SpriteManager.LoadSprite(Sprite, new SpriteLoaderByteBuffer(id, buffer));
        //}

        public Tile(File Stream, int mapId)
        {
            byte[] header1 = new byte[2]; // unknown
            Stream.Read(header1, 2);

            mask = Stream.ReadUShort();

            byte[] header2 = new byte[12]; // unknown
            Stream.Read(header2, 12);

            data = new byte[Width * Height];
            Stream.Read(data, Width * Height);

            this.mapId = mapId;
            //Sprite = SpriteManager.AddSprite(Stream.Name + "?" + id.ToString(), this);
        }

        //public void Render(IntPtr pRender)
        //{
        //    unsafe
        //    {
        //        ColorTable table = BurnGfxData.Instance.GetMapColorTable(mapId);

        //        PixelColor* ptr = (PixelColor*)pRender;
        //        int pos = 0;
        //        int nextstep = 0;

        //        for (int i = 0; i < Width * Height; i++)
        //        {
        //            byte fileData = data[i];

        //            int x = pos % Width;
        //            int y = (pos - x) / Width;

        //            ptr[(x + y * Width)] = table.GetColor(fileData);

        //            pos += 4;
        //            nextstep += 4;

        //            if (nextstep >= Width * Height)
        //            {
        //                pos -= Width * Height;
        //                pos++;
        //                nextstep = 0;
        //            }
        //        }

        //    }
        //}

        public void Render(System.IO.Stream s, int stride)
        {
            ColorTable table = BurnGfxData.Instance.GetMapColorTable(mapId);

            int pos = 0;
            int nextstep = 0;

            byte[] texdata = new byte[4 * Width * Height];

            for (int i = 0; i < Width * Height; i++)
            {
                byte fileData = data[i];

                int x = pos % Width;
                int y = (pos - x) / Width;

                texdata[(x + y * Width) * 4 + 0] = table.GetColor(fileData).b;
                texdata[(x + y * Width) * 4 + 1] = table.GetColor(fileData).g;
                texdata[(x + y * Width) * 4 + 2] = table.GetColor(fileData).r;
                texdata[(x + y * Width) * 4 + 3] = table.GetColor(fileData).a;

                pos += 4;
                nextstep += 4;

                if (nextstep >= Width * Height)
                {
                    pos -= Width * Height;
                    pos++;
                    nextstep = 0;
                }
            }

            for (int i = 0; i < Height; i++)
            {
                s.Write(texdata, i * 4 * Width, 4 * Width);
                s.Seek(stride - 4 * Width, System.IO.SeekOrigin.Current);
            }
        }

        //public void Draw(ref ByteBuffer Buffer, int offsetx, int offsety, ColorTable table)
        //{
        //    if (offsetx >= 0 && offsety >= 0 && offsetx + Width < Buffer.Width && offsety + Height < Buffer.Height)
        //    {
        //        for (int y = offsety; y < offsety + Height; y++)
        //        {
        //            Array.Copy(data, (y - offsety) * Width, Buffer.Data, Buffer.Offset + y * Buffer.Stride + offsetx, Width);
        //        }
        //    }
        //    else
        //    {
        //        for (int y = offsety; y < offsety + Height; y++)
        //        {
        //            if (y < 0 || y >= Buffer.Height)
        //                continue;

        //            for (int x = offsetx; x < offsetx + Width; x++)
        //            {
        //                if (x < 0 || x >= Buffer.Width)
        //                    continue;

        //                int src = ((x - offsetx) + Width * (y - offsety));
        //                int dest = Buffer.Offset + x + Buffer.Stride * y;

        //                Buffer.Data[dest] = data[src];
        //            }
        //        }
        //    }
        //}

        //public void DrawMini(ref ByteBuffer Buffer, int offsetx, int offsety, ColorTable table)
        //{
        //    int miniWidth = 4;
        //    int miniHeight = 4;

        //    for (int y = 0; y < miniHeight; y++)
        //    {
        //        if ((y + offsety) < 0 || (y + offsety) >= Buffer.Height)
        //            continue;

        //        for (int x = 0; x < miniWidth; x++)
        //        {
        //            if ((x + offsetx) < 0 || (x + offsetx) >= Buffer.Width)
        //                continue;

        //            int src = (x * Width / miniWidth) + Width * (y * Height / miniHeight);
        //            int dest = Buffer.Offset + (x + offsetx) + Buffer.Stride * (y + offsety);

        //            Buffer.Data[dest] = data[src];
        //        }
        //    }
        //}

        //public void Read(FileStream Stream, ColorTable Colors)
        //{
        //    byte[] header1 = new byte[2];
        //    Stream.Read(header1, 0, 2);

        //    byte[] m = new byte[2];
        //    Stream.Read(m, 0, 2);
        //    mask = ((ushort)(m[0] + (m[1] << 8)));

        //    byte[] header2 = new byte[12];
        //    Stream.Read(header2, 0, 12);

        //    int pos = 0;
        //    int nextstep = 0;

        //    tile = new Bitmap(Width, Height, PixelFormat.Format24bppRgb);
            
        //    tile.SetPixel(10, 20, Color.White);

        //    BitmapData data = tile.LockBits(SizeRect, ImageLockMode.ReadWrite, tile.PixelFormat);

        //    unsafe
        //    {

        //        byte* bmp = (byte*)data.Scan0;

        //        for (int i = 0; i < Width * Height; i++)
        //        {
        //            byte[] fileData = new byte[1];
        //            Stream.Read(fileData, 0, 1);

        //            int x = pos % Width;
        //            int y = (pos - x) / Width;

        //            Color c = Colors.GetColor(fileData[0]);
        //            bmp[x * 3 + data.Stride * y] = c.B;
        //            bmp[x * 3 + data.Stride * y + 1] = c.G;
        //            bmp[x * 3 + data.Stride * y + 2] = c.R;

        //            pos += 4;
        //            nextstep += 4;

        //            if (nextstep >= Width * Height)
        //            {
        //                pos -= Width * Height;
        //                pos++;
        //                nextstep = 0;
        //            }
        //        }

        //    }

        //    tile.UnlockBits(data);
        //}

    }
}
