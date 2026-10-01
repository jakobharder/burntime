using System.Diagnostics;
using System.Reflection;
using System.Text;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Framework.Network;
using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Remaster;
using Burntime.Remaster.GUI;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.MonoGame;

/// <summary>
/// Runs the real location scene with accelerated, deterministic input. This is
/// deliberately separate from the strategic headless simulation: rendering,
/// scene notifications, sounds, map overlays and input-mode branches stay live.
/// </summary>
internal sealed class LocationSoakRunner : IVisualTestRunner
{
    const float Step = 1f / 60;

    readonly int seed;
    readonly double requestedSeconds;
    readonly int stepsPerFrame;
    readonly Stopwatch wallTime = Stopwatch.StartNew();
    LocationSoakScenario? scenario;
    double simulatedSeconds;
    double nextProgressSeconds = 60;
    bool complete;

    public string OutputDirectory { get; }

    LocationSoakRunner(string outputDirectory, int seed, double requestedSeconds,
        int stepsPerFrame)
    {
        OutputDirectory = outputDirectory;
        this.seed = seed;
        this.requestedSeconds = requestedSeconds;
        this.stepsPerFrame = stepsPerFrame;
    }

    internal static bool IsRequested(string[] args) =>
        args.Contains("--location-soak", StringComparer.OrdinalIgnoreCase);

