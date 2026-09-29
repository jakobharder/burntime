using System.Diagnostics;
using System.Reflection;
using System.Text;
using Burntime.Data.BurnGfx;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Remaster;
using Burntime.Remaster.Logic;

namespace Burntime.MonoGame;

/// <summary>
/// Crash-only UI fuzzing across ordinary game scenes. It deliberately avoids
/// assertions about content, values or navigation results so gameplay and data
/// changes expand the exercised state rather than requiring baseline updates.
/// </summary>
internal sealed class UiSoakRunner : IVisualTestRunner
{
    const float Step = 1f / 60;

    readonly int seed;
    readonly double requestedSeconds;
    readonly int stepsPerFrame;
    readonly Stopwatch wallTime = Stopwatch.StartNew();
    UiSoakScenario? scenario;
    double simulatedSeconds;
    double nextProgressSeconds = 60;
    bool complete;

    public string OutputDirectory { get; }

    UiSoakRunner(string outputDirectory, int seed, double requestedSeconds,
        int stepsPerFrame)
    {
        OutputDirectory = outputDirectory;
        this.seed = seed;
        this.requestedSeconds = requestedSeconds;
        this.stepsPerFrame = stepsPerFrame;
    }

    internal static bool IsRequested(string[] args) =>
        args.Contains("--ui-soak", StringComparer.OrdinalIgnoreCase);

    internal static int Run(string[] args)
    {
        UiSoakRunner? runner = null;
        try
        {
            if (!TryParse(args, out string output, out int seed,
                out double minutes, out int speed, out string? error))
            {
                Console.Error.WriteLine(error);
                Console.Error.WriteLine("Usage: Burntime --ui-soak OUTPUT_DIRECTORY " +
                    "[--minutes=N] [--seed=N] [--speed=N]");
                return 2;
            }

            Directory.CreateDirectory(output);
            FileSystem.UserFolderOverride = Path.Combine(output, "user");
            Directory.CreateDirectory(FileSystem.UserFolderOverride);
            System.IO.File.WriteAllText(
                Path.Combine(FileSystem.UserFolderOverride, "user.txt"),
                "newgfx=true\nprompts=2\nlanguage=en\nfullscreen=false\n" +
                "music=off\nmap_music=none\ncontroller_glyphs=xbox\n");
            Platform.Math.SetRandomSeed(seed);

            runner = new(output, seed, minutes * 60, speed);
            using BurntimeGame game = new(emulateSteamDeck: true);
            game.VisualTest = runner;
            game.Run();
            return runner.complete ? 0 : 1;
        }
        catch (Exception exception)
        {
            string? output = OutputArgument(args);
            if (output != null)
            {
                Directory.CreateDirectory(output);
                StringBuilder failure = new();
                failure.AppendLine(exception.ToString());
                if (runner?.scenario != null)
                {
                    failure.AppendLine();
                    failure.AppendLine("Recent actions:");
                    failure.Append(runner.scenario.RecentActions());
                }
                System.IO.File.WriteAllText(Path.Combine(output, "error.txt"),
                    failure.ToString());
            }
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
        if (complete)
            return;

        if (scenario == null)
        {
            app.VersionLabel = "ui-soak";
            app.LastInputMode = InputMode.Keyboard;
            app.Process(0);
            scenario = new UiSoakScenario(app, seed);
            scenario.Initialize();
            Log.Info($"UI SOAK: seed={seed}, simulatedSeconds={requestedSeconds}, speed={stepsPerFrame}");
        }

        for (int index = 0; index < stepsPerFrame &&
            simulatedSeconds < requestedSeconds; ++index)
        {
            scenario.Step(Step);
            app.Process(Step);
            simulatedSeconds += Step;
        }

        if (simulatedSeconds >= nextProgressSeconds)
        {
            Console.WriteLine($"UI soak: {TimeSpan.FromSeconds(simulatedSeconds)} simulated");
            nextProgressSeconds += 60;
        }

        game.MainTarget.Elapsed = Step;
        game.MainTarget.TotalElapsed += Step;
        game.RenderDevice.Begin();
        app.Render(game.MainTarget);
        game.RenderDevice.End();

        if (simulatedSeconds < requestedSeconds)
            return;

        complete = true;
        string report = scenario.Report(simulatedSeconds, wallTime.Elapsed);
        System.IO.File.WriteAllText(Path.Combine(OutputDirectory, "report.txt"), report);
        Console.WriteLine(report);
        game.Exit();
    }

    public void Capture(BurntimeGame game) { }

    static bool TryParse(string[] args, out string output, out int seed,
        out double minutes, out int speed, out string? error)
    {
        output = string.Empty;
        seed = 1;
        minutes = 30;
        speed = 8;
        error = null;

        int command = Array.FindIndex(args, argument =>
            argument.Equals("--ui-soak", StringComparison.OrdinalIgnoreCase));
        if (command < 0 || command + 1 >= args.Length ||
            args[command + 1].StartsWith("--", StringComparison.Ordinal))
        {
            error = "--ui-soak requires an output directory.";
            return false;
        }
        output = Path.GetFullPath(args[command + 1]);

        foreach (string argument in args)
        {
            if (argument.StartsWith("--minutes=", StringComparison.OrdinalIgnoreCase))
            {
                if (!double.TryParse(argument["--minutes=".Length..],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out minutes) || minutes <= 0)
                {
                    error = "--minutes must be a positive number.";
                    return false;
                }
            }
            else if (argument.StartsWith("--seed=", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(argument["--seed=".Length..], out seed))
                {
                    error = "--seed must be an integer.";
                    return false;
                }
            }
            else if (argument.StartsWith("--speed=", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(argument["--speed=".Length..], out speed) ||
                    speed is < 1 or > 64)
                {
                    error = "--speed must be between 1 and 64.";
                    return false;
                }
            }
        }
        return true;
    }

    static string? OutputArgument(string[] args)
    {
        int command = Array.FindIndex(args, argument =>
            argument.Equals("--ui-soak", StringComparison.OrdinalIgnoreCase));
        return command >= 0 && command + 1 < args.Length
            ? Path.GetFullPath(args[command + 1]) : null;
    }
}

internal sealed class UiSoakScenario
{
    static readonly string[] SceneKinds =
    [
        "location", "map", "inventory", "room", "trader", "doctor",
        "pub", "restaurant", "info", "statistics", "options"
    ];

