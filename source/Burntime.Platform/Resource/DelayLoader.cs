using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Burntime.Platform.Graphics;

namespace Burntime.Platform.Resource;

public class DelayLoader
{
    static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);
    readonly IResourceManager _resourceManager;
    readonly List<ISprite> _loadingQueue;
    readonly Thread _thread;
    readonly AutoResetEvent _loadingRequested;
    readonly object _loadSync = new();

    bool _started;
    volatile bool _stopLoader;
    volatile bool _suspended;

    public bool IsLoading
    {
        get { lock (_loadingQueue) return _loadingQueue.Count > 0; }
    }

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
            if (_stopLoader)
                return;
            if (_loadingQueue.Contains(Sprite))
                return;
            _loadingQueue.Add(Sprite);
        }

        _loadingRequested.Set();
    }

    public void Run()
    {
        lock (_loadingQueue)
        {
            if (_started)
                return;
            _stopLoader = false;
            _started = true;
            _thread.Start();
        }
    }

    public void Reset()
    {
        lock (_loadingQueue)
            _loadingQueue.Clear();
    }

    public void SetSuspended(bool suspended)
    {
        // Prevent another load from starting before waiting for the current one.
        _suspended = suspended;
        lock (_loadSync) { }
        _loadingRequested.Set();
    }

    void RunThread()
    {
        Thread.CurrentThread.Name = "DelayLoader";

        while (!_stopLoader)
        {
            lock (_loadSync)
            {
                ISprite? nextToLoad = null;
                if (!_suspended && !_stopLoader)
                {
                    lock (_loadingQueue)
                        nextToLoad = _loadingQueue.FirstOrDefault();
                }

                if (nextToLoad is not null)
                {
                    _resourceManager.Reload(nextToLoad, ResourceLoadType.Now);

                    lock (_loadingQueue)
                    {
                        // Reset may have removed this entry while it was loading.
                        _loadingQueue.Remove(nextToLoad);
                    }
                }
            }

            if (_suspended || !IsLoading)
            {
                // queue is empty, wait for new arrivals
                _loadingRequested.WaitOne(200, true);
            }
        }
    }

    public bool Stop()
    {
        _stopLoader = true;
        _loadingRequested.Set();
        if (!_started || _thread == Thread.CurrentThread)
            return true;
        return _thread.Join(StopTimeout);
    }
}
