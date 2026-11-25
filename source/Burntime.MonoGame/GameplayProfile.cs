using System.Diagnostics;
using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Burntime.Framework;
using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Remaster;
using Burntime.Remaster.GUI;
using Burntime.Remaster.Logic;

namespace Burntime.MonoGame;

// Opt-in normal-speed profiling. Does not use the single-threaded visual-test loop.
internal sealed class GameplayProfile
{
    internal readonly record struct Sample(long Wall, double Cpu, long Allocated);
    sealed class Phase
    {
        internal readonly List<double> Wall = new(4096), Cpu = new(4096);
        internal long Allocated;
    }
    readonly Dictionary<string, Phase> phases = new()
    {
        ["simulation"] = new(), ["scene_commands"] = new(),
        ["render_prepare"] = new(), ["render_submit"] = new(), ["presentation"] = new(),
        ["sprite_sort"] = new(), ["scene_gpu_commands"] = new(), ["output_scaling"] = new()
    };
    readonly Stopwatch clock = new();
    readonly string scenario;
    readonly int seconds;
    readonly bool music;
    readonly object sync = new();
    volatile bool measuring, complete;
    volatile bool initialized, screenshotSaved;
    bool written;
    double processCpuStart, processCpuEnd, measurementStart, measurementEnd, nextAction;
    long allocatedStart, allocatedEnd;
    int[] collectionsStart = new int[3], collectionsEnd = new int[3];
    int actors, party, actionIndex, minActors = int.MaxValue, maxActors;
    double entityTotal;
    int renderFrames;
    public int FramesPerSecond { get; }
    public string OutputDirectory { get; }

    GameplayProfile(string scenario, int fps, int seconds, bool music, string output)
    {
        this.scenario = scenario;
        FramesPerSecond = fps;
        this.seconds = seconds;
        this.music = music;
        OutputDirectory = output;
    }

