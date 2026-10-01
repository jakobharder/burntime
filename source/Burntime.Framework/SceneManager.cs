using System;
using System.Collections.Generic;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework.GUI;
using System.Linq;

namespace Burntime.Framework
{
    public class SceneManager
    {
        Dictionary<String, Scene> scenes = new Dictionary<string, Scene>();
        List<String> sceneQueue = new List<string>();
        Scene activeScene = null;
        Module app;
        Stack<Window> modalStack = new Stack<Window>();
        int blockBlendIn;
        bool blendMusicThroughBridge;

        Dictionary<String, Type> sceneTypes = new Dictionary<string, Type>();

        public SceneManager(Module App)
        {
            app = App;

            foreach (Type t in App.Scenes)
                sceneTypes.Add(t.Name, t);
        }

        public void Add(String Name, Scene Scene)
        {
            scenes.Add(Name, Scene);
            if (activeScene == null)
            {
                activeScene = Scene;
                activeScene.ActivateScene(null);
            }
        }

        public void SetScene(String Scene)
        {
            SetScene(Scene, false, null);
        }

        public void SetScene(String scene, object parameter)
        {
            SetScene(scene, false, parameter);
        }

        public void SetScene(String scene, bool doNotQueue)
        {
            SetScene(scene, doNotQueue, null);
        }

        public void SetScene(String Scene, bool DoNotQueue, object parameter)
        {
            CancelTouchPress();
            touchInputContext = new object();
            bool sourceIsTransitionBridge = activeScene is ISceneTransitionBridge;
            bool targetIsTransitionBridge = typeof(ISceneTransitionBridge)
                .IsAssignableFrom(sceneTypes[Scene]);
            var musicTransition = ConfigureMusicTransition(sceneTypes[Scene]);
            app.Engine.MusicBlend = blendMusicThroughBridge;
            app.Engine.BlendOverlay.FadeOut(wait: true);
            if (musicTransition.discardRememberedSong)
                app.Engine.Music.DiscardRememberedSong();
            if (musicTransition.rememberSong)
                app.Engine.Music.RememberCurrentSong();
            if (musicTransition.rememberPlaylist)
                app.Engine.Music.RememberPlaylistSong();
            if (activeScene != null)
            {
                if (!DoNotQueue)
                {
                    sceneQueue.Add(activeScene.GetType().Name);
                    if (sceneQueue.Count > 10)
                        sceneQueue.RemoveAt(0);
                }

                activeScene.InactivateScene();
            }

            if (!scenes.ContainsKey(Scene))
            {
                //app.ResourceManager.LoadingCounter.IncreaseLoadingCount();
                Add(Scene, Activator.CreateInstance(sceneTypes[Scene], new object[] { app }) as Scene);
                //app.ResourceManager.LoadingCounter.DecreaseLoadingCount();
            }

            activeScene = scenes[Scene];
            if (musicTransition.targetIsMap)
                activeScene.KeepMusic = musicTransition.keepMusic;
            activeScene.ActivateScene(parameter);
            if (musicTransition.resumeRememberedSong)
                app.Engine.Music.ResumeRememberedSong();
            if (musicTransition.playPlaylist)
                app.Engine.Music.PlayPlaylist();
            else
                app.Engine.Music.SetPlaylistContinuation(
                    musicTransition.continuePlaylist);
            if (targetIsTransitionBridge)
                app.Engine.MusicSilenced = true;
            else if (sourceIsTransitionBridge)
                app.Engine.MusicSilenced = false;
            app.Engine.CenterMouse();
            app.Engine.IsLoading = true;
            app.Engine.BlendOverlay.FadeIn();
            touchInputContext = new object();
            if (sourceIsTransitionBridge && !targetIsTransitionBridge)
                blendMusicThroughBridge = false;
        }

        public void PreviousScene()
        {
            CancelTouchPress();
            touchInputContext = new object();
            if (sceneQueue.Count > 0)
            {
                Scene previousScene = scenes[sceneQueue[sceneQueue.Count - 1]];
                bool sourceIsTransitionBridge = activeScene is ISceneTransitionBridge;
                bool targetIsTransitionBridge = previousScene is ISceneTransitionBridge;
                var musicTransition = ConfigureMusicTransition(previousScene.GetType());
                app.Engine.MusicBlend = blendMusicThroughBridge;
                app.Engine.BlendOverlay.FadeOut(wait: true);
                if (musicTransition.discardRememberedSong)
                    app.Engine.Music.DiscardRememberedSong();
                if (musicTransition.rememberSong)
                    app.Engine.Music.RememberCurrentSong();
                if (musicTransition.rememberPlaylist)
                    app.Engine.Music.RememberPlaylistSong();
                activeScene.InactivateScene();
                activeScene = previousScene;
                app.Engine.CenterMouse();
                if (musicTransition.targetIsMap)
                    activeScene.KeepMusic = musicTransition.keepMusic;
                activeScene.ActivateScene();
                if (musicTransition.resumeRememberedSong)
                    app.Engine.Music.ResumeRememberedSong();
                if (musicTransition.playPlaylist)
                    app.Engine.Music.PlayPlaylist();
                else
                    app.Engine.Music.SetPlaylistContinuation(
                        musicTransition.continuePlaylist);
                if (targetIsTransitionBridge)
                    app.Engine.MusicSilenced = true;
                else if (sourceIsTransitionBridge)
                    app.Engine.MusicSilenced = false;
                sceneQueue.RemoveAt(sceneQueue.Count - 1);
                app.Engine.BlendOverlay.FadeIn();
                if (sourceIsTransitionBridge && !targetIsTransitionBridge)
                    blendMusicThroughBridge = false;
            }
        }

