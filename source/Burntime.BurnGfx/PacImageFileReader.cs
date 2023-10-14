using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Platform.Graphics;

namespace Burntime.Data.BurnGfx
{
    public class PacImageFileReader
    {
        protected byte[] header;

        //protected Bitmap bmp;
        protected PixelColor[] bytes;
        protected IntPtr ptr;

        protected byte[] memFile;
        protected int colorTableOffset;
        protected bool hasColorTable;

        protected short[] moveCmd;

        int width;
        int height;

        public int Width
        {
            get { return width; }
        }

        public int Height
        {
            get { return height; }
        }

        public PixelColor[] Data
        {
            get { return bytes; }
        }

        string id;
        public string ID
        {
            get { return id; }
        }

        ColorTable table;
        public ColorTable ColorTable
        {
            get { return table; }
        }

        static public PacImageFileReader Read(String filePath)
        {
            PacImageFileReader g = new PacImageFileReader();

            g.id = filePath;
            g.width = 320;
            g.height = 200;
            File file = FileSystem.GetFile(filePath);
            g.memFile = file.ReadAllBytes();

            g.moveCmd = new short[15];
            for (int i = 0; i < 15; i++)
            {
                g.moveCmd[i] = g.memFile[i];
                if (g.moveCmd[i] >= 0x80)
                    g.moveCmd[i] = (short)-(0x100 - g.moveCmd[i]);
            }


            // calc color table offset
            //PixelColor[] data = new PixelColor[3];
            //ByteBuffer b = new ByteBuffer(1, 1, data);
            //g.Draw(ref b);

            // TODO: optimize loading sequence

            g.bytes = new PixelColor[g.width * g.height];
            ByteBuffer buffer = new ByteBuffer(g.width, g.height, g.bytes);
            g.Draw(ref buffer);

            if (g.hasColorTable)
            {

                g.table = new ColorTable();
                g.table.FromPac(g.memFile, g.colorTableOffset, g.moveCmd);
                BurnGfxData.Instance.AddPacColorTable(filePath, g.table);

            //    g.table.save(filePath);
            }

            g.Draw(ref buffer);

            return g;
        }

        //static public Background Read(Reader reader, ColorTable colors, int width, int height)
        //{
        //    Background g = new Background();
        //    g.ReadData(reader, colors, width, height);
        //    return g;
        //}

        //protected void ReadData(Reader reader, ColorTable colors, int width, int height)
        //{
        //    bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        //    bytes = new byte[width * height * 3];

        //    header = new byte[15];
        //    reader.Read(header, 0, 15);

        //    int colorindex = 0;
        //    int pos = 0;

        //    BitmapData img = bmp.LockBits(PixelRect, System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        //    ptr = img.Scan0;

        //    while (!reader.EOF && pos < width * height )
        //    {
        //        byte data = reader.ReadByte();

        //        int cmd = (data & 0xC0) >> 6;
        //        switch (cmd)
        //        {
        //            case 0:
        //                ReadSingleStream(reader, ref pos, (data & 0x3f) + 1, ref colorindex, colors);
        //                break;
        //            case 1:
        //                ReadBitStream(reader, ref pos, (data & 0x3f) + 1, ref colorindex, colors);
        //                break;
        //            case 2:
        //                break;
        //            case 3:

        //                int length = 0x3F - (data & 0x3F) + 1;
        //                SetLine(pos, length, colors.GetColor(colorindex));
        //                pos += length;

        //                break;
        //            default:
        //                break;
        //        }
        //    }

        //    //System.Runtime.InteropServices.Marshal.Copy(bytes, 0, img.Scan0, width * height * 3);
        //    bmp.UnlockBits(img);
        //}

        public void Draw(ref ByteBuffer Target)
        {
            int dataindex = 0;
            header = new byte[15];
            for (int i = 0; i < 15; i++)
            {
                header[i] = memFile[dataindex];
                dataindex++;
            }
            //reader.Read(header, 0, 15);

            if (!hasColorTable)
                table = BurnGfxData.Instance.DefaultColorTable;

            int colorindex = 0;
            int pos = 0;

            while (pos < width * height)
            {
                byte data = memFile[dataindex];
                dataindex++;

                int cmd = (data & 0xC0) >> 6;
                switch (cmd)
                {
                    case 0:
                        ReadSingleStream(ref Target, ref dataindex, ref pos, (data & 0x3f) + 1, ref colorindex);
                        break;
                    case 1:
                        ReadBitStream(ref Target, ref dataindex, ref pos, (data & 0x3f) + 1, ref colorindex);
                        break;
                    case 2:
                        break;
                    case 3:

                        int length = 0x3F - (data & 0x3F) + 1;
                        if (table != null)
                            SetLine(ref Target, pos, length, table.GetColor(colorindex));
                        pos += length;

                        break;
                    default:
                        break;
                }
            }

            colorTableOffset = dataindex;
            hasColorTable = !(colorTableOffset == memFile.Length);
        }
        protected void ReadSingleStream(ref ByteBuffer Target, ref int dataindex, ref int pos, int length, ref int colorindex)
        {
            for (int i = 0; i < length; i++)
                ReadSingle(ref Target, ref dataindex, ref pos, ref colorindex);
        }

        protected void ReadSingle(ref ByteBuffer Target, ref int dataindex, ref int pos, ref int colorindex)
        {
            byte single = memFile[dataindex];
            dataindex++;
            colorindex += single;
            if (colorindex > 255)
                colorindex -= 256;

            if (table != null)
                SetPixel(ref Target, pos, table.GetColor(colorindex));
            pos++;
        }

