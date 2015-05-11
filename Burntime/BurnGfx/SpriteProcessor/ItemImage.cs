using System;
using System.Collections.Generic;

using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Platform.Graphics;

namespace Burntime.Data.BurnGfx
{
    public class ItemImage
    {
        //protected Bitmap bmp;
        //public Image Image
        //{
        //    get
        //    {
        //        return bmp;
        //    }
        //}

        protected int width;
        protected int height;
        public int Width
        {
            get { return width; }
        }

        public int Height
        {
            get { return height; }
        }

        PixelColor[] data;
        virtual public PixelColor[] Data
        {
            get 
            {
                if (data == null)
                    Load();
                return data; 
            }
        }

        protected string id;
        public string ID
        {
            get { return id; }
        }

        protected ItemImage()
        {
        }

        public ItemImage(String file)
        {
            id = file + "?0";

            this.file = file;// "burngfx:" + file;
            pos = 0;
        }

        public ItemImage(String file, int pos)
        {
            id = file + "?" + pos.ToString();

            //if (file.StartsWith("burngfx:"))
            //    this.file = file;
            //else
            //    this.file = "burngfx:" + file;
            this.file = file;
            this.pos = pos;
        }

        String file;
        int pos;
        public void Load()
        {
            Read(FileSystem.GetFile(file), BurnGfxData.Instance.GetRawColorTable(file), pos);
        }

        public void Read(File reader, ColorTable colors, int startpos)
        {
            ushort[] header = new ushort[0x100];

            for (int i = 0; i < 0x100; i++)
            {
                header[i] = reader.ReadUShort();
            }

            reader.Seek(header[startpos], SeekPosition.Begin);

            int pos = 0;

            int jump = 4;

            int round = 0;

            width = reader.ReadUShort();
            height = reader.ReadUShort();

            //bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            byte[] us = new byte[2];
            data = new PixelColor[width * height];

            int read = reader.Read(us, 0, 2);
            while (!reader.IsEOF && !(gety(pos) >= height || round == 4))
            {

                if (us[1] == 0xff)
                {
                    round++;
                    pos = round;
                }
                else
                {
                    pos += 4 * (us[0]);
                    readline(reader, colors, ref pos, us[1], jump);
                }

                read = reader.Read(us, 0, 2);

            }
        }

        void readline(File reader, ColorTable colors, ref int pos, int count, int jump)
        {
            for (int i = 0; i < count && !reader.IsEOF; i++)
            {
                byte data = reader.ReadByte();
                setpixel(pos, colors.GetColor(data));
                pos += jump;
            }
        }


        //void readline(Reader reader, ref int pos, int count, int jump)
        //{
        //    for (int i = 0; i < count && !reader.EOF; i++)
        //    {
        //        byte color = reader.ReadByte();
                
        //        int x = getx(pos);
        //        int y = gety(pos);
        //        if (x >= width || y >= height)
        //            continue;

        //        data[x + y * width] = color;

        //        pos += jump;
        //    }
        //}

        void setpixel(int pos, PixelColor color)
        {
            int x = getx(pos);
            int y = gety(pos);
            if (x >= width || y >= height)
                return;
            //bmp.SetPixel(x, y, color);

            data[(y * width + x)].a = color.a;
            data[(y * width + x)].b = color.b;
            data[(y * width + x)].g = color.g;
            data[(y * width + x)].r = color.r;
        }

        int getx(int pos)
        {
            return pos % 320;
        }

        int gety(int pos)
        {
            return (pos - getx(pos)) / 320;
        }

        //public void Draw(ByteBuffer Target, int x, int y)
        //{
        //    Draw(Target, x, y, -1);
        //}

        //public virtual void Draw(ByteBuffer Target, int offsetx, int offsety, int colorkey)
        //{
        //    ColorTable table = ColorTable.Last;

        //    for (int y = offsety; y < offsety + height; y++)
        //    {
        //        if (y < 0 || y >= Target.Height)
        //            continue;

        //        for (int x = offsetx; x < offsetx + width; x++)
        //        {
        //            if (x < 0 || x >= Target.Width)
        //                continue;

        //            int src = (x - offsetx) + width * (y - offsety);
        //            int dest = Target.Offset + x * 3 + Target.Stride * y;

        //            if (data[src] == colorkey)
        //                continue;

        //            Target.Data[dest] = table.GetColor(data[src]).B;
        //            Target.Data[dest + 1] = table.GetColor(data[src]).G;
        //            Target.Data[dest + 2] = table.GetColor(data[src]).R;
        //        }
        //    }
        //}

        //public virtual void DrawSmall(ByteBuffer Target, int offsetx, int offsety, int colorkey, int size)
        //{
        //    ColorTable table = ColorTable.Last;

        //    for (int y = offsety; y <= offsety + height / size; y++)
        //    {
        //        if (y < 0 || y >= Target.Height)
        //            continue;

        //        for (int x = offsetx; x <= offsetx + width / size; x++)
        //        {
        //            if (x < 0 || x >= Target.Width)
        //                continue;

        //            int dest = Target.Offset + x * 3 + Target.Stride * y;

        //            int r = 0;
        //            int g = 0;
        //            int b = 0;
        //            int a = 0;

        //            int cc = 0;

        //            for (int sy = 0; sy < size; sy++)
        //            {
        //                for (int sx = 0; sx < size; sx++)
        //                {
        //                    int tsx = (x - offsetx) * size + sx;
        //                    int tsy = (y - offsety) * size + sy;
        //                    if (tsx >= width || tsy >= height)
        //                        continue;

        //                    int src = tsx + width * tsy;

        //                    if (data[src] == colorkey)
        //                        continue;

        //                    Color c = table.GetColor(data[src]);
        //                    r += c.R;
        //                    g += c.G;
        //                    b += c.B;
        //                    a += 255;
        //                    cc++;
        //                }
        //            }

        //            if (cc == 0)
        //                continue;

        //            a /= size * size;
        //            r /= cc;
        //            g /= cc;
        //            b /= cc;

        //            Target.Data[dest] = (byte)(Target.Data[dest] * (255 - a) / 255 + b * a / 255);
        //            Target.Data[dest + 1] = (byte)(Target.Data[dest + 1] * (255 - a) / 255 + g * a / 255);
        //            Target.Data[dest + 2] = (byte)(Target.Data[dest + 2] * (255 - a) / 255 + r * a / 255);
        //        }
        //    }
        //}
        //public void save(String file)
        //{
        //    Bitmap bmp = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);


        //    System.Drawing.Imaging.BitmapData bdata = bmp.LockBits(new Rectangle(0, 0, Width, Height), System.Drawing.Imaging.ImageLockMode.ReadOnly,
        //        System.Drawing.Imaging.PixelFormat.Format24bppRgb);


        //    ByteBuffer target = new ByteBuffer(Width, Height, new byte[3 * Width * Height]);
        //    Draw(target, 0, 0);

        //    System.Runtime.InteropServices.Marshal.Copy(target.Data, 0, bdata.Scan0, target.Height * target.Stride);
        //    bmp.UnlockBits(bdata);

        //    bmp.Save(BurnGfxSetting.Singleton.DataPath + "\\..\\output\\items\\" + file + ".png");
        //}
    }
}
