using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using Burntime.Platform.Resource;
using Burntime.Platform.IO;

namespace Burntime.Platform.Graphics
{
    public class FontProcessorTxt : IFontProcessor
    {
        Vector2 size;
        int offset;
        float factor;

        public Vector2 Size { get { return size; } }
        public int Offset { get { return offset; } }
        public float Factor { get { return factor; } }

        public Dictionary<char, CharInfo> CharInfo { get { return charInfo; } }

        Dictionary<char, CharInfo> charInfo;

        byte[] image;
        int stride;

        public PixelColor Color { get; set; } = PixelColor.White;
        public PixelColor Shadow { get; set; } = PixelColor.Black;

        public void Process(ResourceID id)
        {
            ConfigFile config = new ConfigFile();
            config.Open(FileSystem.GetFile(id.File));

            int lines = config[""].GetInt("lines");
            int height = config[""].GetInt("height");
            offset = config[""].GetInt("offset");
            factor = config[""].GetFloat("factor");
            if (factor == 0)
                factor = 1;

            height = (int)(height / factor);

            charInfo = new Dictionary<char, CharInfo>();

            for (int line = 0; line < lines; line++)
            {

                // read character info
                string sequence = config[""].Get("char" + line);
                int[] widths = config[""].GetInts("width" + line);
                char[] chars = sequence.ToCharArray();

                int pos = 0;
                for (int i = 0; i < sequence.Length; i++)
                {
                    int width = widths[i];// (int)(widths[i] / factor);

                    CharInfo info = new CharInfo();
                    info.pos = pos;
                    info.width = width;
                    info.imgHeight = height;
                    info.imgWidth = (int)(widths[i] / factor);
                    info.spritePos = new Vector2(pos, line * height);

                    if (charInfo.ContainsKey(chars[i]))
                        charInfo.Remove(chars[i]);

                    charInfo.Add(chars[i], info);

                    pos += (int)(widths[i] / factor);
                }
            }

            // read png image
            File file = FileSystem.GetFile(config[""].Get("image"));
            Bitmap bmp = new Bitmap(file.Stream);
            size = new Vector2(bmp.Width, bmp.Height);

            BitmapData data = bmp.LockBits(new Rectangle(new Point(0, 0), bmp.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            image = new byte[data.Stride * bmp.Height];
            stride = data.Stride;
            Marshal.Copy(data.Scan0, image, 0, image.Length);

            bmp.UnlockBits(data);

            bmp.Dispose();
            file.Close();
        }

        public void Render(System.IO.Stream stream, int stride)
        {
            ByteBuffer buffer = new ByteBuffer(Size.x, Size.y, new PixelColor[Size.x * Size.y]);

            foreach (char c in charInfo.Keys)
                DrawText(buffer, 0, 0, "" + c, false, Color, Shadow);

            buffer.Write(stream, stride);
        }

        void DrawText(ByteBuffer input, int x, int y, String str, bool center, PixelColor fore, PixelColor back)
        {
            if (str == null || str.Length == 0)
                return;

            char[] charray = str.ToCharArray();
            foreach (char ch in charray)
            {
                CharInfo info = charInfo[translateChar(ch)];
                x += DrawChar(input, ch, x + info.spritePos.x, y + info.spritePos.y, fore, back);
            }
        }

        int DrawChar(ByteBuffer input, char ch, int offsetx, int offsety, PixelColor fore, PixelColor back)
        {
            CharInfo info = charInfo[translateChar(ch)];

            int w = info.imgWidth;
            int h = info.imgHeight;
            int p = info.pos;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int pos = (offsetx + x) * 4 + ((y + offsety) * stride);

                    if (image[pos + 3] != 0)
                    {
                        PixelColor c;
                        if (back != PixelColor.Black)
                            c = MixColor(fore, back, image[pos + 3], image[pos]);
                        else
                            c = MixColor(PixelColor.White, PixelColor.Black, image[pos + 3], image[pos]);
                        //if (back != PixelColor.Black)
                        //    c = (image[pos] == 0) ? back : fore;
                        //else
                        //    c = (image[pos] == 0) ? PixelColor.Black : PixelColor.White;

                        input.DrawPixel(x + offsetx, y + offsety, c.a, c.r, c.g, c.b);
                    }
                }
            }

            return info.width;
        }

        PixelColor MixColor(PixelColor fore, PixelColor back, byte a, byte r)
        {
            float factor = r / 255.0f;

            PixelColor c = new PixelColor();
            int _r = (int)((fore.r * factor) + (back.r * (factor - 1)));
            int _g = (int)((fore.g * factor) + (back.g * (factor - 1)));
            int _b = (int)((fore.b * factor) + (back.b * (factor - 1)));
            c.r = (byte)System.Math.Min(System.Math.Max(0, _r), 255);
            c.g = (byte)System.Math.Min(System.Math.Max(0, _g), 255);
            c.b = (byte)System.Math.Min(System.Math.Max(0, _b), 255);
            c.a = a;

            return c;
        }

        char translateChar(char ch)
        {
            if (charInfo.ContainsKey(ch))
                return ch;
            else
                return '?';
        }
    }
}
