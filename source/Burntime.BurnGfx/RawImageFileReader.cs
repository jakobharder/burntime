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
            count = 0;
            header = Array.Empty<ushort>();
            if (file == null || file.Length < sizeof(ushort))
            {
                Log.Warning("Animation header is missing or empty: " + (file?.Name ?? "<unknown>"));
                return;
            }

            file.Seek(0, SeekPosition.Begin);
            int headerSize = file.ReadUShort();
            if (headerSize < 2 || headerSize % 2 != 0 || headerSize > file.Length)
            {
                Log.Warning("Invalid animation header in " + file.Name);
                return;
            }

            ushort[] offsets = new ushort[headerSize / 2];
            offsets[0] = (ushort)headerSize;
            for (int i = 1; i < offsets.Length; i++)
                offsets[i] = file.ReadUShort();

            for (int i = 0; i < offsets.Length; i++)
            {
                int offset = offsets[i];
                if (offset == 0)
                    break;

                bool invalid = offset < headerSize || offset > file.Length - 4 ||
                    (i > 0 && offset <= offsets[i - 1]);
                if (invalid)
                {
                    // Original portrait tables can retain a stale 41st offset.
                    // GES_08 uses this same value for a real frame, so only ignore
                    // it when invalid and followed exclusively by zero padding.
                    bool legacyTail = headerSize == 256 && i == 40 && offset == 0x460d;
                    for (int j = i + 1; legacyTail && j < offsets.Length; j++)
                        legacyTail = offsets[j] == 0;
                    if (!legacyTail)
                        Log.Warning("Invalid animation frame offset in " + file.Name);
                    break;
                }
                count++;
            }

            header = new ushort[count];
            Array.Copy(offsets, header, count);
        }

        public bool ReadImage(int index)
        {
            if (header == null || index < 0 || index >= header.Length)
            {
                Log.Warning($"Animation frame {index} is out of range in {file?.Name ?? "<unknown>"} ({header?.Length ?? 0} frames)");
                return false;
            }

            if (index + 1 >= header.Length)
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
