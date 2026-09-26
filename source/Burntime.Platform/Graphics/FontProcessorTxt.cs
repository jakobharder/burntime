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
        int glyphHeight;
        Vector2f factor;

        public Vector2 Size { get { return size; } }
        public int Offset { get { return offset; } }
        public int GlyphHeight { get { return glyphHeight; } }
        public int LineHeight { get; private set; }
        public Vector2f Factor { get { return factor; } }
        public bool PostFilter { get; private set; }

        public Dictionary<char, CharInfo> CharInfo { get { return charInfo; } }
        public Dictionary<string, float> Kerning { get { return kerning; } }
        public IReadOnlyDictionary<char, FontSpriteInfo> Indicators => indicators;
        public IReadOnlyDictionary<char, FontSpriteInfo> Icons => icons;

        Dictionary<char, CharInfo> charInfo;
        Dictionary<string, float> kerning = [];
        Dictionary<char, FontSpriteInfo> indicators = [];
        Dictionary<char, FontSpriteInfo> icons = [];

        byte[] image;
        int stride;

        public PixelColor Color { get; set; } = PixelColor.White;
        public PixelColor Shadow { get; set; } = PixelColor.Black;

        public void Process(ResourceID id)
        {
            ConfigFile config = new ConfigFile();
            config.Open(FileSystem.GetFile(id.File));

            int lines = config[""].GetInt("lines");
            float sourceHeight = config[""].GetFloat("height");
            LineHeight = config[""].GetInt("line_height");
            offset = config[""].GetInt("offset");
            Vector2f scale = config[""].GetVector2f("scale", Vector2f.One);
            PostFilter = config[""].GetBool("post_filter", false);
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
            int height = (int)System.Math.Round(sourceHeight * scale.y) * multiplier;
            glyphHeight = height;

            charInfo = new Dictionary<char, CharInfo>();
            kerning = new Dictionary<string, float>();
            indicators = ReadSpriteInfo(config[""], "indicator");
            icons = ReadSpriteInfo(config[""], "icon");

            foreach (var entry in config[""].Values)
                if (entry.Key.StartsWith("kerning") &&
                    float.TryParse(entry.Key[7..], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float amount) && amount > 0)
                    AddKerning(config[""], entry.Key, amount);

            for (int line = 0; line < lines; line++)
            {

                // read character info
                string sequence = config[""].Get("char" + line);
                float[] widths = config[""].GetFloats("width" + line);
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
                    info.width = (int)System.Math.Round(widths[i]);
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
                    info.spritePos = new Vector2(pos, config[""].ContainsKey("atlas_y" + line)
                        ? config[""].GetInt("atlas_y" + line) : line * height);

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
            foreach (char character in new List<char>(charInfo.Keys))
            {
                CharInfo glyph = charInfo[character];
                glyph.imgHeight = System.Math.Max(0, System.Math.Min(glyph.imgHeight, size.y - glyph.spritePos.y));
                charInfo[character] = glyph;
            }
            file.Close();
        }

        static Dictionary<char, FontSpriteInfo> ReadSpriteInfo(
            ConfigSection config, string prefix)
        {
            Dictionary<char, FontSpriteInfo> result = [];
            string image = config.Get(prefix + "_image");
            string codes = config.Get(prefix + "_codes");
            Vector2 size = config.GetVector2(prefix + "_size");
            int[] starts = config.GetInts(prefix + "_starts");
            int[] counts = config.GetInts(prefix + "_counts");
            int[] widths = config.GetInts(prefix + "_widths");
            int count = System.Math.Min(codes?.Length ?? 0,
                System.Math.Min(starts.Length,
                    System.Math.Min(counts.Length, widths.Length)));
            if (string.IsNullOrEmpty(image) || size.x <= 0 || size.y <= 0)
                return result;

            for (int i = 0; i < count; i++)
            {
                if (counts[i] <= 0 || widths[i] < 0)
                    continue;
                result[codes[i]] = new FontSpriteInfo(image, size, starts[i],
                    counts[i], widths[i]);
            }
            return result;
        }

        void AddKerning(ConfigSection section, string key, float amount)
        {
            if (!section.ContainsKey(key))
                return;

            foreach (string pair in section.Get(key).Split((char[]?)null,
                StringSplitOptions.RemoveEmptyEntries))
            {
                if (pair.Length != 2)
                    throw new System.IO.InvalidDataException(
                        $"{key} entries must be two-character pairs.");

                kerning[pair] = -amount;
            }
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
