using Burntime.Platform.Utils;

namespace Burntime.MonoGame;

public partial class BurntimeGame
{
#if ANDROID
    readonly int[] safeAreaViewLocation = new int[2];
    readonly int[] safeAreaRootLocation = new int[2];
#endif

    void UpdateMobileSafeArea(bool resize = true)
    {
        SafeAreaInsets insets = default;
#if IOS
        var view = Services.GetService<UIKit.UIViewController>()?.View;
        if (view?.Window == null || view.Bounds.Width <= 0 || view.Bounds.Height <= 0) return;
        var safe = view.SafeAreaInsets;
        insets = new(ToPixels((double)safe.Left, (double)view.Bounds.Width, Resolution.Native.x),
            ToPixels((double)safe.Top, (double)view.Bounds.Height, Resolution.Native.y),
            ToPixels((double)safe.Right, (double)view.Bounds.Width, Resolution.Native.x),
            ToPixels((double)safe.Bottom, (double)view.Bounds.Height, Resolution.Native.y));
#elif ANDROID
        var view = Services.GetService<global::Android.Views.View>();
        var root = view?.RootView;
        var windowInsets = root?.RootWindowInsets;
        if (view == null || root == null || windowInsets == null || view.Width <= 0 || view.Height <= 0) return;
        int left, top, right, bottom;
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var safe = windowInsets.GetInsetsIgnoringVisibility(
                global::Android.Views.WindowInsets.Type.SystemBars() |
                global::Android.Views.WindowInsets.Type.DisplayCutout());
            left = safe.Left; top = safe.Top; right = safe.Right; bottom = safe.Bottom;
        }
        else
        {
            left = windowInsets.SystemWindowInsetLeft;
            top = windowInsets.SystemWindowInsetTop;
            right = windowInsets.SystemWindowInsetRight;
            bottom = windowInsets.SystemWindowInsetBottom;
            if (OperatingSystem.IsAndroidVersionAtLeast(28) && windowInsets.DisplayCutout is { } cutout)
            {
                left = System.Math.Max(left, cutout.SafeInsetLeft);
                top = System.Math.Max(top, cutout.SafeInsetTop);
                right = System.Math.Max(right, cutout.SafeInsetRight);
                bottom = System.Math.Max(bottom, cutout.SafeInsetBottom);
            }
        }
        // Some Android versions already fit the game view inside system bars.
        // Intersect with that view so these margins are not applied twice.
        view.GetLocationOnScreen(safeAreaViewLocation);
        root.GetLocationOnScreen(safeAreaRootLocation);
        int x = safeAreaViewLocation[0] - safeAreaRootLocation[0];
        int y = safeAreaViewLocation[1] - safeAreaRootLocation[1];
        insets = new(ToPixels(left - x, view.Width, Resolution.Native.x),
            ToPixels(top - y, view.Height, Resolution.Native.y),
            ToPixels(x + view.Width - (root.Width - right), view.Width, Resolution.Native.x),
            ToPixels(y + view.Height - (root.Height - bottom), view.Height, Resolution.Native.y));
#endif
        if (insets == Resolution.NativeSafeAreaInsets) return;
        lock (mobileFrameSync)
        {
            Resolution.NativeSafeAreaInsets = insets;
            if (resize && _initialized) _burntimeApp.SceneManager.ResizeScene();
        }
    }

    static int ToPixels(double inset, double extent, int pixels) => extent <= 0 ? 0 :
        (int)System.Math.Ceiling(System.Math.Max(0, inset) * pixels / extent);
}
