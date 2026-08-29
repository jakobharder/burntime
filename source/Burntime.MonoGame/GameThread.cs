using Burntime.Platform;
using System;
using System.Diagnostics;
using System.Threading;

namespace Burntime.MonoGame;

internal class GameThread
{
    readonly ManualResetEventSlim _stopRequested = new(false);
    Thread _renderThread;
    string _threadName;
    int _framesPerSecond;
    Action<GameTime> _call;

    public void Start(Action<GameTime> call, string threadName = "GameThread", int framesPerSecond = 60)
    {
        if (framesPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond));

        _threadName = threadName;
        _framesPerSecond = framesPerSecond;
        _call = call;

        _stopRequested.Reset();
        _renderThread = new Thread(new ThreadStart(WorkerThread)) { IsBackground = true };
        _renderThread.Start();
    }

    public void Stop()
    {
        if (_renderThread is null)
            return;

        _stopRequested.Set();
        _renderThread.Join();
        _renderThread = null;
    }

    void WorkerThread()
    {
        Thread.CurrentThread.Name = _threadName;
        GameTime gameTime = new();
        double frameTicks = Stopwatch.Frequency / (double)_framesPerSecond;
        long previousFrame = Stopwatch.GetTimestamp();
        double nextFrame = previousFrame + frameTicks;

        while (!_stopRequested.IsSet)
        {
            if (!WaitUntil(nextFrame))
                break;

            long now = Stopwatch.GetTimestamp();
            gameTime.Elapsed = (now - previousFrame) / (float)Stopwatch.Frequency;
            previousFrame = now;
            _call(gameTime);

            nextFrame += frameTicks;
            long completed = Stopwatch.GetTimestamp();
            if (completed >= nextFrame)
                nextFrame = completed + frameTicks;
        }
    }

    bool WaitUntil(double targetTicks)
    {
        while (!_stopRequested.IsSet)
        {
            double remainingMilliseconds =
                (targetTicks - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency;
            if (remainingMilliseconds <= 0)
                return true;

            if (remainingMilliseconds > 2)
            {
                if (_stopRequested.Wait((int)remainingMilliseconds - 1))
                    return false;
            }
            else
            {
                Thread.SpinWait(64);
            }
        }

        return false;
    }
}
