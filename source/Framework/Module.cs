﻿
#region GNU General Public License - Burntime
/*
 *  Burntime
 *  Copyright (C) 2008-2011 Jakob Harder
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
*/
#endregion

using System;
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

        public void Initialize()
        {
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

        internal ResourceManager resourceManager;
        public virtual void AddProcessor(String Extension, ISpriteProcessor Processor)
        {
            Log.Info("   Add resource processor for '" + Extension + "': " + Processor.GetType().Name);
            resourceManager.AddSpriteProcessor(Extension, Processor);
        }

        public virtual void AddProcessor(String Extension, IFontProcessor Processor)
        {
            Log.Info("   Add resource processor for '" + Extension + "': " + Processor.GetType().Name);
            resourceManager.AddFontProcessor(Extension, Processor);
        }

        public void AddProcessor(IDataProcessor processor)
        {
            dataProcessors.Add(processor);
        }

        public virtual String Title { get { return ""; } }
        public virtual Vector2[] Resolutions { get { return new Vector2[] { new Vector2(640, 480) }; } }
        public virtual float VerticalRatio { get { return 1; } }
        public virtual System.Drawing.Icon Icon { get { return null; } }

        protected static Module instance;
        public static Module Instance
        {
            get { return instance; }
        }

        public Sprite MouseImage = null;
        public bool RenderMouse = true;
        public Nullable<Rect> MouseBoundings
        {
            get { return DeviceManager.Mouse.Boundings; }
            set { DeviceManager.Mouse.Boundings = value; }
        }

        public Engine Engine;
        public SceneManager SceneManager;
        public ResourceManager ResourceManager;
        public DeviceManager DeviceManager;
        public ConfigFile Settings;

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

        internal void Render(RenderTarget Target)
        {
            Target.Layer = 0;

            SceneManager.Render(Target);

            if (MouseImage != null && RenderMouse)
            {
                Target.Layer = 255;
                Target.DrawSprite(DeviceManager.Mouse.Position, MouseImage);
            }
        }

        internal void Process(float Elapsed)
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

                // process frame
                SceneManager.Process(Elapsed);

                DeviceManager.Refresh();
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

            if (Server != null)
                Server.Stop();
            Engine.Close();
        }
    }

    public class ApplicationInternal : IApplication
    {
        Module wrap;

        public ApplicationInternal(Module Wrap)
        {
            wrap = Wrap;
        }

        public String Title { get { return wrap.Title; } }
        public Vector2[] Resolutions { get { return wrap.Resolutions; } }
        public System.Drawing.Icon Icon { get { return wrap.Icon; } }

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
