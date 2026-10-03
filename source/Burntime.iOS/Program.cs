using Burntime.MonoGame;
using Foundation;
using UIKit;

namespace Burntime.iOS;

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    BurntimeGame? game;

    static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        Console.WriteLine("Starting Burntime for iOS");
        game = new BurntimeGame(disableShaders: true);
        if (Environment.GetEnvironmentVariable("BURNTIME_IOS_SMOKE_TEST") == "1")
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BurntimeSmokeTest");
            Directory.CreateDirectory(directory);
            Burntime.Platform.IO.FileSystem.UserFolderOverride = Path.Combine(directory, "user");
            Directory.CreateDirectory(Burntime.Platform.IO.FileSystem.UserFolderOverride);
            System.IO.File.WriteAllText(Path.Combine(Burntime.Platform.IO.FileSystem.UserFolderOverride, "user.txt"),
                "newgfx=true\nlanguage=en\nmusic=off\nmap_music=none\ncontroller_glyphs=xbox\n");
            game.VisualTest = new IosSmokeTestRunner(directory);
        }
        game.Run();
        return true;
    }

    public override void OnResignActivation(UIApplication application) => game?.SetMobileActive(false);
    public override void OnActivated(UIApplication application) => game?.SetMobileActive(true);
    public override void ReceiveMemoryWarning(UIApplication application) => game?.QueueMemoryPressureCleanup();
}
