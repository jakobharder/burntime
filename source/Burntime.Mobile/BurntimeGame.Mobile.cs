using Burntime.Framework;
using Burntime.Platform;
using Microsoft.Xna.Framework.Input.Touch;
using System.Diagnostics;

namespace Burntime.MonoGame;

public partial class BurntimeGame
{
    readonly TouchGestureRecognizer touchRecognizer = new();
    int? touchId;
    object? touchContext;
    volatile bool _mobileActive = true;
    int saveSettingsPending;
    readonly object settingsSaveSync = new();
    bool suppressTouchesUntilReleased;
    readonly object mobileFrameSync = new();
    int memoryPressurePending;

    public void QueueMemoryPressureCleanup() => Interlocked.Exchange(ref memoryPressurePending, 1);

    void HandleMemoryPressure()
    {
        // Texture disposal belongs on the graphics thread while the app is active.
        if (!_mobileActive || !IsActive ||
            Interlocked.Exchange(ref memoryPressurePending, 0) == 0)
            return;

        lock (mobileFrameSync)
        {
            ResourceManager.SetSuspended(true);
            try { ResourceManager.ReleaseCachedResources(); }
            finally { ResourceManager.SetSuspended(false); }
        }
    }

    public void SetMobileActive(bool active)
    {
        if (!active && _burntimeApp != null)
        {
#if IOS
            QueueMobileSettingsSave();
#else
            // Android may stop scheduling the game thread as soon as OnPause returns.
            // Complete the settings write before handing control back to the OS.
            lock (mobileFrameSync)
            {
                Interlocked.Exchange(ref saveSettingsPending, 1);
                PersistMobileSettings();
            }
#endif
        }
        _mobileActive = active;
        if (!active)
        {
#if IOS
            mobileMouse?.Reset();
            pointerEvents.Clear();
            pointerButtons = 0;
            pointerScroll = 0;
#endif
            if (DeviceManager != null) DeviceManager.IsRightDown = false;
            if (_burntimeApp?.LastInputMode == InputMode.Mouse)
                _burntimeApp.LastInputMode = InputMode.Touch;
        }
        ResourceManager?.SetSuspended(!active);
        CancelTouch();
        _burntimeApp?.SceneManager?.ClearTouchGestures();
        Music.SetSuspended(!active);
    }

    void PersistMobileSettings()
    {
        lock (settingsSaveSync)
        {
            if (Interlocked.Exchange(ref saveSettingsPending, 0) == 0) return;
            try { _burntimeApp.SaveUserSettings(); }
            catch (Exception error) { Log.Warning("Could not save settings: " + error.Message); }
#if IOS
            finally { EndSettingsBackgroundTask(); }
#endif
        }
    }

    void CancelTouch()
    {
        if (touchId.HasValue)
            _burntimeApp?.SceneManager?.ClearTouchGestures();
        touchRecognizer.Cancel();
        touchId = null;
        suppressTouchesUntilReleased = true;
    }

    void HandleTouchInput()
    {
        var contacts = TouchPanel.GetState();
        if (!_mobileActive || !IsActive)
        {
            CancelTouch();
            return;
        }
        if (contacts.Count == 0)
        {
            if (touchId.HasValue) CancelTouch();
            touchRecognizer.Cancel();
            touchId = null;
            suppressTouchesUntilReleased = false;
            return;
        }
        if (contacts.Count > 1 || (touchId.HasValue &&
            !ReferenceEquals(touchContext, _burntimeApp.SceneManager.TouchInputContext)))
            CancelTouch();
        if (suppressTouchesUntilReleased) return;

        var contact = contacts[0];
        var nativePosition = new Vector2((int)contact.Position.X, (int)contact.Position.Y);
        double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        TouchGesture? began = null;
        if (contact.State == TouchLocationState.Pressed)
        {
#if IOS
            if (traceInput) Log.Info("Direct touch Press");
#endif
            _burntimeApp.LastInputMode = InputMode.Touch;
            touchId = contact.Id;
            touchContext = _burntimeApp.SceneManager.TouchInputContext;
            // Keep movement tolerance in points/dp rather than physical pixels.
#if IOS
            touchRecognizer.MovementTolerance = 10 * (float)UIKit.UIScreen.MainScreen.Scale;
#else
            touchRecognizer.MovementTolerance = 10 * (global::Android.App.Application.Context.Resources?.DisplayMetrics?.Density ?? 1);
#endif
            began = touchRecognizer.Begin(nativePosition, now);
        }
        if (touchId != contact.Id) return;
        TouchGesture? gesture = contact.State switch
        {
            TouchLocationState.Pressed => began,
            TouchLocationState.Moved => touchRecognizer.Move(nativePosition, now),
            TouchLocationState.Released => touchRecognizer.End(nativePosition, now),
            TouchLocationState.Invalid => new TouchGesture(TouchGestureKind.Cancel, nativePosition, nativePosition, default),
            _ => null
        };
        if (gesture is { } value)
        {
            var origin = ToGamePosition(value.Origin);
            var position = ToGamePosition(value.Position);
            // Transform endpoints, not delta alone, to retain sub-pixel drag movement.
            var delta = position - ToGamePosition(value.Position - value.Delta);
            var velocity = ToGameVelocity(value.Velocity);
            _burntimeApp.SceneManager.QueueTouchGesture(
                new(value.Kind, origin, position, delta, now, velocity), touchContext);
        }
        if (contact.State is TouchLocationState.Released or TouchLocationState.Invalid)
        {
            touchRecognizer.Cancel();
            touchId = null;
        }
    }

    Vector2 ToGamePosition(Vector2 position) => new(
        position.x * Resolution.Game.x / System.Math.Max(1, Resolution.Native.x),
        position.y * Resolution.Game.y / System.Math.Max(1, Resolution.Native.y));

    Vector2f ToGameVelocity(Vector2f velocity) => new(
        velocity.x * Resolution.Game.x / System.Math.Max(1, Resolution.Native.x),
        velocity.y * Resolution.Game.y / System.Math.Max(1, Resolution.Native.y));
}