    internal static int Run(string[] args)
    {
        try
        {
            if (!TryParse(args, out string output, out int seed,
                out double minutes, out int speed, out string? error))
            {
                Console.Error.WriteLine(error);
                Console.Error.WriteLine("Usage: Burntime --location-soak OUTPUT_DIRECTORY " +
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

            LocationSoakRunner runner = new(output, seed, minutes * 60, speed);
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
                System.IO.File.WriteAllText(Path.Combine(output, "error.txt"),
                    exception.ToString());
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
            app.VersionLabel = "location-soak";
            app.LastInputMode = InputMode.Keyboard;
            app.Process(0);
            scenario = new LocationSoakScenario(app, seed);
            scenario.Initialize();
            Log.Info($"LOCATION SOAK: seed={seed}, simulatedSeconds={requestedSeconds}, speed={stepsPerFrame}");
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
            Console.WriteLine($"Location soak: {TimeSpan.FromSeconds(simulatedSeconds)} simulated");
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
        minutes = 60;
        speed = 8;
        error = null;

        int command = Array.FindIndex(args, argument =>
            argument.Equals("--location-soak", StringComparison.OrdinalIgnoreCase));
        if (command < 0 || command + 1 >= args.Length ||
            args[command + 1].StartsWith("--", StringComparison.Ordinal))
        {
            error = "--location-soak requires an output directory.";
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
            argument.Equals("--location-soak", StringComparison.OrdinalIgnoreCase));
        return command >= 0 && command + 1 < args.Length
            ? Path.GetFullPath(args[command + 1]) : null;
    }
}

internal sealed class LocationSoakScenario
{
    static readonly InputMode[] Modes =
    [
        InputMode.Mouse, InputMode.Keyboard, InputMode.Gamepad, InputMode.Touch
    ];
    static readonly InputAction[] Directions =
    [
        InputAction.MoveUp, InputAction.MoveDown,
        InputAction.MoveLeft, InputAction.MoveRight
    ];

    readonly BurntimeClassic app;
    readonly Random random;
    readonly Dictionary<InputMode, int> modeActions = [];
    readonly Dictionary<CharClass, int> attacksByTarget = [];
    LocationScene scene = null!;
    MapView view = null!;
    Player player = null!;
    Location location = null!;
    List<Character> targets = [];
    MethodInfo clickObject = null!;
    float untilAction;
    int actionIndex;
    int attacks;
    int moves;
    int characterCycles;
    int sceneReactivations;
    int targetDeaths;

    internal LocationSoakScenario(BurntimeClassic app, int seed)
    {
        this.app = app;
        random = new Random(seed);
    }

    internal void Initialize()
    {
        VisualTestScenes fixtures = new(app);
        fixtures.Open("map");

        player = app.Game.World.ActivePlayerObj;
        location = player.Location.IsCity
            ? app.Game.World.Locations.First(candidate => !candidate.IsCity)
            : player.Location;
        player.Location = location;
        player.IsDead = false;
        app.Game.World.Time = 1;
        app.Game.World.RoundTime = 1_000_000_000;

        foreach (Character member in player.Party)
        {
            member.Position = location.EntryPoint;
            member.Health = 100;
        }

        foreach (Character recruit in app.Game.World.AllCharacters.Where(character =>
            character.Player == null && character.IsHuman && !character.IsTrader &&
            !character.IsDead).Take(3).ToArray())
        {
            recruit.Position = WalkableNear(location.EntryPoint, 12, 35);
            recruit.Hire(player, waivePayment: true);
        }
        player.SelectGroup(player.Party);

        List<Character> candidates = app.Game.World.AllCharacters.Where(character =>
            character != player.Character && !player.Party.Contains(character) &&
            !character.IsDead).ToList();
        targets = candidates.Where(character => character.Class == CharClass.Dog)
            .Take(3)
            .Concat(candidates.Where(character => character.Class == CharClass.Mutant)
                .Take(3))
            .Concat(candidates.Where(character => character.Class == CharClass.Trader)
                .Take(3))
            .Concat(candidates.Where(character => character.Class is not
                (CharClass.Dog or CharClass.Mutant or CharClass.Trader)).Take(3))
            .Distinct()
            .ToList();

        int targetIndex = 0;
        foreach (Character target in targets)
        {
            if (target.Player != null && target.Player != player)
                target.Player.Location = location;
            target.Location = location;
            target.Position = WalkableNear(location.EntryPoint, 18,
                45 + targetIndex++ * 3);
            target.Health = 100;
            // Keep the synthetic encounter non-lethal so a normal game-over
            // cannot end a long soak. Creature/trader innate attacks remain.
            target.Items.Clear();
            target.Path.Stop(target.Position);
        }

        app.SetScene("LocationScene");
        app.Process(0);
        BindScene();
    }

    internal void Step(float elapsed)
    {
        app.Game.World.Time = 1;
        KeepPlayerAlive();
        ReviveTargets();
        untilAction -= elapsed;
        if (untilAction > 0)
            return;

        untilAction = 0.12f + (float)random.NextDouble() * 0.35f;
        app.InputManager.ClearDown(InputSource.Keyboard);
        app.InputManager.ClearDown(InputSource.GamepadOne);

        InputMode mode = Modes[actionIndex % Modes.Length];
        app.LastInputMode = mode;
        modeActions[mode] = modeActions.GetValueOrDefault(mode) + 1;

        switch (actionIndex++ % 10)
        {
            case 0:
            case 2:
            case 4:
            case 6:
            case 8:
                Attack(mode);
                break;
            case 1:
            case 5:
                Move(mode);
                break;
            case 3:
                CycleCharacter(mode);
                break;
            case 7:
                app.InputManager.Press(InputAction.ToggleInteractionMode);
                break;
            case 9:
                if (actionIndex % 200 == 0)
                    ReactivateScene();
                else
                    Move(mode);
                break;
        }
    }

    void Attack(InputMode mode)
    {
        Character? target = targets.Where(candidate => !candidate.IsDead &&
            candidate.Location == location).OrderBy(_ => random.Next()).FirstOrDefault();
        if (target == null)
        {
            ReactivateScene();
            return;
        }

        Character actor = player.SelectedCharacter;
        if (actor == null || actor.IsDead)
            return;
        target.Position = WalkableNear(actor.Position, 12, 28);
        target.Path.Stop(target.Position);
        view.CenterTo(actor.Position);

        if (mode is InputMode.Keyboard or InputMode.Gamepad)
        {
            view.HoveredObject = target;
            location.HoverCharacter = target;
            app.InputManager.Press(InputAction.Action);
        }
        else
        {
            MouseButton button = mode == InputMode.Mouse
                ? MouseButton.Right : MouseButton.Left;
            clickObject.Invoke(scene, [view,
                new ObjectArgs(target, target.Position, button)]);
        }

        attacks++;
        attacksByTarget[target.Class] =
            attacksByTarget.GetValueOrDefault(target.Class) + 1;
    }

    void Move(InputMode mode)
    {
        if (mode is InputMode.Keyboard or InputMode.Gamepad)
        {
            InputSource source = mode == InputMode.Gamepad
                ? InputSource.GamepadOne : InputSource.Keyboard;
            app.InputManager.SetDown(source,
                Directions[random.Next(Directions.Length)], true);
        }
        else
        {
            scene.OnMouseClickMap(WalkableNear(player.SelectedCharacter.Position,
                30, 120), MouseButton.Left);
        }
        moves++;
    }

    void CycleCharacter(InputMode mode)
    {
        app.LastInputMode = mode is InputMode.Mouse or InputMode.Touch
            ? InputMode.Keyboard : mode;
        app.InputManager.Press(random.Next(2) == 0
            ? InputAction.LeftArea : InputAction.RightArea);
        characterCycles++;
    }

    void ReactivateScene()
    {
        app.SetScene("LocationScene");
        app.Process(0);
        BindScene();
        sceneReactivations++;
    }

    void BindScene()
    {
        scene = ReadPrivate<LocationScene>(app.SceneManager, "activeScene");
        view = ReadPrivate<MapView>(scene, "view");
        clickObject = typeof(LocationScene).GetMethod("view_ClickObject",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
    }

    void KeepPlayerAlive()
    {
        player.IsDead = false;
        foreach (Character member in player.Party)
            if (!member.IsDead)
                member.Health = 100;
    }

    void ReviveTargets()
    {
        foreach (Character target in targets)
        {
            if (!target.IsDead)
            {
                if (target.Health < 35)
                    target.Health = 100;
                continue;
            }

            targetDeaths++;
            target.Revive();
            target.Location = location;
            target.Position = WalkableNear(player.SelectedCharacter.Position, 25, 90);
            target.Path.Stop(target.Position);
        }
    }

    Vector2 WalkableNear(Vector2 origin, int minimumDistance, int maximumDistance)
    {
        for (int attempt = 0; attempt < 50; ++attempt)
        {
            double angle = random.NextDouble() * System.Math.PI * 2;
            float distance = minimumDistance +
                (float)random.NextDouble() * (maximumDistance - minimumDistance);
            Vector2 candidate = origin + new Vector2(
                (int)System.Math.Round(System.Math.Cos(angle) * distance),
                (int)System.Math.Round(System.Math.Sin(angle) * distance));
            if (location.Map.Mask.IsWalkableMapPosition(candidate))
                return candidate;
        }
        return origin;
    }

    internal string Report(double seconds, TimeSpan elapsed)
    {
        StringBuilder report = new();
        report.AppendLine("Burntime location soak completed");
        report.AppendLine($"Simulated: {TimeSpan.FromSeconds(seconds)}");
        report.AppendLine($"Wall time: {elapsed}");
        report.AppendLine($"Actions: {actionIndex}");
        report.AppendLine($"Attacks: {attacks}");
        report.AppendLine($"Moves: {moves}");
        report.AppendLine($"Character cycles: {characterCycles}");
        report.AppendLine($"Scene reactivations: {sceneReactivations}");
        report.AppendLine($"Target deaths revived: {targetDeaths}");
        report.AppendLine("Input modes: " + string.Join(", ",
            modeActions.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}")));
        report.AppendLine("Attack targets: " + string.Join(", ",
            attacksByTarget.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}")));
        return report.ToString();
    }

    static T ReadPrivate<T>(object owner, string name) =>
        (T)owner.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
}
