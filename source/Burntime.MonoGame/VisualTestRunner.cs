using System.Diagnostics;
using System.Text.Json;
using Burntime.Platform;
using Burntime.Framework;
using Burntime.Platform.IO;
using Burntime.Remaster;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Burntime.MonoGame;

internal sealed class VisualTestRunner(string outputDirectory, bool touchOnly = false, string? storeMode = null, string language = "en", bool safeAreaReview = false, string? scenario = null, bool gamepad = false, bool touchControls = false) : IVisualTestRunner
{
    public string OutputDirectory { get; } = outputDirectory;
    readonly string[] names = scenario != null ? scenario.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : storeMode?.StartsWith("phone", StringComparison.Ordinal) == true ? VisualTestScenes.PhoneHudNames :
        storeMode != null ? VisualTestScenes.StoreNames :
        touchOnly ? VisualTestScenes.TouchNames : safeAreaReview
            // Closing the victory dialog advances the session; capture it last.
            ? [.. VisualTestScenes.PhoneScreenNames.Where(name => name != "last-rival"), .. VisualTestScenes.HudMenuNames, "last-rival"]
            : VisualTestScenes.Names;
    readonly List<string> captured = [];
    readonly List<string> safeAreaFailures = [];
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
            args[1] is not ("ipad" or "steam" or "macos" or "phone" or "phone-safe-left" or "phone-safe-right") ||
            args.Length == 4 && args[3] is not ("--language=en" or "--language=de"))
        {
            Console.Error.WriteLine("Usage: Burntime --store-capture ipad|steam|macos|phone|phone-safe-left|phone-safe-right OUTPUT_DIRECTORY [--language=en|--language=de]");
            return 2;
        }
        bool phone = args[1].StartsWith("phone", StringComparison.Ordinal);
        string captureLanguage = args.Length == 4 && args[3] == "--language=de" ? "de" : "en";
        string output = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(output);
        if ((phone ? VisualTestScenes.PhoneHudNames : VisualTestScenes.StoreNames).Any(name => System.IO.File.Exists(Path.Combine(output, name + ".png"))))
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
            "phone" or "phone-safe-left" or "phone-safe-right" => new Burntime.Platform.Vector2(2340, 1080),
            "ipad" => new Burntime.Platform.Vector2(2752, 2064),
            "macos" => new Burntime.Platform.Vector2(2560, 1600),
            _ => new Burntime.Platform.Vector2(1920, 1080)
        };
        VisualTestRunner runner = new(output, storeMode: args[1], language: captureLanguage);
        try
        {
            using BurntimeGame game = new(disableShaders: (args[1] == "ipad" || phone),
                windowSizeOverride: new Burntime.Platform.Vector2(960, 720));
            game.CaptureSize = size;
            game.EmulateIpadLayout = (args[1] == "ipad" || phone);
            game.EmulateSmallDeviceUi = phone;
            if (args[1] == "phone-safe-left")
                game.Resolution.NativeSafeAreaInsets = new(132, 12, 30, 63);
            else if (args[1] == "phone-safe-right")
                game.Resolution.NativeSafeAreaInsets = new(30, 12, 132, 63);
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
        if (args.Length is < 3 or > 8 || (args.Contains("--gamepad") && (args.Contains("--touch") || args.Contains("--touch-controls"))) || args.Skip(3).Any(arg =>
            !arg.StartsWith("--scenario=", StringComparison.Ordinal) &&
            arg is not ("--native-filter" or "--language=en" or "--language=de" or "--touch" or "--gamepad" or "--touch-controls" or "--phone-safe-left" or "--phone-safe-right" or "--phone-16x9")) || args[0] != "--visual-test" ||
            args[1] is not ("classic" or "newgfx" or "classic-no-hints"))
        {
            Console.Error.WriteLine("Usage: Burntime --visual-test classic|newgfx|classic-no-hints OUTPUT_DIRECTORY [--native-filter] [--language=en|--language=de] [--touch|--gamepad|--touch-controls] [--phone-safe-left|--phone-safe-right] [--phone-16x9] [--scenario=NAME]");
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
        bool phone = args.Contains("--phone-safe-left") || args.Contains("--phone-safe-right") || args.Contains("--phone-16x9");
        string? scenario = args.FirstOrDefault(arg => arg.StartsWith("--scenario=", StringComparison.Ordinal))?[11..];
        VisualTestRunner runner = new(output, args.Contains("--touch"), safeAreaReview: phone, scenario: scenario, gamepad: args.Contains("--gamepad"), touchControls: args.Contains("--touch-controls"));
        try
        {
            using BurntimeGame game = new(emulateSteamDeck: true, disableShaders: phone,
                nearestPointOutputFiltering: args[1] != "newgfx" && !args.Contains("--native-filter"));
            if (phone)
            {
                game.CaptureSize = args.Contains("--phone-16x9") ? new(1920, 1080) : new(2340, 1080);
                game.EmulateIpadLayout = true;
                game.EmulateSmallDeviceUi = true;
                game.Resolution.NativeSafeAreaInsets = args.Contains("--phone-safe-left")
                    ? new(132, 12, 30, 63) : new(30, 12, 132, 63);
            }
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
        // Keep fixture setup deterministic, then exercise controller-specific UI
        // through the normal scene update/render paths for this optional pass.
        if (gamepad) app.LastInputMode = InputMode.Gamepad;
        else if (touchControls) app.LastInputMode = InputMode.Touch;
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
        if (scenes!.RunPendingCaptureAction())
        {
            // Hit tests must use the same settled layout the player sees.
            readyFrames = 0;
            deadline.Restart();
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
        if (storeMode?.StartsWith("phone", StringComparison.Ordinal) == true) scenes!.ValidatePhoneHudCapture(name);
        if (safeAreaReview) safeAreaFailures.AddRange(scenes!.ValidateSafeScreenCapture(name));
        captured.Add(name);
        Console.WriteLine($"Captured {name}");
        nextScene = true;
        if (index + 1 == names.Length)
        {
            Complete = safeAreaFailures.Count == 0;
            if (safeAreaReview)
                System.IO.File.WriteAllText(Path.Combine(OutputDirectory, "safe-area-errors.json"),
                    JsonSerializer.Serialize(safeAreaFailures, new JsonSerializerOptions { WriteIndented = true }));
            System.IO.File.WriteAllText(Path.Combine(OutputDirectory, "captures.json"),
                JsonSerializer.Serialize(captured));
            if (storeMode != null)
                System.IO.File.WriteAllText(Path.Combine(OutputDirectory, "manifest.json"),
                    JsonSerializer.Serialize(new { mode = storeMode, language, boss = "Jakob", seed = 123,
                        width, height, safeArea = new { left = game.Resolution.SafeArea.Left, top = game.Resolution.SafeArea.Top, right = game.Resolution.SafeArea.Right, bottom = game.Resolution.SafeArea.Bottom }, nativeInsets = game.Resolution.NativeSafeAreaInsets, city = scenes!.CaptureCityName, day = 42,
                        scenarioDays = names.ToDictionary(
                            name => name, name => name == "01-city" ? 45 : 42),
                        input = storeMode?.StartsWith("phone", StringComparison.Ordinal) == true ? "mixed" : storeMode == "ipad" ? "touch" : storeMode == "macos" ? "mouse" : "gamepad",
                        glyphs = storeMode?.StartsWith("phone", StringComparison.Ordinal) == true ? "touch/steam" : storeMode == "ipad" ? "touch" : storeMode == "macos" ? "mouse" : "steam",
                        renderer = game.OutputFiltering.ToString(), campaign = scenes.StoreCaptureState,
                        captured }, new JsonSerializerOptions { WriteIndented = true }));
            game.Exit();
        }
    }
}
