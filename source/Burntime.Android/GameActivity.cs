using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Burntime.MonoGame;
using Microsoft.Xna.Framework;

namespace Burntime.Android;

[Activity(Label = "Burntime", MainLauncher = true, Exported = true,
    Icon = "@drawable/icon", ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
public sealed class GameActivity : AndroidGameActivity
{
    BurntimeGame? game;
    bool resumed;
    string? preparedContentPath;
    bool startupFailed;
    BackCallback? backCallback;
    readonly CancellationTokenSource loadingCancellation = new();

    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            backCallback = new BackCallback(HandleBack);
            OnBackInvokedDispatcher.RegisterOnBackInvokedCallback(0, backCallback);
        }
        SetContentView(new TextView(this) { Text = "Loading Burntime…", Gravity = GravityFlags.Center });
        try
        {
            // Android recreates this activity for screen-size changes. That rebuilds
            // scenes whose compact layout is selected when they are constructed.
            string contentPath = await Task.Run(() => BundledAssets.Prepare(this, loadingCancellation.Token));
            if (loadingCancellation.IsCancellationRequested || IsFinishing || IsDestroyed) return;
            preparedContentPath = contentPath;
            StartGameWhenResumed();
        }
        catch (System.OperationCanceledException) { }
        catch (Exception error)
        {
            ShowStartupFailure(error);
        }
    }

    void StartGameWhenResumed()
    {
        if (!resumed || game != null || preparedContentPath == null || startupFailed ||
            loadingCancellation.IsCancellationRequested || IsFinishing || IsDestroyed) return;
        try
        {
            game = new BurntimeGame(disableShaders: true)
            {
                AndroidContentPath = preparedContentPath,
                AndroidSmallDeviceUi = (Resources?.Configuration?.SmallestScreenWidthDp ?? 0) < 600
            };
            game.SetMobileActive(resumed);
            SetContentView(game.Services.GetService<View>()!);
            game.Run();
        }
        catch (Exception error) { ShowStartupFailure(error); }
    }

    void ShowStartupFailure(Exception error)
    {
        startupFailed = true;
        if (loadingCancellation.IsCancellationRequested || IsFinishing || IsDestroyed) return;
        global::Android.Util.Log.Error("Burntime", error.ToString());
        new AlertDialog.Builder(this)!
            .SetTitle("Could not start Burntime")!
            .SetMessage("The game resources could not be loaded. Please free some storage and reopen the app.")!
            .SetPositiveButton("Close", (_, _) => Finish())!
            .SetCancelable(false)!
            .Show();
    }


    protected override void OnPause()
    {
        resumed = false;
        game?.SetMobileActive(false);
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        resumed = true;
        game?.SetMobileActive(true);
        StartGameWhenResumed();
    }

    public override void OnLowMemory()
    {
        base.OnLowMemory();
        game?.QueueMemoryPressureCleanup();
    }

    // Pre-Android 13 fallback; newer versions use the system back dispatcher.
    public override void OnBackPressed() => HandleBack();

    void HandleBack()
    {
        if (game == null) Finish();
        else game.QueueAndroidBack();
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android33.0")]
    sealed class BackCallback(Action action) : Java.Lang.Object, global::Android.Window.IOnBackInvokedCallback
    {
        public void OnBackInvoked() => action();
    }

    protected override void OnDestroy()
    {
        loadingCancellation.Cancel();
        if (OperatingSystem.IsAndroidVersionAtLeast(33) && backCallback != null)
        {
            OnBackInvokedDispatcher.UnregisterOnBackInvokedCallback(backCallback);
            backCallback.Dispose();
            backCallback = null;
        }
        game?.Dispose();
        game = null;
        base.OnDestroy();
    }
}
