using System.Collections.Generic;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.Utils;

namespace Burntime.Remaster.Tests;

using static Program;

static class ResolutionTests
{
    internal static IEnumerable<Case<int>> ResolutionCases()
    {
        yield return Int("720p and 1440p retain the same logical viewport", 0, () =>
        {
            Resolution resolution720 = CreateResolution(new Vector2(1280, 720));
            Resolution resolution1440 = CreateResolution(new Vector2(2560, 1440));

            Equal(1.5f, resolution720.OutputScale, "720p output scale");
            Equal(3f, resolution1440.OutputScale, "1440p output scale");
            Equal(new Vector2(455, 213), resolution720.Game, "720p game resolution");
            Equal(resolution720.Game, resolution1440.Game,
                "proportional outputs use the same game resolution");
            return 0;
        });

        yield return Int("800p naturally selects the Steam Deck scale", 0, () =>
        {
            Resolution resolution = CreateResolution(new Vector2(1280, 800));
            Equal(1.5f, resolution.OutputScale, "800p output scale");
            Equal(new Vector2(853, 533), resolution.BackBuffer, "800p internal target");
            Equal(new Vector2(455, 237), resolution.Game, "800p game resolution");
            return 0;
        });

        yield return Int("iPhone 12 mini uses a fixed 160-unit viewport", 0, () =>
        {
            Resolution resolution = CreateResolution(new Vector2(2436, 1125));
            resolution.FixedGameHeight = 160;
            Equal(new Vector2(416, 160), resolution.Game, "phone game resolution");
            Equal(new Vector2(780, 360), resolution.BackBuffer, "phone aspect-correct target");
            Equal(true, MathF.Abs(resolution.OutputScale - 1125f / 360f) < .00001f,
                "fractional scale is not rounded to a half step");
            resolution.Native = new Vector2(2340, 1080);
            Equal(new Vector2(416, 160), resolution.Game, "native panel keeps the same layout");
            return 0;
        });

        yield return Int("fixed height survives resize and changes in pixel density", 0, () =>
        {
            Resolution resolution = CreateResolution(new Vector2(1280, 720));
            resolution.FixedGameHeight = 160;
            Equal(new Vector2(341, 160), resolution.Game, "16:9 viewport");
            resolution.Native = new Vector2(2560, 1440);
            Equal(new Vector2(341, 160), resolution.Game, "same layout at double density");
            resolution.Native = new Vector2(320, 180);
            Equal(new Vector2(341, 160), resolution.Game, "fixed height also supports downscaling");
            Equal(true, resolution.OutputScale < 1, "fixed mode does not clamp scaling to one");
            resolution.RatioCorrection = Vector2f.One;
            Equal(new Vector2(284, 160), resolution.Game, "width follows aspect correction");
            return 0;
        });

        yield return Int("iPad retains automatic scaling", 0, () =>
        {
            Resolution resolution = CreateResolution(new Vector2(2266, 1488));
            Equal(null, resolution.FixedGameHeight, "no small-screen policy by default");
            Equal(3f, resolution.OutputScale, "iPad output scale");
            Equal(new Vector2(403, 220), resolution.Game, "iPad logical viewport");
            return 0;
        });

        yield return Int("clearing fixed height restores automatic scaling", 0, () =>
        {
            Resolution resolution = CreateResolution(new Vector2(2436, 1125));
            Vector2 original = resolution.Game;
            resolution.FixedGameHeight = 160;
            resolution.FixedGameHeight = null;
            Equal(original, resolution.Game, "clearing policy restores the old viewport");
            resolution.FixedGameHeight = 0;
            Equal(original, resolution.Game, "invalid height safely uses automatic scaling");
            return 0;
        });

        foreach (bool smallScreen in new[] { false, true })
        foreach (bool newGfx in new[] { false, true })
            yield return Int($"filter policy, small screen={smallScreen}, remastered graphics={newGfx}", 0, () =>
            {
                var app = (BurntimeClassic)RuntimeHelpers.GetUninitializedObject(typeof(BurntimeClassic));
                app.Engine = DispatchProxy.Create<IEngine, FilterCapabilities>();
                app.SmallDeviceUi = smallScreen;
                var filter = (OutputFiltering)typeof(BurntimeClassic)
                    .GetMethod("GetGraphicsModeFiltering", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(app, [newGfx, true])!;
                Equal(smallScreen ? OutputFiltering.SharpBilinear : newGfx
                    ? OutputFiltering.Xbr2 : OutputFiltering.SharpBilinearShader,
                    filter, "phone uses sharp-bilinear; larger screens retain their graphics-mode filter");
                return 0;
            });
    }

    static Resolution CreateResolution(Vector2 native)
    {
        Resolution resolution = new()
        {
            RatioCorrection = new Vector2f(60f / 64f, 72f / 64f),
            MinResolution = new Vector2(320, 200),
            MaxResolution = new Vector2(680, 320),
            Native = native
        };
        return resolution;
    }

    public class FilterCapabilities : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "get_SupportsXbr2Shader" or "get_SupportsSharpBilinearShader" => true,
            "get_ForceLinearOutputFiltering" or "get_ForceNearestPointOutputFiltering" or "get_DisableShaders" => false,
            _ => throw new NotSupportedException(method.Name)
        };
    }
}
