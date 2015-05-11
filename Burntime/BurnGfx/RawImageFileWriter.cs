using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Platform.Graphics;

namespace Burntime.Data.BurnGfx
{
    public class RawImageFileWriter
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

        public RawImageFileWriter(File file)
        {
            this.file = file;

            header = new ushort[0x100];
            count = 0;

            Finish();
        }

        public void Finish()
        {
            file.Seek(0, SeekPosition.Begin);
            for (int i = 0; i < 0x100; i++)
            {
                file.Write(BitConverter.GetBytes(header[i]), 0, 2);
            }
        }

        public void WriteImage()
        {
            header[count] = (ushort)file.Position;
            count++;

            _Write(file, BurnGfxData.Instance.GetRawColorTable(file.Name));
        }

        void _Write(File writer, ColorTable colors)
        {
            //byte linemax = 0xfe;

            int pos = 0;
            int jump = 4;
            int round = 0;

            writer.Write(BitConverter.GetBytes(size.x), 0, 2);
            writer.Write(BitConverter.GetBytes(size.y), 0, 2);

            int togo = size.x / 4 * size.y;

            byte[] us = new byte[2];


            int line = 1;

            us[0] = 0;
            us[1] = (byte)(size.x / 4);

            while (round != 4)
            {
                writer.Write(us, 0, 2);

                if (us[1] == 0xff)
                {
                    round++;
                    pos += 1 + 4 * (us[0]) - size.y * 320;//pos = round;
                    line = 1;
                    us[0] = 0;
                    us[1] = (byte)(size.x / 4);
                    continue;
                }
                else
                {
                    pos += 4 * (us[0]);
                    writeline(writer, colors, ref pos, us[1], jump);
                }

                if (line == size.y)
                {
                    us[1] = 0xff;
                    us[0] = (byte)((320 - size.x) / 4);
                }
                else
                {
                    us[0] = (byte)((320 - size.x) / 4);
                    us[1] = (byte)(size.x / 4);
                    line++;
                }
            }

            writer.Write(us, 0, 2);
        }

        void writeline(File writer, ColorTable colors, ref int pos, int count, int jump)
        {
            for (int i = 0; i < count; i++)
            {
                byte data = colors.GetIndex(getpixel(pos));
                writer.WriteByte(data);
                pos += jump;
            }
        }

        PixelColor getpixel(int pos)
        {
            int x = getx(pos);
            int y = gety(pos);
            if (x >= size.x || y >= size.y)
                return PixelColor.Black;

            PixelColor color = new PixelColor();
            color.b = data[(y * size.x + x) * 4 + 0];
            color.g = data[(y * size.x + x) * 4 + 1];
            color.r = data[(y * size.x + x) * 4 + 2];
            color.a = data[(y * size.x + x) * 4 + 3];
            return color;
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
