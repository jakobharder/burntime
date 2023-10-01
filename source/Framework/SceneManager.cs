﻿using System;
using System.Collections.Generic;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework.GUI;

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
            app.Engine.Blend = 1;
            app.Engine.WaitForBlend();
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
                app.Engine.IncreaseLoadingCount();
                Add(Scene, Activator.CreateInstance(sceneTypes[Scene], new object[] { app }) as Scene);
                app.Engine.DecreaseLoadingCount();
            }

            activeScene = scenes[Scene];
            app.Engine.CenterMouse();
            activeScene.ActivateScene(parameter);
            app.Engine.Blend = 0;
        }

        public void PreviousScene()
        {
            if (sceneQueue.Count > 0)
            {
                app.Engine.Blend = 1;
                app.Engine.WaitForBlend();
                activeScene.InactivateScene();
                activeScene = scenes[sceneQueue[sceneQueue.Count - 1]];
                app.Engine.CenterMouse();
                activeScene.ActivateScene();
                sceneQueue.RemoveAt(sceneQueue.Count - 1);
                app.Engine.Blend = 0;
            }
        }

        public void BlockBlendIn()
        {
            blockBlendIn++;

            if (blockBlendIn == 1)
                app.Engine.BlockBlend = true;
        }

        public void UnblockBlendIn()
        {
            blockBlendIn--;

            if (blockBlendIn == 0)
                app.Engine.BlockBlend = false;
        }

        public String LastScene
        {
            get { if (sceneQueue.Count == 0) return null; return sceneQueue[sceneQueue.Count - 1]; }
        }

        internal void Render(IRenderTarget Target)
        {
            if (activeScene != null)
                activeScene.Render(Target);
        }

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
                    handle.MouseClick(click.Position - parentPos, click.Button);
                }

                // handle keys
                Key[] keys = app.DeviceManager.Keyboard.Keys;
                foreach (Key key in keys)
                {
                    if (key.IsVirtual)
                        handle.VKeyPress(key.VKey);
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

        internal void PopModalStack()
        {
            modalStack.Pop();
        }

        internal void Reset()
        {
            modalStack.Clear();
            activeScene = null;
        }
    }
}
