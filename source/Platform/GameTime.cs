using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;

namespace Burntime.Platform
{
    public class GameTime
    {
        public float Elapsed = 0;
        long TimeStamp;

        public GameTime()
        {
            Reset();
        }

        public void Reset()
        {
            Elapsed = 0;
            TimeStamp = Stopwatch.GetTimestamp();
        }

        public void Refresh(int MinimumElapsed)
        {
            long last = TimeStamp;

            TimeStamp = Stopwatch.GetTimestamp();
            Elapsed = ((TimeStamp - last) / (float)Stopwatch.Frequency);
            if (Elapsed < MinimumElapsed)
            {
                Thread.Sleep(MinimumElapsed - 1 - (int)Elapsed);
                TimeStamp = Stopwatch.GetTimestamp();
                Elapsed = ((TimeStamp - last) / (float)Stopwatch.Frequency);
            }
        }
    }

}
