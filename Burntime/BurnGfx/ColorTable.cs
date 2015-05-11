using System;
using System.Collections.Generic;
using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Platform.Graphics;

namespace Burntime.Data.BurnGfx
{
    public class ColorTable
    {
        PixelColor[] colorTable;

        public static ColorTable Last;
        public static ColorTable Default;

        public const int DefaultBlue = 148;
        public const int DefaultRed = 241;
        public const int ButtonBlue = 140;
        public const int ButtonBlueHighlight = 150;
        public const int HoverGray = 248;
        public const int TextGray = 189;

        public ColorTable()
        {
        }

        public ColorTable(File file)
        {
            file.Seek(16, SeekPosition.Begin);
            Read(file);
        }

        public void Select()
        {
            //Last = this;
        }

        public void FromPac(byte[] file, int offset, short[] moveCmd)
        {
            colorTable = new PixelColor[256];
           // Last = this;

            //int offset = file.Length - 0x305;

            while (file[offset] != 0x22)
            {
                offset++;

            }

            //offset++;

            //byte[] colorbytes = new byte[3];
          //  int read = 3;// = File.Read(colorbytes, 0, 3);

            int active = 0;
            int last = 0;

            colorTable[0] = new PixelColor(0, 0, 0);

            int pos = 0;
            int next = 100;

            bool mode = true;

            int leftcount = 0;
            int run = 0;
            int streampixel = 0;
            int ignore = 0;

            int streamoff = 0;
            int streamdata = 0;
            int streamdatapos = 0;

            for (int i = 0; i < 255; i++)
            {
              //  read = reader.Read(colorbytes, 0, 3);
                //if (read != 3)
                  //  break;


                if (i == 127)
                {
                    mode = true;
                }

                if (mode)
                {
                    int[] color = new int[3];
                    for (int c = 0; c < 3; c++)
                    {
                        //if (offset + pos >= file.Length || offset + pos < 0)
                        //    break;
                        color[c] = active;

                        if (run > 0)
                        {
                            run--;
                        }
                        else if (leftcount > 0)
                        {
                            leftcount--;
                            if (offset + pos >= file.Length || offset + pos < 0)
                                break;
                            color[c] = (file[offset + pos] + color[c]) % 0x40;
                            pos++;
                        }
                        else if (streampixel > 0)
                        {
                            int sd = 0;
                            if (streamoff == 0)
                            {
                                streamoff = 1;

                                sd = file[offset + pos] >> 4;
                            }
                            else
                            {
                                sd = file[offset + pos] & 0xf;
                                pos++;
                                streamoff = 0;
                            }
                            if (sd != 0)
                            {
                                color[c] = (color[c] + moveCmd[sd - 1]) % 0x40;
                                if (color[c] < 0)
                                    color[c] += 0x40;
                            }
                            else
                            {
                                color[c] = (color[c] + file[offset + streamdatapos + streamdata]) % 0x40;
                                streamdata++;
                            }

                            streampixel--;

                            if (streampixel == 0 && ignore == 0)
                            {
                                pos += streamdata;
                            }
                        }
                        else if (ignore >= 1)
                        {
                            ignore--;
                            pos++;
                            pos += streamdata;
                            c--;
                        }
                        else
                        {
                            if (offset + pos >= file.Length || offset + pos < 0)
                                break;
                            int data = file[offset + pos];
                            pos++;
                            if (data >= 0xf0)
                            {
                                run = 0x100 - data - 1;
                            }
                            //else if ((data >> 4) >= 5 && (data >> 4) < 8) // pixel stream
                            //{
                            //    streampixel = (data - 0x51) * 3;
                            //    if (streampixel % 2 == 1)
                            //        ignore = 1;
                            //    streamoff = 0;
                            //    streamdatapos = pos + (streampixel + ignore) / 2;
                            //    streamdata = 0;
                            //    c--;
                            //}
                            else if ((data >> 4) >= 4) // bit stream
                            {
                                streampixel = (data - 0x3F);
                                if (streampixel % 2 == 1)
                                    ignore = 1;
                                streamoff = 0;
                                streamdatapos = pos + (streampixel + ignore) / 2;
                                streamdata = 0;
                                c--;
                            }
                            else if (data == 0)
                            {
                                if (offset + pos >= file.Length || offset + pos < 0)
                                    break;
                                color[c] = (file[offset + pos] + color[c]) % 0x40;
                                pos++;
                            }
                            else if (data == 0x42 && c == 0)
                            {
                                pos++;
                                pos++;
                                c++;
                                c++;
                                color[0] = 2;
                                color[1] = 2;
                                color[2] = 4;
                            }
                            else
                            {
                                leftcount = data;

                                if (offset + pos >= file.Length || offset + pos < 0)
                                    break;
                                color[c] = (file[offset + pos] + color[c]) % 0x40;
                                pos++;
                            }
                        }

                        if (c >= 0)
                            active = color[c];
                    }

                    colorTable[i + 1] = new PixelColor(255, color[0] * 4, color[1] * 4, color[2] * 4);

                    //if (offset + pos >= file.Length || offset + pos < 0)
                    //    break;
                }
                else
                {
                    if (pos == next)
                    {
                        int d = file[offset + pos];
                        next = d + pos + 2;
                        pos++;
                    }
                    if (offset + pos >= file.Length || offset + pos < 0)
                        break;
                    int r = file[offset + pos];
                    pos++;
                    if (r == 0xfe || r % 0x40 == 0x3e)
                        r = 0x40;
                    if (r == 0x42)
                        r = 2;
                    last = r;
                    r = (active + r) % 0x40;// *4;
                    active = r;
                    if (pos == next)
                    {
                        int d = file[offset + pos];
                        next = d + pos + 2;
                        pos++;
                    }
                    if (offset + pos >= file.Length || offset + pos < 0)
                        break;
                    int g = file[offset + pos];
                    pos++;
                    if (/*g == 0xfe ||*/ g == 0x3e)
                    {
                        g = 0x40;
                    }
                    else if (g == 0xfe && pos > 120 * 3)
                    {
                        r *= 4;

                        if (r >= 256)
                        {
                            r = 0;
                        }

                        colorTable[i + 1] = new PixelColor(r, r, r);
                        continue;
                    }
                    if (g == 0x41)
                        g = 0;
                    last = g;
                    g = (active + g) % 0x40;// *4;
                    active = g;
                    if (pos == next)
                    {
                        int d = file[offset + pos];
                        next = d + pos + 2;
                        pos++;
                    }
                    if (offset + pos >= file.Length || offset + pos < 0)
                        break;
                    int b = file[offset + pos];
                    pos++;
                    if (b == 0xfe || b % 0x40 == 0x3e)
                        b = 0x0;
                    if (b == 0x40)
                        b = 0x02;
                    last = b;
                    b = (active + b) % 0x40;// *4;
                    active = b;

                    r *= 4;
                    g *= 4;
                    b *= 4;

                    if (r >= 256 || g >= 256 || b >= 256)
                    {
                        r = 0;
                        g = 0;
                        b = 0;
                    }

                    colorTable[i + 1] = new PixelColor(r, g, b);
                }
            }
        }

