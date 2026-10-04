using System.Collections.Generic;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.Resource;

namespace Burntime.Remaster.Tests;

using static Program;

static class ClothingRecolorTests
{
    internal static IEnumerable<Case<int>> Cases()
    {
        yield return Int("character artwork requires explicit opt-in", 0, () =>
        {
            using var resources = new TestResourceManager();
            Burntime.Platform.IO.FileSystem.AddPackage("character-test",
                System.IO.Path.GetDirectoryName(ResourceFile("../classic_newgfx/newgfx.txt"))!);
            resources.SetResourceReplacement("character-test:newgfx.txt");
            try
            {
                CharacterGraphicsOptions.Configure([]);
                foreach (string id in new[] { "80-95", "240-255", "64-79", "256-271" })
                {
                    var body = new ResourceID("burngfxani@syssze.raw?" + id);
                    Equal("gfx/syssze.png", resources.GetReplacement(body)!.Id.File,
                        "default uses existing artwork");
                    CharacterGraphicsOptions.Configure(["--EXPERIMENTAL-CHARACTERS"]);
                    Equal(true, resources.GetReplacement(body)!.Id.File.StartsWith("gfx/char_"),
                        "opt-in uses new artwork");
                    CharacterGraphicsOptions.Configure([]);
                }
                CharacterGraphicsOptions.Configure([CharacterGraphicsOptions.CommandLineFlag]);
                Equal("gfx/syssze.png",
                    resources.GetReplacement(new ResourceID("burngfxani@syssze.raw?176-191"))!.Id.File,
                    "unfinished trader keeps existing artwork even with opt-in");
                CharacterGraphicsOptions.Configure([]);
                Equal(3, Burntime.Remaster.Logic.Generation.PlayerColorSetup.BodyColorSet(
                    Burntime.Remaster.Logic.Generation.BurntimePlayerColor.Black), "gray player hires into gray bodyset");
                Equal((uint?)0xff0806,
                    new ResourceID("burngfxani@syssze.raw?128-143?;recolor=ff0806").RecolorRgb,
                    "recolor remains available without experimental characters");
            }
            finally
            {
                CharacterGraphicsOptions.Configure([]);
                Burntime.Platform.IO.FileSystem.RemovePackage("character-test");
            }
            return 0;
        });
        yield return Int("gray is separate from original bodysets", 0, () =>
        {
            using var resources = new TestResourceManager();
            Burntime.Platform.IO.FileSystem.AddPackage("character-test",
                System.IO.Path.GetDirectoryName(ResourceFile("../classic_newgfx/newgfx.txt"))!);
            resources.SetResourceReplacement("character-test:newgfx.txt");
            try
            {
                CharacterGraphicsOptions.Configure([]);
                for (int body = 0; body < 4; body++)
                {
                    for (int color = 0; color < 3; color++)
                    {
                        var original = new ResourceID(Helper.GetCharacterBodyResource(body, color));
                        var mapped = resources.GetReplacement(original)!.Id;
                        Equal(original.Index, mapped.Index, "original frame index");
                        Equal((uint?)null, mapped.RecolorRgb, "original colors have no recolor");
                    }
                    var gray = resources.GetReplacement(new ResourceID(
                        Helper.GetCharacterBodyResource(body, Helper.GrayBodyColorSet)))!.Id;
                    Equal(Helper.GetBodyId(body, 2), gray.Index, "gray uses green source");
                    Equal((uint?)Helper.GrayClothingRgb, gray.RecolorRgb, "gray recolor");
                    CharacterGraphicsOptions.Configure([CharacterGraphicsOptions.CommandLineFlag]);
                    var experimentalGray = resources.GetReplacement(new ResourceID(
                        Helper.GetCharacterBodyResource(body, Helper.GrayBodyColorSet)))!.Id;
                    Equal((uint?)Helper.GrayClothingRgb, experimentalGray.RecolorRgb,
                        "gray survives experimental replacement");
                    var red = resources.GetReplacement(new ResourceID(
                        Helper.GetCharacterBodyResource(body, 0)))!.Id;
                    Equal("gfx/syssze.png", red.File, "red always uses original artwork");
                    Equal((uint?)null, red.RecolorRgb, "red never recolors");
                    CharacterGraphicsOptions.Configure([]);
                }
            }
            finally
            {
                CharacterGraphicsOptions.Configure([]);
                Burntime.Platform.IO.FileSystem.RemovePackage("character-test");
            }
            return 0;
        });
        yield return Int("saved player icons correct legacy bodyset assignments", 0, () =>
        {
            var blue = new Burntime.Remaster.Logic.Player { IconID = 3, BodyColorSet = -1 };
            var gray = new Burntime.Remaster.Logic.Player { IconID = 2, BodyColorSet = 1 };
            Equal(1, blue.CharacterBodyColorSet, "saved blue player uses blue sprites");
            Equal(Helper.GrayBodyColorSet, gray.CharacterBodyColorSet,
                "saved gray player hires gray recruits");
            return 0;
        });
        yield return Int("neutral gray recolor preserves clothing contrast", 0, () =>
        {
            byte[] pixels = [0, 60, 0, 255, 0, 132, 0, 128, 0, 208, 0, 255,
                220, 180, 150, 255, 255, 255, 255, 255];
            GreenClothingRecolor.Apply(pixels, 0xd4d4d4);
            Equal("50,50,50,255,110,110,110,128,173,173,173,255,220,180,150,255,255,255,255,255",
                string.Join(',', pixels), "neutral gray keeps shadows, highlights, skin and white trim");
            return 0;
        });
        yield return Int("recolor retains non-clothing pixels and alpha", 0, () =>
        {
            byte[] pixels = [7, 227, 5, 255, 7, 87, 6, 128,
                220, 180, 150, 255, 255, 255, 255, 255, 7, 227, 5, 0,
                100, 130, 0, 255];
            GreenClothingRecolor.Apply(pixels, 0x8ca6c7);
            Equal("125,148,177,255,48,57,68,128,220,180,150,255,255,255,255,255,7,227,5,0,100,130,0,255",
                string.Join(',', pixels), "only strongly green visible pixels change");
            return 0;
        });
        yield return Int("resource recolor options preserve dimensions and cache identity", 0, () =>
        {
            var blue = new ResourceID("pngsheet@gfx/char_merc.png?0-39?36x42;recolor=8ca6c7");
            var red = new ResourceID("pngsheet@gfx/char_merc.png?0-39?36x42;recolor=ff0806");
            Equal("36x42", blue.Custom, "processor still receives frame dimensions");
            Equal((uint?)0x8ca6c7, blue.RecolorRgb, "RGB target is parsed");
            Equal(false, blue.ToString() == red.ToString(), "variants have different cache keys");
            Equal((uint?)null, new ResourceID("burngfxani@syssze.raw?32-47").RecolorRgb,
                "classic sprites remain unchanged by default");
            Equal((uint?)0xff0806,
                new ResourceID("burngfxani@syssze.raw?128-143?;recolor=ff0806").RecolorRgb,
                "burngfx can opt in later through the same option");
            Equal((uint?)null,
                new ResourceID("pngsheet@gfx/char_merc.png?0?36x42;recolor=bad").RecolorRgb,
                "invalid recolor option is ignored");
            return 0;
        });
    }
    sealed class TestResourceManager : ResourceManagerBase, System.IDisposable
    {
        public TestResourceManager() : base(new TestLoadingCounter()) { }
        public override Font? GetFont(string file, PixelColor color) => null;
        public override Font? GetFont(string file, PixelColor color, PixelColor backColor) => null;
        public override ISprite GetImage(ResourceID id,
            ResourceLoadType loadType = ResourceLoadType.Delayed) => null!;
        public override void Reload(ISprite sprite,
            ResourceLoadType loadType = ResourceLoadType.Delayed) { }
    }

    sealed class TestLoadingCounter : ILoadingCounter
    {
        public void IncreaseLoadingCount() { }
        public void DecreaseLoadingCount() { }
    }
}
