using System.Diagnostics;
using System.Text.Json;
using Burntime.Platform;
using Burntime.Framework;
using Burntime.Platform.IO;
using Burntime.Remaster;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Burntime.MonoGame;

internal sealed class VisualTestRunner(string outputDirectory, bool touchOnly = false, string? storeMode = null, string language = "en") : IVisualTestRunner
{
    public string OutputDirectory { get; } = outputDirectory;
    readonly string[] names = storeMode != null ? VisualTestScenes.StoreNames :
        touchOnly ? VisualTestScenes.TouchNames : VisualTestScenes.Names;
    readonly List<string> captured = [];
    readonly Stopwatch deadline = Stopwatch.StartNew();
    VisualTestScenes? scenes;
    int index = -1;
    int readyFrames;
    bool nextScene = true;
    int animationFrames;
    bool advanceAnimation;
    public bool Complete { get; private set; }

    public static int RunStoreCapture(string[] args)
    {
        if (args.Length is < 3 or > 4 || args[0] != "--store-capture" ||
            args[1] is not ("ipad" or "steam" or "macos") ||
            args.Length == 4 && args[3] is not ("--language=en" or "--language=de"))
        {
            Console.Error.WriteLine("Usage: Burntime --store-capture ipad|steam|macos OUTPUT_DIRECTORY [--language=en|--language=de]");
            return 2;
        }
        string captureLanguage = args.Length == 4 && args[3] == "--language=de" ? "de" : "en";
        string output = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(output);
        if (VisualTestScenes.StoreNames.Any(name => System.IO.File.Exists(Path.Combine(output, name + ".png"))))
        {
            Console.Error.WriteLine("Use a fresh output directory; store captures are never overwritten.");
            return 2;
        }
        FileSystem.UserFolderOverride = Path.Combine(output, "user");
        Directory.CreateDirectory(FileSystem.UserFolderOverride);
        System.IO.File.WriteAllText(Path.Combine(FileSystem.UserFolderOverride, "user.txt"),
            $"newgfx=true\nprompts=0\nlanguage={captureLanguage}\nfullscreen=false\nmusic=off\nmap_music=none\ncontroller_glyphs=steam\n");
        var size = args[1] switch
        {
            "ipad" => new Burntime.Platform.Vector2(2752, 2064),
            "macos" => new Burntime.Platform.Vector2(2560, 1600),
            _ => new Burntime.Platform.Vector2(1920, 1080)
        };
        VisualTestRunner runner = new(output, storeMode: args[1], language: captureLanguage);
        try
        {
            using BurntimeGame game = new(disableShaders: args[1] == "ipad",
                windowSizeOverride: new Burntime.Platform.Vector2(960, 720));
            game.CaptureSize = size;
            game.EmulateIpadLayout = args[1] == "ipad";
            game.VisualTest = runner;
            game.Run();
            return runner.Complete ? 0 : 1;
        }
        catch (Exception exception)
        {
            System.IO.File.WriteAllText(Path.Combine(output, "error.txt"), exception.ToString());
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally { FileSystem.UserFolderOverride = null; }
    }

    public static int Run(string[] args)
    {
        // Deliberately small private CLI; the Python script owns the user interface.
        if (args.Length is < 3 or > 6 || args.Skip(3).Any(arg =>
            arg is not ("--native-filter" or "--language=en" or "--language=de" or "--touch")) || args[0] != "--visual-test" ||
            args[1] is not ("classic" or "newgfx" or "classic-no-hints"))
        {
            Console.Error.WriteLine("Usage: Burntime --visual-test classic|newgfx|classic-no-hints OUTPUT_DIRECTORY [--native-filter] [--language=en|--language=de] [--touch]");
            return 2;
        }
        string output = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(output);
        FileSystem.UserFolderOverride = Path.Combine(output, "user");
        Directory.CreateDirectory(FileSystem.UserFolderOverride);
        int hints = (int)(args[1] == "classic-no-hints"
            ? UIHintVisibilityMode.Hide : UIHintVisibilityMode.Full);
        string language = args.Contains("--language=de") ? "de" : "en";
        System.IO.File.WriteAllText(Path.Combine(FileSystem.UserFolderOverride, "user.txt"),
            $"newgfx={(args[1] == "newgfx" ? "true" : "false")}\nprompts={hints}\nlanguage={language}\nfullscreen=false\nmusic=off\nmap_music=none\ncontroller_glyphs=xbox\n");
        Platform.Math.SetRandomSeed(123);
        VisualTestRunner runner = new(output, args.Contains("--touch"));
        try
        {
            using BurntimeGame game = new(emulateSteamDeck: true,
                nearestPointOutputFiltering: args[1] != "newgfx" && !args.Contains("--native-filter"));
            game.VisualTest = runner;
            game.Run();
            return runner.Complete ? 0 : 1;
        }
        catch (Exception exception)
        {
            System.IO.File.WriteAllText(Path.Combine(output, "error.txt"), exception.ToString());
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            FileSystem.UserFolderOverride = null;
        }
    }

    public void Update(BurntimeGame game, BurntimeClassic app)
    {
        if (Complete) return;
        if (deadline.Elapsed > TimeSpan.FromSeconds(60))
        {
            System.IO.File.WriteAllText(Path.Combine(OutputDirectory, "error.txt"),
                $"Timed out loading scenario {names[System.Math.Max(0, index)]}");
            game.Exit();
            return;
        }
        if (scenes == null)
        {
            app.VersionLabel = "visual-test";
            app.LastInputMode = InputMode.Keyboard;
            app.Process(0); // normal startup initializes the module and first scene
            scenes = new(app, storeMode == null ? "Visual Test" : "Jakob");
        }
        if (nextScene)
        {
            index++;
            Log.Info($"VISUAL SCENARIO: {names[index]}");
            if (storeMode == null) scenes.Open(names[index]);
            else scenes.OpenStoreCapture(names[index], storeMode == "ipad" ? InputMode.Touch :
                storeMode == "macos" ? InputMode.Mouse : InputMode.Gamepad);
            nextScene = false;
            readyFrames = 0;
            animationFrames = names[index] == "menu" ? 60 : 0;
            advanceAnimation = false;
            game.MainTarget.TotalElapsed = 0;
            deadline.Restart();
        }
        // Freeze scene time except for the menu's fixed, post-load animation warmup.
        // Resource requests, GPU uploads and ordinary scene updates still run every frame.
        float elapsed = advanceAnimation && animationFrames > 0 &&
            !game.ResourceManager.IsLoading && game.LoadingStack == 0 ? 1f / 60 : 0;
        if (elapsed > 0) animationFrames--;
        app.Process(elapsed);
        game.MainTarget.Elapsed = elapsed;
        game.MainTarget.TotalElapsed += elapsed;
        game.RenderDevice.Begin();
        app.Render(game.MainTarget);
        game.RenderDevice.End();
    }

    public void Capture(BurntimeGame game)
    {
        if (Complete || index < 0) return;
        if (game.IsLoading || game.ResourceManager.IsLoading || game.LoadingStack != 0 ||
            game.BlendOverlay.BlendState != 0)
        {
            readyFrames = 0;
            return;
        }
        // Allow lazy requests and GPU uploads generated by the completed scene to settle.
        if (++readyFrames < 3) return;
        if (animationFrames > 0)
        {
            advanceAnimation = true;
            return;
        }
        var presentation = game.GraphicsDevice.PresentationParameters;
        int width = game.CaptureTarget?.Width ?? presentation.BackBufferWidth,
            height = game.CaptureTarget?.Height ?? presentation.BackBufferHeight;
        Color[] pixels = new Color[width * height];
        if (game.CaptureTarget != null) game.CaptureTarget.GetData(pixels);
        else game.GraphicsDevice.GetBackBufferData(pixels);
        // The window presents the already-composited RGB values as opaque. Backbuffer
        // alpha contains blend-pass bookkeeping, not transparency for the screenshot.
        for (int i = 0; i < pixels.Length; i++)
            pixels[i].A = 255;
        using Texture2D image = new(game.GraphicsDevice, width, height);
        image.SetData(pixels);
        string name = names[index];
        using (var stream = System.IO.File.Create(Path.Combine(OutputDirectory, name + ".png")))
            image.SaveAsPng(stream, width, height);
        captured.Add(name);
        Console.WriteLine($"Captured {name}");
        nextScene = true;
        if (index + 1 == names.Length)
        {
            Complete = true;
            System.IO.File.WriteAllText(Path.Combine(OutputDirectory, "captures.json"),
                JsonSerializer.Serialize(captured));
            if (storeMode != null)
                System.IO.File.WriteAllText(Path.Combine(OutputDirectory, "manifest.json"),
                    JsonSerializer.Serialize(new { mode = storeMode, language, boss = "Jakob", seed = 123,
                        width, height, city = scenes!.CaptureCityName, day = 42,
                        scenarioDays = VisualTestScenes.StoreNames.ToDictionary(
                            name => name, name => name == "01-city" ? 45 : 42),
                        input = storeMode == "ipad" ? "touch" : storeMode == "macos" ? "mouse" : "gamepad",
                        glyphs = storeMode == "ipad" ? "touch" : storeMode == "macos" ? "mouse" : "steam",
                        renderer = game.OutputFiltering.ToString(), campaign = scenes.StoreCaptureState,
                        captured }, new JsonSerializerOptions { WriteIndented = true }));
            game.Exit();
        }
    }
}
