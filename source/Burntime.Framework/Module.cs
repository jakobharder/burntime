﻿using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Platform.Graphics;
using Burntime.Platform.IO;
using Burntime.Framework.GUI;
using Burntime.Framework.States;

namespace Burntime.Framework
{
    public class Module
    {
        List<Type> scenes;
        public List<Type> Scenes
        {
            get { return scenes; }
            set { scenes = value; }
        }

        List<IDataProcessor> dataProcessors = new List<IDataProcessor>();

        protected void FindClassesFromAssembly(System.Reflection.Assembly asm)
        {
            Scenes = new();
            List<Type> dataprocessor = new();

            try
            {
                foreach (Type t in asm.GetTypes())
                {
                    if (t.IsSubclassOf(typeof(Scene)))
                        Scenes.Add(t);

                    if (typeof(IDataProcessor).IsAssignableFrom(t))
                        dataprocessor.Add(t);
                }

                foreach (Type t in Scenes)
                    Log.Info("   Load scene class: " + t.Name);
                foreach (Type t in dataprocessor)
                {
                    Log.Info("   Load data processor: " + t.Name);
                    AddProcessor((IDataProcessor)Activator.CreateInstance(t));
                }

            }
            catch (Exception e)
            {
                // marker for debugging purposes
                throw e;
            }
        }

        public void Initialize(IResourceManager resourceManager)
        {
            ResourceManager = resourceManager;

            // initialize all data processors
            foreach (IDataProcessor processor in dataProcessors)
            {
                foreach (string format in processor.Names)
                {
                    Log.Info("   Add data processor for '" + format + "': " + processor.GetType().Name);
                    resourceManager.AddDataProcessor(format, processor.GetType());
                }
            }

            dataProcessors = null;

            // call custom initialize routines
            OnInitialize();
        }

        public void Run()
        {
            instance = this;
            OnRun();
        }

        protected virtual void OnInitialize() { }
        protected virtual void OnRun() { }
        protected virtual void OnClose() { }
        protected virtual void OnProcess(float elapsed) { }

        public virtual void AddProcessor(String Extension, ISpriteProcessor Processor)
        {
            Log.Info("   Add resource processor for '" + Extension + "': " + Processor.GetType().Name);
            ResourceManager.AddSpriteProcessor(Extension, Processor);
        }

        public virtual void AddProcessor(String Extension, IFontProcessor Processor)
        {
            Log.Info("   Add resource processor for '" + Extension + "': " + Processor.GetType().Name);
            ResourceManager.AddFontProcessor(Extension, Processor);
        }

        public void AddProcessor(IDataProcessor processor)
        {
            dataProcessors.Add(processor);
        }

        public virtual String Title { get { return ""; } }
        //public virtual Vector2[] Resolutions { get { return new Vector2[] { new Vector2(640, 480) }; } }
        public virtual int MaxVerticalResolution => 480;
        public virtual Vector2 MinResolution { get; } = new Vector2(320, 200);
        public virtual Vector2 MaxResolution { get; } = new Vector2(320, 200);
        public virtual Vector2f RatioCorrection => Vector2f.One;
        public virtual System.Drawing.Icon? Icon => null;

        protected static Module instance;
        public static Module Instance
        {
            get { return instance; }
        }

        public ISprite MouseImage = null;
        public bool RenderMouse = true;
        public Nullable<Rect> MouseBoundings
        {
            get { return DeviceManager.Mouse.Boundings; }
            set { DeviceManager.Mouse.Boundings = value; }
        }

        public IEngine Engine;
        public SceneManager SceneManager;
        public IResourceManager ResourceManager { get; private set; }
        public DeviceManager DeviceManager;
        public ConfigFile Settings;

        public ConfigFile? UserSettings { get; protected set; }

        public WorldState GameState
        {
            get
            {
                return ActiveClient.StateContainer.Root;
            }
        }

        public Network.IGameServer GameServer = null;
        public Network.GameServer Server = null;

        public List<Network.GameClient> Clients = new List<Burntime.Framework.Network.GameClient>();
        public Network.GameClient ActiveClient = Network.GameClient.NoClient;

        bool running = false;
        public virtual void Start()
        {
        }

        public void StopGame()
        {
            if (Server != null)
            {
                Server.Stop();
                Server = null;
            }

            Clients.Clear();
            ActiveClient = null;
        }

        public void Render(RenderTarget target)
        {
            target.Layer = 0;

            SceneManager.Render(target);

            if (MouseImage != null && RenderMouse)
            {
                target.Layer = 255;
                target.DrawSprite(DeviceManager.Mouse.Position, MouseImage);
            }
        }

        public void Process(float elapsed)
        {
            if (!running)
            {
                running = true;
                //Run();
                Start();
            }
            else
            {
                // process input
                OnProcess(elapsed);

                // process frame
                SceneManager.Process(elapsed);

                DeviceManager.Clear();
            }
        }

        internal void Reset()
        {
            SceneManager.Reset();
            Start();
        }

        public void Close()
        {
            OnClose();

            Server?.Stop();
            Engine.ExitApplication();
        }

        public virtual bool IsNewGfx { get; set; } = true;
        public virtual string Language
        {
            get => FileSystem.LocalizationCode;
            set => FileSystem.LocalizationCode = value;
        }
    }

    public class ApplicationInternal : IApplication
    {
        Module wrap;

        public ApplicationInternal(Module Wrap)
        {
            wrap = Wrap;
        }

        public string Title => wrap.Title;
        public int MaxVerticalResolution => wrap.MaxVerticalResolution;
        public Vector2 MinResolution => wrap.MinResolution;
        public Vector2 MaxResolution => wrap.MaxResolution;
        public System.Drawing.Icon? Icon => wrap.Icon;

        public void Render(RenderTarget Target)
        {
            wrap.Render(Target);
        }

        public void Process(float Elapsed)
        {
            wrap.Process(Elapsed);
        }

        public void Reset()
        {
            wrap.Reset();
        }

        public void Close()
        {
            wrap.Close();
        }
    }
}
