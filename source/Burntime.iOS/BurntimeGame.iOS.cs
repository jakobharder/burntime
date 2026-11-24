using Burntime.Framework;
using Burntime.Platform;
using UIKit;

namespace Burntime.MonoGame;

public partial class BurntimeGame
{
    nint settingsBackgroundTask = UIApplication.BackgroundTaskInvalid;
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
}