        //public void save(string file)
        //{

        //    Bitmap bmp = new Bitmap(256, 1, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        //    for (int i = 0; i < 256; i++)
        //        bmp.SetPixel(i, 0, colorTable[i]);

        //    //bmp.Save(BurnGfxSetting.Singleton.DataPath + "\\..\\output\\" + file + ".png");
        //}

        public bool Read(File reader)
        {
            colorTable = new PixelColor[256];
           // Last = this;

            byte[] colorbytes = new byte[3];
            int read = 3;// = File.Read(colorbytes, 0, 3);
            for (int i = 0; i < 256 && read == 3; i++)
            {
                read = reader.Read(colorbytes, 0, 3);
                if (read != 3)
                    break;

                int r = colorbytes[0] * 4;
                int g = colorbytes[1] * 4;
                int b = colorbytes[2] * 4;

                if (r >= 256 || g >= 256 || b >= 256)
                {
                    r = 0;
                    g = 0;
                    b = 0;
                }

                colorTable[i] = new PixelColor((i == 0) ? 0 : 255, r, g, b);
            }

            return (read == 3);
        }

        public int Find(int r, int g, int b)
        {
            for (int i = 1; i < 256; i++)
            {
                PixelColor c = colorTable[i];
                if (c.r == r && c.g == g && c.b == b)
                    return i;
            }

            return 0;
        }

        public PixelColor GetColor(int index)
        {
            return colorTable[index];
        }

        public byte GetIndex(PixelColor Color)
        {
            return (byte)Find(Color.r, Color.g, Color.b);
        }
    }
}
