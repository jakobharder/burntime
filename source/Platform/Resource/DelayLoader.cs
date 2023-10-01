using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Burntime.Platform.Graphics;

namespace Burntime.Platform.Resource
{
    public class DelayLoader
    {
        readonly IResourceManager resourceManager_;
        readonly List<ISprite> toLoad_;
        Thread thread;
        readonly AutoResetEvent activate_;

        public bool IsLoading => toLoad_.Count > 0;

        public DelayLoader(IResourceManager resourceManager)
        {
            resourceManager_ = resourceManager;
            toLoad_ = new List<ISprite>();
            activate_ = new AutoResetEvent(false);
        }

        public void Enqueue(ISprite Sprite)
        {
            if (toLoad_.Contains(Sprite))
                return;

            // TODO: critical section
            toLoad_.Add(Sprite);
            activate_.Set();
        }

        public void Run()
        {
            stop = false;
            thread = new(new ThreadStart(RunThread))
            {
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal
            };
            thread.Start();
        }

        public void Reset()
        {
            toLoad_.Clear();
        }

        void RunThread()
        {
            Thread.CurrentThread.Name = "DelayLoader";

            while (!stop)
            {
                if (toLoad_.Count > 0)
                {
                    //if (engine.SafeMode)
                    //{
                    //    try
                    //    {
                    //        if (!engine.crashed)
                    //        {
                    //            resourceManager.Reload(toLoad_[0], ResourceLoadType.Now);
                    //            toLoad_.RemoveAt(0);
                    //        }
                    //    }
                    //    catch
                    //    {
                    //        engine.crashed = true;
                    //    }
                    //}
                    //else
                    {
                        resourceManager_.Reload(toLoad_[0], ResourceLoadType.Now);
                        toLoad_.RemoveAt(0);
                    }
                }

                if (toLoad_.Count == 0)
                    activate_.WaitOne(200, true);
            }
        }

        bool stop = false;
        public void Stop()
        {
            stop = true;
            activate_.Set();
        }
    }
}
