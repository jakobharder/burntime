using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Burntime.MonoGame;

namespace Burntime.Remaster.Tests;

using static Program;

static class GameThreadTests
{
    internal static IEnumerable<Case<int>> Cases()
    {
        yield return Int("suspension finishes pending work and excludes background time", 0, () =>
        {
            using AutoResetEvent parked = new(false);
            using AutoResetEvent frame = new(false);
            GameThread worker = new();
            int frames = 0;
            float elapsed = 0;
            worker.SetSuspended(true);
            try
            {
                worker.Start(time =>
                {
                    elapsed = time.Elapsed;
                    Interlocked.Increment(ref frames);
                    frame.Set();
                }, suspendedWork: () => parked.Set());
                Equal(true, parked.WaitOne(2000), "initial suspended work finishes");
                Equal(0, Volatile.Read(ref frames), "no game frame during initial suspension");

                // Repeated inactive notifications may queue another settings save.
                worker.SetSuspended(true);
                Equal(true, parked.WaitOne(2000), "parked worker handles new suspended work");
                Equal(false, frame.WaitOne(400), "suspended game does not tick");

                worker.SetSuspended(false);
                Equal(true, frame.WaitOne(2000), "game resumes");
                Equal(true, elapsed < 0.3f, "first frame excludes the background interval");

                while (parked.WaitOne(0)) { }
                worker.SetSuspended(true);
                Equal(true, parked.WaitOne(2000), "worker reaches suspension after active frames");
                int pausedFrames = Volatile.Read(ref frames);
                // Drain any notification from the last active frame.
                while (frame.WaitOne(0)) { }
                Equal(false, frame.WaitOne(100), "no further frames while parked");
                Equal(pausedFrames, Volatile.Read(ref frames), "frame count stays fixed");
            }
            finally { worker.Stop(); }
            return 0;
        });

        yield return Int("stop interrupts a long frame wait", 0, () =>
        {
            GameThread worker = new();
            worker.Start(_ => { }, framesPerSecond: 1);
            Stopwatch timer = Stopwatch.StartNew();
            worker.Stop();
            Equal(true, timer.Elapsed < TimeSpan.FromSeconds(0.5),
                "stop wakes the thread instead of waiting for its deadline");
            return 0;
        });
    }
}
