using Burntime.Framework;
using Burntime.MonoGame;
using Burntime.Platform;
using Burntime.Remaster;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.iOS;

// Opt-in integration check, isolated from the player's saves and settings.
internal sealed class IosSmokeTestRunner(string outputDirectory) : IVisualTestRunner
{
    public string OutputDirectory { get; } = outputDirectory;
    static readonly string[] Scenes = ["menu", "map", "location", "inventory", "options", .. VisualTestScenes.TouchNames];
    VisualTestScenes? scenes;
    BurntimeClassic? app;
    int index = -1, readyFrames;
    bool advance = true, complete;
    readonly bool saveOnly = Environment.GetEnvironmentVariable("BURNTIME_IOS_SMOKE_SAVE_ONLY") == "1";
    readonly System.Diagnostics.Stopwatch deadline = System.Diagnostics.Stopwatch.StartNew();

    void Finish(string result)
    {
        complete = true;
        System.IO.File.WriteAllText(Path.Combine(OutputDirectory, "result.txt"), result);
        Console.WriteLine(result);
    }

    public void Update(BurntimeGame game, BurntimeClassic application)
    {
        if (complete) return;
        try
        {
            app = application;
            if (deadline.Elapsed.TotalSeconds > 120)
            {
                Finish("FAIL: scene loading timed out");
                return;
            }
            if (scenes is null)
            {
                app.Process(0);
                scenes = new(app);
            }
            if (advance)
            {
                scenes.Open(Scenes[++index]);
                app.LastInputMode = InputMode.Touch;
                Log.Info("iOS smoke scene: " + Scenes[index]);
                advance = false;
                readyFrames = 0;
                deadline.Restart();
            }
            app.Process(0);
            game.RenderDevice.Begin();
            app.Render(game.MainTarget);
            game.RenderDevice.End();
        }
        catch (Exception error) { Finish("FAIL: " + error); }
    }

    public void Capture(BurntimeGame game)
    {
        if (complete || index < 0 || app is null) return;
        if (game.IsLoading || game.ResourceManager.IsLoading || game.LoadingStack != 0 ||
            game.BlendOverlay.BlendState != 0)
        {
            readyFrames = 0;
            return;
        }
        if (++readyFrames < 10) return;
        try
        {
            // Reload replaces the session, so the focused serialization run ends here.
            if (saveOnly && Scenes[index] == "map")
            {
                if (!scenes!.SaveAndReload("saves/ios-initial-test.sav"))
                {
                    Finish("FAIL: initial save/load round trip failed");
                    return;
                }
                Finish("PASS: new game, map, save and reload");
                return;
            }
            if (index + 1 == Scenes.Length)
            {
                if (!scenes!.SaveAndReload("saves/ios-test.sav"))
                {
                    Finish("FAIL: save/load round trip failed");
                    return;
                }
                Finish("PASS: menu, new game, map, location, inventory, options, touch inspection/second-tap/long-press/trader switching, save and reload");
            }
            else advance = true;
        }
        catch (Exception error) { Finish("FAIL: " + error); }
    }
}
