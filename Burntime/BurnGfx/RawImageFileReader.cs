using System;

using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Platform.Graphics;

namespace Burntime.Data.BurnGfx
{
    public class RawImageFileReader
    {
        int count;
        ushort[] header;
        Vector2 size;
        byte[] data;
        File file;

        public int ImageCount
        {
            get { return count; }
        }

        public Vector2 Size
        {
            get { return size; }
            set { size = value; }
        }

        public byte[] Data
        {
            get { return data; }
            set { data = value; }
        }

        public RawImageFileReader(File File)
        {
            //byte[] data = new byte[File.Length];
            //File.Read(data, File.Length);
            //file = new File(new System.IO.MemoryStream(data));
            file = File;
        }

        public void ReadHeader()
        {
            ushort[] header = new ushort[0x1000];
            int endread = 0x1000;

            for (int i = 0; i < endread / 2; i++)
            {
                header[i] = file.ReadUShort();
                if (i == 0)
                    endread = header[i];

                if (header[i] == 0 && count == 0)
                {
                    count = i;
                    break;
                }

                if (header[i] >= file.Length)
                {
                    count = i;
                    Log.Warning("frame out of index in " + file.Name);
                    break;
                }
            }

            this.header = new ushort[count];
            for (int i = 0; i < count; i++)
                this.header[i] = header[i];
        }

        public bool ReadImage(int index)
        {
            if (header.Length <= index + 1)
                return _Read(file.GetSubFile(header[index], 0), BurnGfxData.Instance.GetRawColorTable(file.Name));
            else
                return _Read(file.GetSubFile(header[index], header[index + 1] - 1), BurnGfxData.Instance.GetRawColorTable(file.Name));
        }

        bool _Read(File reader, ColorTable colors)
        {
            int pos = 0;

            int jump = 4;

            int round = 0;

            size = new Vector2();
            size.x = reader.ReadUShort();
            size.y = reader.ReadUShort();
            if (size.x > 2048 || size.y > 2048)
                return false;

            //bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            byte[] us = new byte[2];
            data = new byte[4 * size.x * size.y];

            int read = reader.Read(us, 0, 2);
            while (!reader.IsEOF && !(/*gety(pos) >= size.y ||*/ round == 4))
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

            return true;
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

        void setpixel(int pos, PixelColor color)
        {
            int x = getx(pos);
            int y = gety(pos);
            if (x >= size.x || y >= size.y)
                return;

            data[(y * size.x + x) * 4 + 0] = color.b;
            data[(y * size.x + x) * 4 + 1] = color.g;
            data[(y * size.x + x) * 4 + 2] = color.r;
            data[(y * size.x + x) * 4 + 3] = color.a;
        }

        int getx(int pos)
        {
            return pos % 320;
        }

        int gety(int pos)
        {
            return (pos - getx(pos)) / 320;
        }
    }
}
