using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform.Resource;
using Burntime.Platform.IO;

namespace Burntime.Platform.Graphics
{
    public class FontProcessorTxt : IFontProcessor
    {
        Vector2 size;
        int offset;
        Vector2f factor;

        public Vector2 Size { get { return size; } }
        public int Offset { get { return offset; } }
        public Vector2f Factor { get { return factor; } }

        public Dictionary<char, CharInfo> CharInfo { get { return charInfo; } }
        public Dictionary<string, int> Kerning { get { return kerning; } }

        Dictionary<char, CharInfo> charInfo;
        Dictionary<string, int> kerning = [];

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
            Vector2f scale = config[""].GetVector2f("scale", Vector2f.One);
            // Keep existing one-dimensional font descriptors working.
            if (scale.y == 0)
                scale.y = scale.x;
            if (scale.x == 0 || scale.y == 0)
                scale = Vector2f.One;
            int multiplier = config[""].GetInt("multiplier");
            if (multiplier <= 0)
                multiplier = 1;

            Vector2f effectiveScale = scale * multiplier;
            factor = Vector2f.One / effectiveScale;

            // Round at the base export scale first. Higher-resolution atlases are
            // exact integer multiples of that rasterization.
            height = (int)System.Math.Round(height * scale.y) * multiplier;

            charInfo = new Dictionary<char, CharInfo>();
            kerning = new Dictionary<string, int>();

            for (int amount = 1; config[""].ContainsKey("kerning" + amount); amount++)
            {
                string key = "kerning" + amount;
                foreach (string pair in config[""].Get(key).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (pair.Length != 2)
                        throw new System.IO.InvalidDataException($"{key} entries must be two-character pairs.");

                    kerning[pair] = -amount;
                }
            }

            for (int line = 0; line < lines; line++)
            {

                // read character info
                string sequence = config[""].Get("char" + line);
                int[] widths = config[""].GetInts("width" + line);
                float[] renderWidths = config[""].GetFloats("renderwidth" + line);
                char[] chars = sequence.ToCharArray();

                if (renderWidths.Length != 0 && renderWidths.Length != widths.Length)
                    throw new System.IO.InvalidDataException($"renderwidth{line} must contain one value per character.");

                float sourcePos = 0;
                for (int i = 0; i < sequence.Length; i++)
                {
                    int pos = (int)System.Math.Round(sourcePos * scale.x) * multiplier;
                    sourcePos += widths[i];
                    int end = (int)System.Math.Round(sourcePos * scale.x) * multiplier;

                    CharInfo info = new CharInfo();
                    info.pos = pos;
                    info.width = widths[i];
                    info.renderWidth = renderWidths.Length == 0
                        ? widths[i]
                        : renderWidths[i] * 2 / scale.x;
                    info.imgHeight = height;
                    // A render width describes the tight glyph advance. Its source rectangle
                    // must end at that same edge so padded atlas cells cannot overlap the
                    // following glyph during rasterization.
                    info.imgWidth = renderWidths.Length == 0
                        ? end - pos
                        : (int)System.Math.Round(info.renderWidth / factor.x);
                    info.spritePos = new Vector2(pos, line * height);

                    if (charInfo.ContainsKey(chars[i]))
                        charInfo.Remove(chars[i]);

                    charInfo.Add(chars[i], info);

                }
            }

            // read png image
            IO.File file = FileSystem.GetFile(config[""].Get("image"));
            DecodedImage decoded = ImageLoader.LoadBgra(file.Stream);
            size = new Vector2(decoded.Width, decoded.Height);
            image = decoded.BgraData;
            stride = decoded.Width * 4;
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
                        if (fore == PixelColor.Transparent)
                            c = new PixelColor(image[pos + 3], image[pos + 2], image[pos + 1], image[pos]);
                        else if (back != PixelColor.Black)
                            c = MixColor(fore, back, image[pos + 3], image[pos]);
                        else
                            c = MixColor(PixelColor.White, PixelColor.Black, image[pos + 3], image[pos]);

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