    internal static GameplayProfile? Create()
    {
        string? scenario = Environment.GetEnvironmentVariable("BURNTIME_PROFILE_GAMEPLAY");
        if (scenario is not ("map" or "location")) return null;
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsIOS())
        {
            Console.Error.WriteLine("Gameplay profiling currently requires Darwin CPU clocks (macOS/iOS).");
            return null;
        }
        int fps = Environment.GetEnvironmentVariable("BURNTIME_PROFILE_FPS") == "60" ? 60 : 30;
        int seconds = int.TryParse(Environment.GetEnvironmentVariable("BURNTIME_PROFILE_SECONDS"), out int n)
            ? System.Math.Clamp(n, 10, 120) : 30;
        bool music = Environment.GetEnvironmentVariable("BURNTIME_PROFILE_MUSIC") == "1";
        string output = Environment.GetEnvironmentVariable("BURNTIME_PROFILE_OUTPUT") ??
            Path.Combine(OperatingSystem.IsIOS() ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : Path.GetTempPath(), "BurntimeProfiles", $"{scenario}-{fps}");
        try
        {
            Directory.CreateDirectory(Path.Combine(output, "user"));
            FileSystem.UserFolderOverride = Path.Combine(output, "user");
            System.IO.File.WriteAllText(Path.Combine(FileSystem.UserFolderOverride, "user.txt"),
                "newgfx=true\nlanguage=en\nprompts=2\nfullscreen=false\ncontroller_glyphs=xbox\n" +
                (music ? "music=on\nmap_music=2\n" : "music=off\nmap_music=none\n"));
            return new(scenario, fps, seconds, music, output);
        }
        catch (Exception error)
        {
            FileSystem.UserFolderOverride = null;
            Console.Error.WriteLine("Profiling unavailable: " + error.Message);
            return null;
        }
    }

    internal void BeforeFrame(BurntimeGame host, BurntimeClassic app)
    {
        if (complete) return;
        if (!initialized)
        {
            app.Process(0);
            new VisualTestScenes(app, "CPU profile").Open("map");
            var activeScene = typeof(SceneManager).GetField("activeScene", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app.SceneManager)!;
            if (activeScene.GetType().GetField("_manualWindow", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(activeScene) is ManualWindow manual && manual.IsVisible)
                manual.Hide();
            if (scenario == "location") SetupLocation(app);
            app.LastInputMode = InputMode.Touch;
            app.RenderMouse = false;
            app.ShowManualOnNextWorldMap = false;
            app.Game.World.RoundTime = 1_000_000;
            app.Process(0);
            party = app.Game.World.ActivePlayerObj.Party.Count;
            clock.Start();
            initialized = true;
            Console.WriteLine($"PROFILE warmup: {scenario}, {FramesPerSecond} FPS, output={OutputDirectory}");
        }
        double now = clock.Elapsed.TotalSeconds;
        if (now >= nextAction)
        {
            nextAction = now + 2;
            if (scenario == "map")
            {
                // Browse neighboring camps without entering another scene or ending the turn.
                app.InputManager.Press((actionIndex++ % 4) switch
                {
                    0 => InputAction.MoveRight, 1 => InputAction.MoveDown,
                    2 => InputAction.MoveLeft, _ => InputAction.MoveUp
                });
            }
            else
            {
                var location = app.Game.World.ActiveLocationObj;
                Vector2 origin = location.EntryPoint;
                int direction = actionIndex++ % 4;
                Vector2 target = origin + (direction switch
                {
                    0 => new Vector2(45, 0), 1 => new Vector2(0, 35),
                    2 => new Vector2(-35, 0), _ => new Vector2(0, -25)
                });
                if (location.Map.Mask.IsWalkableMapPosition(target))
                    foreach (Character member in app.Game.World.ActivePlayerObj.Party)
                        member.Path.MoveTo = target;
                foreach (Character member in app.Game.World.ActivePlayerObj.Party)
                    if (!member.IsDead) member.Health = 100;
            }
            app.LastInputMode = InputMode.Touch;
        }
        if (scenario == "location")
        {
            actors = app.Game.World.ActiveLocationObj.Characters.Count(c => !c.IsDead);
            if (measuring) { minActors = System.Math.Min(minActors, actors); maxActors = System.Math.Max(maxActors, actors); }
        }
        // Exclude startup, texture uploads, and scene fades from steady-state results.
        if (!measuring && screenshotSaved && now >= 12 && !host.IsLoading && !host.ResourceManager.IsLoading &&
            host.LoadingStack == 0 && host.BlendOverlay.BlendState == 0)
        {
            measurementStart = now;
            processCpuStart = CpuTime(12);
            allocatedStart = GC.GetTotalAllocatedBytes(false);
            for (int i = 0; i < 3; i++) collectionsStart[i] = GC.CollectionCount(i);
            measuring = true;
            Console.WriteLine("PROFILE measurement started");
        }
        if (measuring && now - measurementStart >= seconds)
        {
            measuring = false;
            measurementEnd = now;
            processCpuEnd = CpuTime(12);
            allocatedEnd = GC.GetTotalAllocatedBytes(false);
            for (int i = 0; i < 3; i++) collectionsEnd[i] = GC.CollectionCount(i);
            complete = true;
        }
    }

    static void SetupLocation(BurntimeClassic app)
    {
        Player player = app.Game.World.ActivePlayerObj;
        Location location = player.Location;
        foreach (Character recruit in app.Game.World.AllCharacters.Where(c => c.Player == null &&
            c.IsHuman && !c.IsTrader && !c.IsDead).Take(3).ToArray())
        {
            recruit.Hire(player, waivePayment: true);
            recruit.Position = location.EntryPoint;
        }
        player.SelectGroup(player.Party);
        // Populate one busy camp using existing actors, without increasing the world's population.
        var candidates = app.Game.World.AllCharacters.Where(c => !c.IsDead && !player.Party.Contains(c)).ToArray();
        var extras = candidates.Where(c => c.Class == CharClass.Dog).Take(3)
            .Concat(candidates.Where(c => c.Class == CharClass.Mutant).Take(3))
            .Concat(candidates.Where(c => c.IsTrader).Take(3))
            .Concat(candidates.Where(c => c.IsHuman && !c.IsTrader && c.Player == null).Take(3)).Distinct().ToArray();
        int index = 0;
        foreach (Character actor in player.Party.Concat(extras))
        {
            actor.Location = location;
            Vector2 origin = location.EntryPoint;
            Vector2 point = origin + new Vector2((index % 4) * 14, (index / 4) * 12);
            if (location.Map.Mask.IsWalkableMapPosition(point))
                actor.Position = point;
            else actor.Position = origin;
            actor.Health = 100;
            index++;
        }
        app.SetScene("LocationScene");
    }

    internal Sample Begin() => measuring
        ? new(Stopwatch.GetTimestamp(), CpuTime(16), GC.GetAllocatedBytesForCurrentThread()) : default;

    internal void End(string phase, Sample sample)
    {
        if (sample.Wall == 0 || !measuring) return;
        double cpu = CpuTime(16) - sample.Cpu;
        double wall = (Stopwatch.GetTimestamp() - sample.Wall) / (double)Stopwatch.Frequency;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - sample.Allocated;
        lock (sync)
        {
            phases[phase].Wall.Add(wall * 1000);
            phases[phase].Cpu.Add(cpu * 1000);
            phases[phase].Allocated += allocated;
        }
    }

    internal void CountFrame(int entities)
    {
        if (!measuring) return;
        entityTotal += entities;
        renderFrames++;
    }

    internal void AfterDraw(BurntimeGame host)
    {
        if (!screenshotSaved && initialized && clock.Elapsed.TotalSeconds >= 8 &&
            !host.IsLoading && !host.ResourceManager.IsLoading && host.LoadingStack == 0 && host.BlendOverlay.BlendState == 0)
        {
            int width = host.GraphicsDevice.PresentationParameters.BackBufferWidth;
            int height = host.GraphicsDevice.PresentationParameters.BackBufferHeight;
            Microsoft.Xna.Framework.Color[] pixels = new Microsoft.Xna.Framework.Color[width * height];
            host.GraphicsDevice.GetBackBufferData(pixels);
            for (int i = 0; i < pixels.Length; i++) pixels[i].A = 255;
            using Texture2D image = new(host.GraphicsDevice, width, height);
            image.SetData(pixels);
            using var file = System.IO.File.Create(Path.Combine(OutputDirectory, "scene.png"));
            image.SaveAsPng(file, width, height);
            screenshotSaved = true;
        }
        if (!complete || written) return;
        written = true;
        double duration = measurementEnd - measurementStart;
        using MemoryStream stream = new();
        using (Utf8JsonWriter json = new(stream, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteString("scenario", scenario);
            json.WriteNumber("targetFps", FramesPerSecond);
            json.WriteNumber("actualFps", renderFrames / duration);
            json.WriteNumber("durationSeconds", duration);
            json.WriteNumber("cpuSeconds", processCpuEnd - processCpuStart);
            json.WriteNumber("cpuPercentOfOneCore", (processCpuEnd - processCpuStart) / duration * 100);
            json.WriteNumber("allocatedBytesPerSecond", (allocatedEnd - allocatedStart) / duration);
            json.WriteStartArray("gcCollections");
            for (int i = 0; i < 3; i++) json.WriteNumberValue(collectionsEnd[i] - collectionsStart[i]);
            json.WriteEndArray();
            json.WriteNumber("partyMembers", party);
            json.WriteNumber("liveLocationActors", actors);
            json.WriteNumber("minLiveActors", minActors == int.MaxValue ? 0 : minActors);
            json.WriteNumber("maxLiveActors", maxActors);
            json.WriteNumber("averageRenderEntities", renderFrames == 0 ? 0 : entityTotal / renderFrames);
            json.WriteString("nativeResolution", host.Resolution.Native.ToString());
            json.WriteString("intermediateResolution", host.Resolution.BackBuffer.ToString());
            json.WriteString("filtering", host.OutputFiltering.ToString());
            json.WriteBoolean("music", music);
            json.WriteString("musicTrack", host.Music.Playing);
            json.WriteString("platform", RuntimeInformation.OSDescription);
            json.WriteString("runtime", RuntimeInformation.FrameworkDescription);
            json.WriteStartObject("phaseMetrics");
            lock (sync)
            {
                foreach (var (name, phase) in phases)
                {
                    json.WriteStartObject(name);
                    json.WriteNumber("count", phase.Cpu.Count);
                    json.WriteNumber("averageCpuMs", phase.Cpu.Count == 0 ? 0 : phase.Cpu.Average());
                    json.WriteNumber("p95CpuMs", Percentile(phase.Cpu, .95));
                    json.WriteNumber("averageWallMs", phase.Wall.Count == 0 ? 0 : phase.Wall.Average());
                    json.WriteNumber("p95WallMs", Percentile(phase.Wall, .95));
                    json.WriteNumber("cpuMsPerSecond", phase.Cpu.Sum() / duration);
                    json.WriteNumber("allocatedBytesPerFrame", phase.Cpu.Count == 0 ? 0 : phase.Allocated / phase.Cpu.Count);
                    json.WriteEndObject();
                }
            }
            json.WriteEndObject();
            json.WriteString("notes", "Normal two-thread loop. Within-turn gameplay; excludes strategic AI turns. Phase CPU uses thread clocks; render wall time can include driver waits. GPU execution requires an external trace. Nested render phases overlap render_submit.");
            json.WriteEndObject();
        }
        string report = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        System.IO.File.WriteAllText(Path.Combine(OutputDirectory, "report.json"), report);
        Console.WriteLine("PROFILE complete: " + report);
#if !IOS
        host.Exit();
#endif
    }

    static double Percentile(List<double> values, double p)
    {
        if (values.Count == 0) return 0;
        double[] ordered = values.Order().ToArray();
        return ordered[(int)((ordered.Length - 1) * p)];
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Timespec { public long Seconds, Nanoseconds; }
    [DllImport("libSystem.dylib", EntryPoint = "clock_gettime")]
    static extern int ClockGetTime(int clock, out Timespec time);
    static double CpuTime(int clock) => ClockGetTime(clock, out Timespec time) == 0
        ? time.Seconds + time.Nanoseconds / 1_000_000_000.0 : 0;
}
