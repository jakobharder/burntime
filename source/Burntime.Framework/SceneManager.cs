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
            app.Engine.BlendOverlay.FadeOut(wait: true);
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
            activeScene.ActivateScene(parameter);
            app.Engine.CenterMouse();
            app.Engine.IsLoading = true;
            app.Engine.BlendOverlay.FadeIn();
        }

        public void PreviousScene()
        {
            if (sceneQueue.Count > 0)
            {
                app.Engine.BlendOverlay.FadeOut(wait: true);
                activeScene.InactivateScene();
                activeScene = scenes[sceneQueue[sceneQueue.Count - 1]];
                app.Engine.CenterMouse();
                activeScene.ActivateScene();
                sceneQueue.RemoveAt(sceneQueue.Count - 1);
                app.Engine.BlendOverlay.FadeIn();
            }
        }

        public void BlockBlendIn()
        {
            blockBlendIn++;

            if (blockBlendIn == 1)
                app.Engine.BlendOverlay.Block = true;
        }

        public void UnblockBlendIn()
        {
            blockBlendIn--;

            if (blockBlendIn == 0)
                app.Engine.BlendOverlay.Block = false;
        }

        public string? LastScene => sceneQueue.LastOrDefault();

        internal void Render(RenderTarget Target) => activeScene?.Render(Target);

        internal void Process(float Elapsed)
        {
            Window handle = null;

            if (modalStack.Count > 0)
                handle = modalStack.Peek();
            else
                handle = activeScene;

            Vector2 parentPos = handle.PositionOnScreen - handle.Position;

            if (handle != null)
            {
                // move mouse
                handle.MouseMove(app.DeviceManager.Mouse.Position - parentPos);

                // handle clicks
                foreach (MouseClickInfo click in app.DeviceManager.Mouse.Clicks)
                {
                    if (click.Down)
                        handle.MouseDown(click.Position - parentPos, click.Button);
                    else if (!click.Down)
                        handle.MouseClick(click.Position - parentPos, click.Button);
                }

                // handle keys
                Key[] keys = app.DeviceManager.Keyboard.Keys;
                foreach (Key key in keys)
                {
                    if (key.IsVirtual && key.VirtualKey == SystemKey.F8)
                        app.IsNewGfx = !app.IsNewGfx;
                    else if (key.IsVirtual)
                        handle.VKeyPress(key.VirtualKey);
                    else
                        handle.KeyPress(key.Character);
                }

                handle.Update(Elapsed);
            }
        }

        internal void PushModalStack(Window window)
        {
            Window handle = null;

            if (modalStack.Count > 0)
                handle = modalStack.Peek();
            else
                handle = activeScene;

            handle.ModalLeave();

            modalStack.Push(window);
        }

        internal void PopModalStack() => modalStack.Pop();

        internal void Reset()
        {
            modalStack.Clear();
            activeScene = null;
        }

        public void ResizeScene()
        {
            activeScene?.OnResizeScreen();
        }
    }
}
