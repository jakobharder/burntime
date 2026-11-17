using Burntime.Framework;
using Burntime.Platform;
using Microsoft.Xna.Framework.Input.Touch;
using System.Diagnostics;
using UIKit;

namespace Burntime.MonoGame;

public partial class BurntimeGame
{
    readonly TouchGestureRecognizer touchRecognizer = new();
    int? touchId;
    object? touchContext;
    volatile bool _mobileActive = true;
    int saveSettingsPending;
    readonly object settingsSaveSync = new();
    nint settingsBackgroundTask = UIApplication.BackgroundTaskInvalid;
    bool suppressTouchesUntilReleased;
    readonly object mobileFrameSync = new();
    int memoryPressurePending;
    IosMouseInput? mobileMouse;
    readonly System.Collections.Concurrent.ConcurrentQueue<IosMouseInput.Sample> pointerEvents = new();
    ulong pointerButtons;
    double pointerScroll;
    readonly bool traceInput = Environment.GetEnvironmentVariable("BURNTIME_IOS_INPUT_TRACE") == "1";

    void HandleMobileMouseInput()
    {
        mobileMouse ??= new IosMouseInput(Services.GetService<UIViewController>().View!, pointerEvents.Enqueue);
        mobileMouse.PollButtons();
        while (pointerEvents.TryDequeue(out var sample))
        {
            if (!_mobileActive || !IsActive || touchId.HasValue)
            {
                pointerButtons = 0;
                DeviceManager.IsRightDown = false;
                continue;
            }
            var point = mobileMouse.ToNormalized(sample.Position);
            var position = new Vector2((int)((double)point.X * Resolution.Game.x),
                (int)((double)point.Y * Resolution.Game.y));
            if (traceInput) Log.Info($"Pointer {sample.Type} buttons={sample.Buttons}");
            if (sample.Type is IosMouseInput.Kind.Cancel or IosMouseInput.Kind.Leave)
            {
                pointerButtons = 0;
                DeviceManager.IsRightDown = false;
                pointerScroll = 0;
                DeviceManager.MouseLeave();
                if (_burntimeApp.LastInputMode == InputMode.Mouse)
                    _burntimeApp.LastInputMode = InputMode.Touch;
                continue;
            }
            DeviceManager.MouseMove(position);
            if (sample.Type == IosMouseInput.Kind.Move)
                _burntimeApp.LastInputMode = InputMode.Mouse;
            if (sample.Type == IosMouseInput.Kind.Buttons)
            {
                ForwardPointerButton(1, MouseButton.Left);
                ForwardPointerButton(2, MouseButton.Right);
                pointerButtons = sample.Buttons;
                DeviceManager.IsRightDown = (pointerButtons & 2) != 0;
            }
            if (sample.Type == IosMouseInput.Kind.Scroll)
            {
                pointerScroll += sample.Scroll * 4;
                int delta = (int)pointerScroll;
                pointerScroll -= delta;
                DeviceManager.MouseWheel(position, delta);
            }

            void ForwardPointerButton(ulong mask, MouseButton button)
            {
                bool wasDown = (pointerButtons & mask) != 0;
                bool isDown = (sample.Buttons & mask) != 0;
                if (isDown && !wasDown) DeviceManager.MouseDown(position, button);
                if (!isDown && wasDown) DeviceManager.MouseClick(position, button);
            }
        }
    }

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
        if (!active) QueueMobileSettingsSave();
        _mobileActive = active;
        if (!active)
        {
            mobileMouse?.Reset();
            pointerEvents.Clear();
            pointerButtons = 0;
            pointerScroll = 0;
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
            finally { EndSettingsBackgroundTask(); }
        }
    }

    void QueueMobileSettingsSave()
    {
        lock (settingsSaveSync)
        {
            if (settingsBackgroundTask != UIApplication.BackgroundTaskInvalid)
                return;

            // Reserve execution time before publishing work to the game thread.
            settingsBackgroundTask = UIApplication.SharedApplication.BeginBackgroundTask(
                "Save Burntime settings", () =>
                {
                    lock (settingsSaveSync)
                    {
                        Interlocked.Exchange(ref saveSettingsPending, 0);
                        EndSettingsBackgroundTask();
                    }
                });
            if (settingsBackgroundTask != UIApplication.BackgroundTaskInvalid)
                Interlocked.Exchange(ref saveSettingsPending, 1);
        }
    }

    // Called under settingsSaveSync, after the settings stream has closed.
    void EndSettingsBackgroundTask()
    {
        if (settingsBackgroundTask == UIApplication.BackgroundTaskInvalid)
            return;
        nint task = settingsBackgroundTask;
        settingsBackgroundTask = UIApplication.BackgroundTaskInvalid;
        UIApplication.SharedApplication.EndBackgroundTask(task);
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
            if (traceInput) Log.Info("Direct touch Press");
            _burntimeApp.LastInputMode = InputMode.Touch;
            touchId = contact.Id;
            touchContext = _burntimeApp.SceneManager.TouchInputContext;
            // Keep a roughly ten-point movement tolerance on Retina displays.
            touchRecognizer.MovementTolerance = 10 * (float)UIKit.UIScreen.MainScreen.Scale;
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
