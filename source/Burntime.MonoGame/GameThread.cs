using Burntime.Platform;
using System;
using System.Threading;

namespace Burntime.MonoGame;

internal class GameThread
{
    bool _requestStop = false;
    Thread _renderThread;
    AutoResetEvent _renderFinished;
    string _threadName;
    int _minimumElapsed;
    Action<GameTime> _call;

    public void Start(Action<GameTime> call, string threadName = "GameThread", int minimumElapsed = 25)
    {
        _threadName = threadName;
        _minimumElapsed = minimumElapsed;
        _call = call;

        _renderFinished = new AutoResetEvent(true);
        _renderThread = new Thread(new ThreadStart(WorkerThread)) { IsBackground = true };
        _renderThread.Start();
    }

    public void Stop()
    {
        _requestStop = true;
        _renderFinished.Set();
        _renderThread.Join();
    }

    void WorkerThread()
    {
        Thread.CurrentThread.Name = _threadName;
        GameTime gameTime = new();

        while (!_requestStop)
        {
            gameTime.Refresh(_minimumElapsed);
            _call(gameTime);

            //renderFinished.Set();
        }
    }
}
