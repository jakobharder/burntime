using System.Collections.Generic;
using Burntime.Platform;
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
            Equal(new Vector2(455, 237), resolution.Game, "800p game resolution");
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
}