        (bool targetIsMap, bool keepMusic, bool playPlaylist,
            bool rememberPlaylist, bool continuePlaylist, bool rememberSong,
            bool resumeRememberedSong, bool discardRememberedSong)
            ConfigureMusicTransition(Type nextSceneType)
        {
            bool sourceIsMap = activeScene is IMapMusicContinuationScene;
            bool targetIsMap = typeof(IMapMusicContinuationScene)
                .IsAssignableFrom(nextSceneType);
            bool sourceIsMapNavigation = activeScene is IMapNavigationScene;
            bool targetIsMapNavigation = typeof(IMapNavigationScene)
                .IsAssignableFrom(nextSceneType);
            bool sourceIsMusicInterruption = activeScene is IMapMusicInterruptionScene;
            bool targetIsMusicInterruption = typeof(IMapMusicInterruptionScene)
                .IsAssignableFrom(nextSceneType);
            bool keepMusic = false;
            bool playPlaylist = false;
            bool rememberPlaylist = false;
            bool continuePlaylist = targetIsMap &&
                app.Engine.MapMusicMode != MapMusicMode.None;
            bool rememberSong = app.Engine.MapMusicMode == MapMusicMode.Keep &&
                sourceIsMap && targetIsMusicInterruption;
            bool resumeRememberedSong = app.Engine.MapMusicMode == MapMusicMode.Keep &&
                sourceIsMusicInterruption && targetIsMap;
            bool discardRememberedSong =
                (targetIsMusicInterruption && !rememberSong) ||
                (sourceIsMusicInterruption && !resumeRememberedSong);

            if (!targetIsMap)
                app.Engine.Music.SetPlaylistContinuation(false);

            if (app.Engine.MapMusicMode != MapMusicMode.List)
                app.Engine.Music.DiscardRememberedPlaylistSong();

            if (app.Engine.MapMusicMode == MapMusicMode.List &&
                sourceIsMap && !targetIsMap)
            {
                rememberPlaylist = true;
            }

            if (targetIsMap)
            {
                if (app.Engine.MapMusicMode == MapMusicMode.Keep)
                {
                    keepMusic = !sourceIsMusicInterruption;
                }
                else if (app.Engine.MapMusicMode == MapMusicMode.List)
                {
                    // Direct navigation between the world and location maps must
                    // preserve both an active track and a track still being queued.
                    keepMusic = sourceIsMapNavigation && targetIsMapNavigation ||
                        sourceIsMap && app.Engine.Music.IsPlayingFromPlaylist;
                    playPlaylist = !keepMusic;
                }
            }

            return (targetIsMap, keepMusic, playPlaylist, rememberPlaylist,
                continuePlaylist, rememberSong, resumeRememberedSong,
                discardRememberedSong);
        }

        public void BlockBlendIn()
        {
            blockBlendIn++;

            if (blockBlendIn == 1)
                app.Engine.BlendOverlay.Block = true;
        }

        public void BlendMusicThroughNextBridge() => blendMusicThroughBridge = true;

        public void UnblockBlendIn()
        {
            blockBlendIn--;

            if (blockBlendIn == 0)
                app.Engine.BlendOverlay.Block = false;
        }

        public string? LastScene => sceneQueue.LastOrDefault();
        public bool UseCardinalGamepadMovement => activeScene?.UseCardinalGamepadMovement ?? false;
        public bool UseDiagonalGamepadNavigation => activeScene?.UseDiagonalGamepadNavigation ?? false;
        public bool PreserveMouseModeForDirectionalInput =>
            modalStack.Count > 0 && modalStack.Peek().PreserveMouseModeForDirectionalInput;
        internal Window? InputWindow => modalStack.Count > 0
            ? modalStack.Peek()
            : activeScene;

