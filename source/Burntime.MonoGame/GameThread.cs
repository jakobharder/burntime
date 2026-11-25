using Burntime.Platform;
using System;
using System.Diagnostics;
using System.Threading;

namespace Burntime.MonoGame;

internal class GameThread
{
    static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);
    readonly AutoResetEvent _scheduleChanged = new(false);
    volatile bool _stopRequested;
    volatile bool _suspended;
    Action? _suspendedWork;
    Thread? _renderThread;
    string _threadName = "GameThread";
    volatile int _framesPerSecond;
    Action<GameTime> _call = _ => { };

    public void Start(Action<GameTime> call, string threadName = "GameThread", int framesPerSecond = 60,
        Action? suspendedWork = null)
    {
        if (framesPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond));

        _threadName = threadName;
        _framesPerSecond = framesPerSecond;
        _call = call;

        _suspendedWork = suspendedWork;
        _stopRequested = false;
        _renderThread = new Thread(new ThreadStart(WorkerThread)) { IsBackground = true };
        _renderThread.Start();
    }

    public void Stop()
    {
        Thread? thread = _renderThread;
        if (thread is null)
            return;

        _stopRequested = true;
        _scheduleChanged.Set();
        if (thread == Thread.CurrentThread || thread.Join(StopTimeout))
            _renderThread = null;
        else
            Log.Warning($"Timed out stopping {thread.Name ?? "game thread"}.");
    }

    public void SetFrameRate(int framesPerSecond)
    {
        if (framesPerSecond <= 0 || framesPerSecond == _framesPerSecond)
            return;

        _framesPerSecond = framesPerSecond;
        _scheduleChanged.Set();
    }

    public void SetSuspended(bool suspended)
    {
        _suspended = suspended;
        // Also wakes an already suspended worker to finish newly queued settings saves.
        _scheduleChanged.Set();
    }

    void WorkerThread()
    {
        Thread.CurrentThread.Name = _threadName;
        GameTime gameTime = new();
        int framesPerSecond = _framesPerSecond;
        double frameTicks = Stopwatch.Frequency / (double)framesPerSecond;
        long previousFrame = Stopwatch.GetTimestamp();
        double nextFrame = previousFrame + frameTicks;

        while (!_stopRequested)
        {
            int requestedFrameRate = _framesPerSecond;
            if (requestedFrameRate != framesPerSecond)
            {
                framesPerSecond = requestedFrameRate;
                frameTicks = Stopwatch.Frequency / (double)framesPerSecond;
                // Preserve active simulation time when the input mode changes.
                nextFrame = previousFrame + frameTicks;
            }

            if (_suspended)
            {
                _suspendedWork?.Invoke();
                if (_stopRequested) break;
                _scheduleChanged.WaitOne();
                // Background time must not advance the simulation on resume.
                previousFrame = Stopwatch.GetTimestamp();
                nextFrame = previousFrame + frameTicks;
                continue;
            }

            double remainingMilliseconds =
                (nextFrame - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency;
            if (remainingMilliseconds > 0)
            {
                // Round up instead of spinning through the final milliseconds.
                _scheduleChanged.WaitOne((int)System.Math.Ceiling(remainingMilliseconds));
                continue;
            }

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
}
