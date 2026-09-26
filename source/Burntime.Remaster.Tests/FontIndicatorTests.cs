using System.Collections.Generic;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.Resource;

namespace Burntime.Remaster.Tests;

using static Program;

static class FontIndicatorTests
{
    internal static IEnumerable<Case<int>> IndicatorCases()
    {
        yield return Int("small font atlas crops fit with three-quarter-size advances", 1, () =>
        {
            FontProcessorTxt regular = new();
            regular.Process("font-highres.txt");
            FontProcessorTxt small = new();
            Burntime.Platform.IO.FileSystem.AddPackage("small-font-test",
                System.IO.Path.GetDirectoryName(ResourceFile("../classic_newgfx/font-small-highres.txt"))!);
            try
            {
                small.Process("font-small-highres.txt");
            }
            finally
            {
                Burntime.Platform.IO.FileSystem.RemovePackage("small-font-test");
            }
            foreach (var (letter, glyph) in regular.CharInfo)
            {
                CharInfo reduced = small.CharInfo[letter];
                if (reduced.spritePos.x < 0 || reduced.spritePos.y < 0 ||
                    reduced.spritePos.x + reduced.imgWidth > small.Size.x ||
                    reduced.spritePos.y + reduced.imgHeight > small.Size.y ||
                    System.Math.Abs(glyph.renderWidth * 0.75f - reduced.renderWidth) > small.Factor.x + 0.001f)
                    return 0;
            }
            return small.LineHeight == 9 && small.Factor == regular.Factor / 2 ? 1 : 0;
        });

        yield return Int("font config defines dot maximum", 5, () =>
        {
            FontProcessorTxt processor = new();
            processor.Process("font.txt");
            return processor.Indicators['d'].FrameCount;
        });

        yield return Int("font config defines camp NPC indicator width", 3, () =>
        {
            FontProcessorTxt processor = new();
            processor.Process("font.txt");
            return processor.Indicators['n'].Advance;
        });

        yield return Int("font config defines camp NPC maximum", 5, () =>
        {
            FontProcessorTxt processor = new();
            processor.Process("font.txt");
            return processor.Indicators['n'].FrameCount;
        });

        yield return Int("font config maps radiation icon", 1, () =>
        {
            FontProcessorTxt processor = new();
            processor.Process("font.txt");
            return processor.Icons['☢'].StartFrame;
        });

        yield return Int("font variants retain sprite glyphs", 3, () =>
        {
            int loaded = 0;
            foreach (string file in new[] { "font.txt", "font-xbr2.txt", "font-highres.txt" })
            {
                FontProcessorTxt processor = new();
                processor.Process(file);
                if (processor.Indicators.ContainsKey('d') &&
                    processor.Indicators.ContainsKey('n') && processor.Icons.ContainsKey('☢'))
                    loaded++;
            }
            return loaded;
        });

        yield return Int("dot overflow selects the last frame", 4, () =>
        {
            TestSprite[] frames =
            [
                new(0), new(1), new(2), new(3), new(4)
            ];
            FontSpriteResource indicator = new(frames, 2);
            return ((TestSprite)indicator.GetFrame(5)).Index;
        });

        yield return Int("indicator markup uses its configured width", 2, () =>
        {
            TestSprite fontSprite = new(0);
            TestSprite[] frames =
            [
                new(0), new(1), new(2), new(3), new(4)
            ];
            FontSpriteResource indicator = new(frames, 2);
            FontResource resource = new();
            resource.Load(fontSprite,
                new Dictionary<char, CharInfo>(), new Dictionary<string, float>(),
                new Dictionary<char, FontSpriteResource> { ['d'] = indicator },
                new Dictionary<char, FontSpriteResource>(),
                0, 12, false);
            Font font = new(null!) { Resource = resource };
            return font.GetWidth("~d5");
        });

        yield return Int("icon glyph uses its configured width", 13, () =>
        {
            TestSprite sprite = new(0);
            FontSpriteResource icon = new([sprite], 13);
            FontResource resource = new();
            resource.Load(sprite,
                new Dictionary<char, CharInfo>(), new Dictionary<string, float>(),
                new Dictionary<char, FontSpriteResource>(),
                new Dictionary<char, FontSpriteResource> { ['☢'] = icon },
                0, 12, false);
            Font font = new(null!) { Resource = resource };
            return font.GetWidth("☢");
        });
    }

    sealed class TestSprite : ISprite
    {
        public int Index { get; }
        public override ResourceID ID => "test.png";
        public override Vector2f Resolution { get; set; } = Vector2f.One;
        public override Vector2 Size => Vector2.One;
        public override SpriteAnimation Animation { get; set; } = null!;
        public override bool IsLoaded => true;
        public override bool HasSystemCopy => true;

        public TestSprite(int index) => Index = index;

        public override bool Touch() => true;
        public override void Update(float elapsed) { }
        public override int Unload() => 0;
        public override ISprite Clone() => this;
    }
}