        volatile object touchInputContext = new();
        public object TouchInputContext => touchInputContext;
        readonly System.Collections.Concurrent.ConcurrentQueue<(TouchGesture Gesture, object? Context)> touchGestures = new();
        Window? touchTarget;
        Vector2 touchPosition;
        bool touchPressActive;

        public void QueueTouchGesture(TouchGesture gesture, object? context) =>
            touchGestures.Enqueue((gesture, context));

        public void ClearTouchGestures()
        {
            CancelTouchPress();
            touchGestures.Clear();
        }

        void CancelTouchPress()
        {
            if (!touchPressActive)
                return;

            Window? handle = InputWindow;
            if (handle != null)
            {
                Vector2 parentPos = handle.PositionOnScreen - handle.Position;
                handle.TouchRelease(touchPosition - parentPos, cancelled: true);
            }
            touchTarget = null;
            touchPressActive = false;
        }

        internal void Render(RenderTarget Target) => activeScene?.Render(Target);

        internal void Process(float Elapsed)
        {
            Window handle = InputWindow;
            if (handle != null)
            {
                Vector2 parentPos = handle.PositionOnScreen - handle.Position;
                // move mouse
                handle.MouseMove(app.DeviceManager.Mouse.Position - parentPos);

                while (touchGestures.TryDequeue(out var touch))
                {
                    handle = InputWindow;
                    if (handle == null || !ReferenceEquals(touch.Context, TouchInputContext))
                        continue;
                    parentPos = handle.PositionOnScreen - handle.Position;
                    app.LastInputMode = InputMode.Touch;
                    var gesture = touch.Gesture;
                    if (gesture.Kind == TouchGestureKind.Press)
                    {
                        CancelTouchPress();
                        touchPosition = gesture.Origin;
                        touchTarget = handle.FindTouchTarget(gesture.Origin);
                        touchPressActive = true;
                        handle.TouchPress(gesture.Origin - parentPos);
                        // Expanded touch targets can begin outside their visual bounds.
                        // Explicitly notify the captured target as well; touch press handlers
                        // are state setters and therefore safe when the normal tree dispatch
                        // already reached the same control.
                        if (touchTarget != null && !ReferenceEquals(touchTarget, handle))
                            touchTarget.OnTouchPress(gesture.Origin - touchTarget.PositionOnScreen);
                        continue;
                    }
                    if (gesture.Kind == TouchGestureKind.Tap)
                    {
                        touchPosition = gesture.Position;
                        Window? target = touchTarget ?? handle.FindTouchTarget(gesture.Position);
                        bool targetInside = target != null && target.IsVisible &&
                            TouchHitTest.Expand(TouchHitTest.Bounds(target),
                                target.MinimumTouchTargetSize).PointInside(gesture.Position);
                        if (touchPressActive)
                            handle.TouchRelease(gesture.Position - parentPos,
                                cancelled: target != null && !targetInside);
                        touchTarget = null;
                        touchPressActive = false;
                        if (handle.OnTouchTap(gesture.Position - handle.PositionOnScreen))
                            continue;
                        if (target != null)
                        {
                            if (!targetInside)
                                continue;

                            // Both phases go to the same target, even if expanded regions overlap.
                            Vector2 local = gesture.Position - target.PositionOnScreen;
                            if (target.OnTouchTap(local))
                                continue;
                            for (Window? ancestor = target; ancestor != null; ancestor = ancestor.Parent)
                            {
                                bool consumed = ancestor.OnMouseDown(gesture.Position - ancestor.PositionOnScreen, MouseButton.Left);
                                if (consumed || ReferenceEquals(ancestor, handle) || !ReferenceEquals(touch.Context, TouchInputContext)) break;
                            }
                            if (ReferenceEquals(touch.Context, TouchInputContext) && target.IsVisible)
                                target.OnMouseClick(local, MouseButton.Left);
                            continue;
                        }
                    }
                    if (gesture.Kind == TouchGestureKind.Cancel)
                    {
                        touchPosition = gesture.Position;
                        handle.OnTouchHoldEnd(gesture.Position - handle.PositionOnScreen, cancelled: true);
                        if (touchPressActive)
                            handle.TouchRelease(gesture.Position - parentPos, cancelled: true);
                        touchTarget = null;
                        touchPressActive = false;
                        continue;
                    }
                    if (gesture.Kind is TouchGestureKind.HoldMove or TouchGestureKind.HoldEnd)
                    {
                        if (gesture.Kind == TouchGestureKind.HoldMove)
                            handle.OnTouchHoldMove(gesture.Position - handle.PositionOnScreen);
                        else
                            handle.OnTouchHoldEnd(gesture.Position - handle.PositionOnScreen, cancelled: false);
                        if (gesture.Kind == TouchGestureKind.HoldEnd)
                        {
                            touchPosition = gesture.Position;
                            if (touchPressActive)
                                handle.TouchRelease(gesture.Position - parentPos, cancelled: true);
                            touchTarget = null;
                            touchPressActive = false;
                        }
                        continue;
                    }
                    if (gesture.Kind == TouchGestureKind.LongPress)
                    {
                        touchPosition = gesture.Position;
                        // Keep tooltip holds pressed until the finger is released.
                        // These buttons have no secondary action to dispatch.
                        if (touchTarget is Button)
                            continue;
                        if (touchPressActive)
                            handle.TouchRelease(gesture.Position - parentPos, cancelled: true);
                        touchTarget = null;
                        touchPressActive = false;
                        if (handle.TouchLongPress(gesture.Origin - parentPos)) continue;
                    }
                    if (gesture.Kind == TouchGestureKind.Drag)
                    {
                        touchPosition = gesture.Position;
                        if (touchPressActive)
                            handle.TouchRelease(gesture.Position - parentPos, cancelled: true);
                        touchTarget = null;
                        touchPressActive = false;
                        handle.TouchDrag(gesture.Origin - parentPos, gesture.Delta);
                    }
                    else if (gesture.Kind == TouchGestureKind.DragEnd)
                    {
                        touchPosition = gesture.Position;
                        if (touchPressActive)
                            handle.TouchRelease(gesture.Position - parentPos, cancelled: true);
                        touchTarget = null;
                        touchPressActive = false;
                        handle.TouchDragEnd(gesture.Origin - parentPos, gesture.Delta,
                            gesture.Velocity);
                    }
                    else
                    {
                        var button = gesture.Kind == TouchGestureKind.LongPress ? MouseButton.Right : MouseButton.Left;
                        app.DeviceManager.MouseMove(gesture.Position);
                        handle.MouseDown(gesture.Position - parentPos, button);
                        if (ReferenceEquals(handle, InputWindow))
                            handle.MouseClick(gesture.Position - parentPos, button);
                    }
                }

                // handle clicks
                var clicks = app.DeviceManager.Mouse.ConsumeClicks();
                foreach (MouseClickInfo click in clicks)
                {
                    if (click.Down)
                        handle.MouseDown(click.Position - parentPos, click.Button);
                    else if (!click.Down)
                        handle.MouseClick(click.Position - parentPos, click.Button);
                }

                foreach (MouseWheelInfo wheel in app.DeviceManager.Mouse.WheelEvents)
                    handle.MouseWheel(wheel.Position - parentPos, wheel.Delta);

                foreach (InputAction rawAction in app.InputManager.ConsumeActions())
                {
                    InputAction action = handle.ResolveInputAction(rawAction);
                    if (action != InputAction.None)
                        handle.InputAction(action);
                }

                foreach (InputAction rawAction in app.InputManager.ActionsDown)
                {
                    InputAction action = handle.ResolveInputAction(rawAction);
                    if (action != InputAction.None)
                        handle.HeldInputAction(action, Elapsed);
                }

                foreach (GamepadControl control in app.DeviceManager.ConsumeGamepadControls())
                    handle.InputGamepadControl(control);

                foreach (GamepadControl control in app.DeviceManager.GamepadControlsDown)
                    handle.HeldGamepadControl(control, Elapsed);

                // handle keys
                Key[] keys = app.DeviceManager.Keyboard.Keys;
                foreach (Key key in keys)
                {
                    if (key.IsVirtual && key.VirtualKey == SystemKey.F8)
                        app.IsNewGfx = !app.IsNewGfx;
                    else if (!key.IsVirtual && handle.AcceptsTextInput)
                        handle.KeyPress(key.Character);
                    else if (handle.TryGetInputAction(key, out InputAction action) && handle.InputAction(action))
                        continue;
                    else if (key.IsVirtual)
                        handle.VKeyPress(key.VirtualKey, key.Modifier);
                    else
                        handle.KeyPress(key.Character);
                }

                handle.Update(Elapsed);
            }
        }

        internal void PushModalStack(Window window)
        {
            CancelTouchPress();
            if (!window.ContinuesTouchHold)
                touchInputContext = new object();
            Window handle = null;

            if (modalStack.Count > 0)
                handle = modalStack.Peek();
            else
                handle = activeScene;

            handle.ModalLeave();

            modalStack.Push(window);
        }

        internal void PopModalStack()
        {
            CancelTouchPress();
            touchInputContext = new object();
            modalStack.Pop();
        }

        internal void Reset()
        {
            CancelTouchPress();
            touchInputContext = new object();
            modalStack.Clear();
            activeScene = null;
        }

        public void ResizeScene(bool reload = false)
        {
            activeScene?.OnResizeScreen(reload);
        }
    }
}