    static readonly InputAction[] SemanticActions =
    [
        InputAction.MoveUp, InputAction.MoveDown, InputAction.MoveLeft,
        InputAction.MoveRight, InputAction.LeftArea, InputAction.RightArea,
        InputAction.PreviousTarget, InputAction.NextTarget,
        InputAction.ToggleInteractionMode
    ];

    static readonly InputMode[] Modes =
    [
        InputMode.Mouse, InputMode.Keyboard, InputMode.Gamepad, InputMode.Touch
    ];

    readonly BurntimeClassic app;
    readonly Random random;
    readonly Queue<string> trace = new();
    readonly Dictionary<string, int> sceneVisits = [];
    readonly Dictionary<InputMode, int> modeActions = [];
    readonly List<ItemType> itemTypes = [];
    float untilAction;
    int actionIndex;
    int sceneIndex = -1;
    int sceneActionCount;
    int semanticActions;
    int mouseHovers;
    int mouseClicks;
    int touchGestures;
    int itemRefreshes;
    double touchTime = 1;

    internal UiSoakScenario(BurntimeClassic app, int seed)
    {
        this.app = app;
        random = new Random(seed);
    }

    internal void Initialize()
    {
        // Use the same public fixture bootstrap as screenshot tests. The soak
        // runner intentionally owns only exploration, not game construction.
        new VisualTestScenes(app).Open("map");
        itemTypes.AddRange(app.Game.ItemTypes.Types.Where(type => type.IsSelectable));
        OpenNextScene();
    }

    internal void Step(float elapsed)
    {
        Player player = app.Game.World.ActivePlayerObj;
        player.IsDead = false;
        if (!player.Character.IsDead)
            player.Character.Health = 100;

        untilAction -= elapsed;
        if (untilAction > 0)
            return;
        untilAction = 0.08f + (float)random.NextDouble() * 0.20f;

        if (sceneActionCount++ >= 36)
        {
            OpenNextScene();
            return;
        }

        InputMode mode = Modes[actionIndex++ % Modes.Length];
        app.LastInputMode = mode;
        modeActions[mode] = modeActions.GetValueOrDefault(mode) + 1;

        if (mode is InputMode.Mouse or InputMode.Touch)
            PointAtRuntimeControl(mode);
        else
            PressSemanticAction(mode);
    }

    void OpenNextScene()
    {
        sceneActionCount = 0;
        string kind = SceneKinds[++sceneIndex % SceneKinds.Length];
        sceneVisits[kind] = sceneVisits.GetValueOrDefault(kind) + 1;
        AddTrace($"scene {kind}");

        Player player = app.Game.World.ActivePlayerObj;
        player.SelectGroup(player.Party);
        RefreshRuntimeItems(player.Character.Items);

        switch (kind)
        {
            case "location":
                app.SetScene("LocationScene");
                break;
            case "map":
                app.SetScene("MapScene");
                break;
            case "inventory":
                app.InventoryBackground = -1;
                app.InventoryRoom = null;
                app.SetScene("InventoryScene", player.Character);
                break;
            case "room":
                app.InventoryBackground = 0;
                app.InventoryRoom = app.Game.World.ActiveLocationObj.Rooms.FirstOrDefault();
                if (app.InventoryRoom != null)
                    RefreshRuntimeItems(app.InventoryRoom.Items);
                app.SetScene("InventoryScene", player.Character);
                break;
            case "trader":
                Trader? trader = app.Game.World.Traders.FirstOrDefault();
                if (trader == null)
                {
                    OpenNextScene();
                    return;
                }
                app.Game.World.ActiveTraderObj = trader;
                RefreshRuntimeItems(trader.Items);
                app.SetScene("TraderScene");
                break;
            case "doctor":
            case "pub":
            case "restaurant":
                app.SetScene("ServiceScene", new MapEntrance
                {
                    RoomType = kind == "doctor" ? RoomType.Doctor :
                        kind == "pub" ? RoomType.Pub : RoomType.Restaurant,
                    Background = 22
                });
                break;
            case "info":
                app.InfoCity = app.Game.World.ActiveLocationObj.Id;
                app.SetScene("InfoScene");
                break;
            case "statistics":
                app.SetScene("StatisticsScene");
                break;
            case "options":
                app.SetScene("OptionsScene");
                break;
        }
    }

