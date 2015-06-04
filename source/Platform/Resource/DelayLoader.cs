
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

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