        protected void ReadBitStream(ref ByteBuffer Target, ref int dataindex, ref int pos, int length, ref int colorindex)
        {
            int readlength = (length + length % 2) / 2;

            byte[] stream = new byte[readlength];
            for (int i = 0; i < readlength; i++)
            {
                stream[i] = memFile[dataindex];
                dataindex++;
            }

            int fullbytes = 0;
            for (int i = 0; i < length; i++)
            {
                if (i % 2 == 0)
                {
                    if ((stream[i / 2] >> 4) == 0)
                        fullbytes++;
                }
                else
                {
                    if ((stream[(i - 1) / 2] & 0xf) == 0)
                        fullbytes++;
                }
            }

            byte[] bytes = new byte[fullbytes];
            for (int i = 0; i < fullbytes; i++)
            {
                bytes[i] = memFile[dataindex];
                dataindex++;
            }

            int byteindex = 0;

            for (int i = 0; i < length; i++)
            {
                int data;
                if (i % 2 == 0)
                {
                    data = stream[i / 2] >> 4;
                }
                else
                {
                    data = stream[(i - 1) / 2] & 0xf;
                }

                int move;
                if (data == 0)
                {
                    move = bytes[byteindex];
                    byteindex++;
                }
                else
                {
                    move = header[data - 1];
                }

                colorindex += move;
                if (colorindex < 0)
                    colorindex += 256;
                if (colorindex > 255)
                    colorindex -= 256;
                
                if (table != null)
                    SetPixel(ref Target, pos, table.GetColor(colorindex));

                pos++;
            }
        }

        protected void SetLine(ref ByteBuffer Target, int pos, int length, PixelColor color)
        {
            for (int i = pos; i < pos + length; i++)
                SetPixel(ref Target, i, color);
        }

        protected void SetPixel(ref ByteBuffer Target, int pos, PixelColor color)
        {
            if (pos >= width * height)
                return;

            int x = pos % width;
            int y = (pos - x) / width;

            if (Target.Offset + x + Target.Stride * y >= Target.Stride * Target.Height)
                return;

            Target.Data[Target.Offset + x + Target.Stride * y].b = color.b;
            Target.Data[Target.Offset + x + Target.Stride * y].g = color.g;
            Target.Data[Target.Offset + x + Target.Stride * y].r = color.r;
            Target.Data[Target.Offset + x + Target.Stride * y].a = 255;
            //bytes[pos * 3] = color.B;
            //bytes[pos * 3 + 1] = color.G;
            //bytes[pos * 3 + 2] = color.R;
        }
        //protected void ReadSingleStream(Reader reader, ref int pos, int length, ref int colorindex, ColorTable colors)
        //{
        //    for (int i = 0; i < length; i++)
        //        ReadSingle(reader, ref pos, ref colorindex, colors);
        //}

        //protected void ReadSingle(Reader reader, ref int pos, ref int colorindex, ColorTable colors)
        //{
        //    byte single = reader.ReadByte();
        //    colorindex += single;
        //    if (colorindex > 255)
        //        colorindex -= 256;

        //    SetPixel(pos, colors.GetColor(colorindex));
        //    pos++;
        //}

        //protected void ReadBitStream(Reader reader, ref int pos, int length, ref int colorindex, ColorTable colors)
        //{
        //    int readlength = (length + length % 2) / 2;

        //    byte[] stream = new byte[readlength];
        //    reader.Read(stream, 0, readlength);

        //    int fullbytes = 0;
        //    for (int i = 0; i < length; i++)
        //    {
        //        if (i % 2 == 0)
        //        {
        //            if ((stream[i / 2] >> 4) == 0)
        //                fullbytes++;
        //        }
        //        else
        //        {
        //            if ((stream[(i - 1) / 2] & 0xf) == 0)
        //                fullbytes++;
        //        }
        //    }

        //    byte[] bytes = new byte[fullbytes];
        //    reader.Read(bytes, 0, fullbytes);
        //    int byteindex = 0;

        //    for (int i = 0; i < length; i++)
        //    {
        //        int data;
        //        if (i % 2 == 0)
        //        {
        //            data = stream[i / 2] >> 4;
        //        }
        //        else
        //        {
        //            data = stream[(i - 1) / 2] & 0xf;
        //        }

        //        int move;
        //        if (data == 0)
        //        {
        //            move = bytes[byteindex];
        //            byteindex++;
        //        }
        //        else
        //        {
        //            move = header[data - 1];
        //        }

        //        colorindex += move;
        //        if (colorindex < 0)
        //            colorindex += 256;
        //        if (colorindex > 255)
        //            colorindex -= 256;

        //        SetPixel(pos, colors.GetColor(colorindex));

        //        pos++;
        //    }
        //}

        //protected void SetLine(int pos, int length, Color color)
        //{
        //    for (int i = pos; i < pos + length; i++)
        //        SetPixel(i, color);
        //}

        //protected void SetPixel(int pos, Color color)
        //{
        //    if (pos >= bmp.Width * bmp.Height)
        //        return;

        //    //bytes[pos * 3] = color.B;
        //    //bytes[pos * 3 + 1] = color.G;
        //    //bytes[pos * 3 + 2] = color.R;
        //    unsafe
        //    {
        //        byte* b = (byte*)ptr;
        //        b[pos * 3] = color.B;
        //        b[pos * 3 + 1] = color.G;
        //        b[pos * 3 + 2] = color.R;
        //    }
        //}
    }
}
