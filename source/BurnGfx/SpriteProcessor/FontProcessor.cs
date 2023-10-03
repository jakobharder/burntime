using System;
using System.Collections.Generic;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.Resource;

namespace Burntime.Data.BurnGfx
{
    class FontProcessor : IFontProcessor
    {
        public Vector2 Size { get { return new Vector2(512, 10); } }
        public int Offset { get { return 0; } }
        public float Factor { get { return 1; } }

        public Dictionary<char, CharInfo> CharInfo { get { return charInfo; } }

        CharInfo[] chars;
        Burntime.Platform.IO.File reader;
        Dictionary<char, CharInfo> charInfo;

        public PixelColor Color { get; set; } = PixelColor.White;
        public PixelColor Shadow { get; set; } = PixelColor.Black;

        public void Process(ResourceID id)
        {
            reader = Burntime.Platform.IO.FileSystem.GetFile(id.File).GetSubFile(0, -1);

            charInfo = new Dictionary<char, CharInfo>();

            chars = new CharInfo[98];
            for (int i = 0; i < 98; i++)
            {
                chars[i].pos = reader.ReadUShort();
                chars[i].width = reader.ReadUShort();

                charInfo.Add((char)(' ' + i), chars[i]);
            }

            for (int i = 0; i < 98; i++)
            {
                reader.Seek(chars[i].pos, Burntime.Platform.IO.SeekPosition.Begin);

                int unknown1 = reader.ReadUShort();
                int unknown2 = reader.ReadUShort();

                chars[i].imgWidth = 4 * (unknown1 + 1);
                chars[i].imgHeight = unknown2;
            }

        }

        public void Render(System.IO.Stream stream, int stride)
        {
            ByteBuffer buffer = new ByteBuffer(Size.x, Size.y, new PixelColor[Size.x * Size.y]);

            int pos = 0;
            for (char c = ' '; c <= 'z'; c++)
            {
                chars[c - ' '].spritePos = new Vector2(pos, 0);
                DrawText(buffer, pos, 0, "" + c, false, Color, Shadow);
                pos += chars[c - ' '].width;
            }

            char[] german = new char[] { 'ü', 'ä', 'Ä', 'ö', 'Ö', 'Ü', 'ß' };
            foreach (char c in german)
            {
                chars[translateChar(c)].spritePos = new Vector2(pos, 0);
                DrawText(buffer, pos, 0, "" + c, false, Color, Shadow);
                pos += chars[translateChar(c)].width;
            }

            buffer.Write(stream, stride);
        }

        void DrawText(ByteBuffer input, int x, int y, String str, bool center, PixelColor fore, PixelColor back)
        {
            if (str == null || str.Length == 0)
                return;

            char[] charray = str.ToCharArray();
            foreach (char ch in charray)
            {
                x += DrawChar(input, ch, x, y, fore, back);
            }
        }

        int DrawChar(ByteBuffer input, char ch, int offsetx, int offsety, PixelColor fore, PixelColor back)
        {
            CharInfo info = chars[translateChar(ch)];
            reader.Seek(info.pos, Burntime.Platform.IO.SeekPosition.Begin);

            int w = 1;
            int h = 8;
            int round = 0;
            int pos = 0;

            int unknown1 = reader.ReadUShort();
            int unknown2 = reader.ReadUShort();

            w = 4 * (unknown1 + 1);
            h = unknown2;

            while (!reader.IsEOF && round != 4)
            {
                int data = reader.ReadByte();

                PixelColor c;
                if (back != PixelColor.Black)
                    c = (data == 0x01) ? back : fore;
                else
                    c = (data == 0x01) ? PixelColor.Black : PixelColor.White;

                int x = pos % w;
                int y = (pos - x) / w;

                if (data != 0)
                {
                    input.DrawPixel(x + offsetx, y + offsety, c.r, c.g, c.b);
                }

                pos += 4;
                if (pos >= w * h)
                {
                    pos -= w * h;
                    pos++;
                    round++;
                }
            }

            return info.width;
        }

        int translateChar(char ch)
        {
            if (ch < ' ')
                return '?' - ' ';
            if (ch <= 'z')
                return ch - ' ';

            switch (ch)
            {
                case 'ü': return 91;
                case 'ä': return 92;
                case 'Ä': return 93;
                case 'ö': return 94;
                case 'Ö': return 95;
                case 'Ü': return 96;
                case 'ß': return 97;
            }

            return '?' - ' ';
        }

        char translateCharBack(int ch)
        {
            if (ch >= 91)
            {
                switch (ch)
                {
                    case 91: return 'ü';
                    case 92: return 'ä';
                    case 93: return 'Ä';
                    case 94: return 'ö';
                    case 95: return 'Ö';
                    case 96: return 'Ü';
                    case 97: return 'ß';
                }

                return '?';
            }
            else
                return (char)(' ' + ch);
        }
    }
}
