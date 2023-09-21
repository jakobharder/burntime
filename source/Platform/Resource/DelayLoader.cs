using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Burntime.Platform.Graphics;

namespace Burntime.Platform.Resource
{
    class DelayLoader
    {
        ResourceManager resourceManager;
        Engine engine;
        List<Sprite> toLoad;
        Thread thread;
        AutoResetEvent activate;

        public bool IsLoading
        {
            get { return toLoad.Count > 0; }
        }

        public DelayLoader(Engine Engine)
        {
            resourceManager = Engine.ResourceManager;
            engine = Engine;
            toLoad = new List<Sprite>();
            activate = new AutoResetEvent(false);
        }

        public void Enqueue(Sprite Sprite)
        {
            if (toLoad.Contains(Sprite))
                return;

            // TODO: critical section
            toLoad.Add(Sprite);
            activate.Set();
        }

        public void Run()
        {
            stop = false;
            thread = new Thread(new ThreadStart(RunThread));
            thread.IsBackground = true;
            thread.Priority = ThreadPriority.BelowNormal;
            thread.Start();
        }

        public void Reset()
        {
            toLoad.Clear();
        }

        void RunThread()
        {
            Thread.CurrentThread.Name = "DelayLoader";

            while (!stop)
            {
                if (toLoad.Count > 0)
                {
                    if (engine.SafeMode)
                    {
                        try
                        {
                            if (!engine.crashed)
                            {
                                resourceManager.Reload(toLoad[0], ResourceLoadType.Now);
                                toLoad.RemoveAt(0);
                            }
                        }
                        catch
                        {
                            engine.crashed = true;
                        }
                    }
                    else
                    {
                        resourceManager.Reload(toLoad[0], ResourceLoadType.Now);
                        toLoad.RemoveAt(0);
                    }
                }

                if (toLoad.Count == 0)
                    activate.WaitOne(200, true);
            }
        }

        bool stop = false;
        public void Stop()
        {
            stop = true;
            activate.Set();
        }
    }
}
