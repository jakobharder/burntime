using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Remaster.Scenes;

namespace Burntime.Remaster.Tests;

using static Program;

static class TraderLayoutTests
{
    internal static IEnumerable<Case<int>> Cases()
    {
        foreach (Vector2 viewport in new[] { new Vector2(416, 160), new Vector2(430, 160), new Vector2(468, 180) })
            yield return Int($"inventory footer actions fit {viewport}", 0, () =>
            {
                var (back, trade) = TraderScene.SmallScreenActionBounds(viewport);
                Equal(16, trade.Left - (viewport.x - 416) / 2 - 252, "normal trader-relative label offset");
                Equal(true, back.Left >= 24 && trade.Right <= viewport.x - 24, "side insets");
                Equal(true, back.Bottom <= viewport.y && trade.Bottom <= viewport.y, "touch areas remain on screen");
                Equal(true, back.Right < trade.Left && back.Top == trade.Top, "separate Back and Trade targets on one row");
                foreach (Rect bounds in new[] { back, trade })
                {
                    Equal(true, bounds.Width * 812f / viewport.x >= 44, "physical touch width");
                    Equal(true, bounds.Height * 375f / viewport.y >= 44, "physical touch height");
                }
                return 0;
            });

        yield return Int("small-screen trader leaves taps and swipes to its visible grids", 0, () =>
        {
            var app = (BurntimeClassic)RuntimeHelpers.GetUninitializedObject(typeof(BurntimeClassic));
            app.SmallDeviceUi = true;
            var scene = (TraderScene)RuntimeHelpers.GetUninitializedObject(typeof(TraderScene));
            typeof(Window).GetField("app", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(scene, app);
            scene.Position = new Vector2(0, -20);
            scene.Size = new Vector2(416, 200);
            foreach (Vector2 position in new[] { new Vector2(80, 96), new Vector2(210, 100), new Vector2(310, 96) })
                Equal(false, scene.OnTouchTap(position), "item tap reaches the grid without switching inventory");
            Equal(false, scene.OnTouchScroll(new Vector2(310, 80), new Vector2(40, 0)),
                "both inventories remain visible during a horizontal swipe");
            return 0;
        });
    }
}