    void RefreshRuntimeItems(ItemList items)
    {
        items.Clear();
        if (itemTypes.Count == 0)
            return;

        foreach (ItemType type in itemTypes.OrderBy(_ => random.Next())
            .Take(System.Math.Min(items.MaxCount, itemTypes.Count)))
        {
            items.Add(type.Generate());
        }
        itemRefreshes++;
        AddTrace($"items {items.Count}/{itemTypes.Count}");
    }

    void PressSemanticAction(InputMode mode)
    {
        InputAction action = SemanticActions[random.Next(SemanticActions.Length)];
        app.InputManager.Press(action);
        semanticActions++;
        AddTrace($"{mode} action {action}");
    }

    void PointAtRuntimeControl(InputMode mode)
    {
        Scene scene = ReadPrivate<Scene>(app.SceneManager, "activeScene");
        List<Window> visible = Descendants(scene)
            .Where(window => window.Size.x > 0 && window.Size.y > 0)
            .ToList();
        if (visible.Count == 0)
            return;

        List<Window> items = visible.Where(window =>
            window.GetType().Name == "ItemWindow").ToList();
        Window target = items.Count > 0 && random.Next(4) != 0
            ? items[random.Next(items.Count)]
            : visible[random.Next(visible.Count)];
        Vector2 point = ScreenBounds(target).Center;
        bool mayActivate = target.GetType().Name == "ItemWindow";

        if (mode == InputMode.Mouse)
        {
            app.DeviceManager.MouseMove(point);
            mouseHovers++;
            if (mayActivate && random.Next(3) != 0)
            {
                MouseButton button = random.Next(4) == 0
                    ? MouseButton.Right : MouseButton.Left;
                app.DeviceManager.MouseDown(point, button);
                app.DeviceManager.MouseClick(point, button);
                mouseClicks++;
                AddTrace($"mouse {button} {target.GetType().Name}");
            }
            else
                AddTrace($"mouse hover {target.GetType().Name}");
            return;
        }

        // Non-item controls may include load, quit, or delete. Hovering them is
        // useful render/tooltip coverage, but activating them would change the
        // lifecycle of the test rather than probe gameplay stability.
        if (!mayActivate)
        {
            app.DeviceManager.MouseMove(point);
            AddTrace($"touch inspect {target.GetType().Name}");
            return;
        }

        TouchGestureKind kind = random.Next(5) switch
        {
            0 => TouchGestureKind.LongPress,
            1 => TouchGestureKind.Drag,
            _ => TouchGestureKind.Tap
        };
        Vector2 end = kind == TouchGestureKind.Drag
            ? point + new Vector2(random.Next(-24, 25), random.Next(-24, 25))
            : point;
        app.SceneManager.QueueTouchGesture(new(kind, point, end, end - point,
            touchTime += 0.2), app.SceneManager.TouchInputContext);
        touchGestures++;
        AddTrace($"touch {kind} {target.GetType().Name}");
    }

    static Rect ScreenBounds(Window window)
    {
        Vector2 parentPosition = window.PositionOnScreen - window.Position;
        return window.Boundings + parentPosition;
    }

    static IEnumerable<Window> Descendants(Container parent)
    {
        foreach (Window window in parent.Windows)
        {
            if (!window.IsVisible)
                continue;
            yield return window;
            if (window is Container child)
                foreach (Window nested in Descendants(child))
                    yield return nested;
        }
    }

    void AddTrace(string entry)
    {
        trace.Enqueue($"{actionIndex}: {entry}");
        while (trace.Count > 256)
            trace.Dequeue();
    }

    internal string RecentActions() => string.Join(Environment.NewLine, trace);

    internal string Report(double seconds, TimeSpan elapsed)
    {
        StringBuilder report = new();
        report.AppendLine("Burntime UI soak completed");
        report.AppendLine($"Simulated: {TimeSpan.FromSeconds(seconds)}");
        report.AppendLine($"Wall time: {elapsed}");
        report.AppendLine($"Actions: {actionIndex}");
        report.AppendLine($"Semantic actions: {semanticActions}");
        report.AppendLine($"Mouse hovers: {mouseHovers}");
        report.AppendLine($"Mouse clicks: {mouseClicks}");
        report.AppendLine($"Touch gestures: {touchGestures}");
        report.AppendLine($"Runtime item refreshes: {itemRefreshes}");
        report.AppendLine("Input modes: " + string.Join(", ",
            modeActions.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}")));
        report.AppendLine("Scene visits: " + string.Join(", ",
            sceneVisits.Select(pair => $"{pair.Key}={pair.Value}")));
        return report.ToString();
    }

    static T ReadPrivate<T>(object owner, string name) =>
        (T)owner.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
}
