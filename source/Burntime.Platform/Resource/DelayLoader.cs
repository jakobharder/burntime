using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Burntime.Platform.Graphics;

namespace Burntime.Platform.Resource;

public class DelayLoader
{
    readonly IResourceManager _resourceManager;
    readonly List<ISprite> _loadingQueue;
    readonly Thread _thread;
    readonly AutoResetEvent _loadingRequested;

    public bool IsLoading => _loadingQueue.Count > 0;

    public DelayLoader(IResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
        _loadingQueue = new List<ISprite>();
        _loadingRequested = new AutoResetEvent(false);
        _thread = new(new ThreadStart(RunThread))
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal
        };
    }

    public void Enqueue(ISprite Sprite)
    {
        lock (_loadingQueue)
        {
            if (_loadingQueue.Contains(Sprite))
                return;
            _loadingQueue.Add(Sprite);
        }

        _loadingRequested.Set();
    }

    public void Run()
    {
        _stopLoader = false;
        _thread.Start();
    }

    public void Reset()
    {
        lock (_loadingQueue)
            _loadingQueue.Clear();
    }

    void RunThread()
    {
        Thread.CurrentThread.Name = "DelayLoader";

        while (!_stopLoader)
        {
            ISprite? nextToLoad = null;
            lock (_loadingQueue)
                nextToLoad = _loadingQueue.FirstOrDefault();

            if (nextToLoad is not null)
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
                    _resourceManager.Reload(nextToLoad, ResourceLoadType.Now);

                    lock (_loadingQueue)
                    {
                        // queue may have been cleared in the meantime
                        if (_loadingQueue.Count > 0)
                            _loadingQueue.RemoveAt(0);
                    }
                }
            }

            if (_loadingQueue.Count == 0)
            {
                // queue is empty, wait for new arrivals
                _loadingRequested.WaitOne(200, true);
            }
        }
    }

    bool _stopLoader = false;
    public void Stop()
    {
        _stopLoader = true;
        _loadingRequested.Set();
    }
}
