using System;
using System.Collections.Generic;
using System.Threading;

using Burntime.Framework.Network;
using Burntime.Platform;

namespace Burntime.Framework.AI
{
    public class AIControl
    {
        static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

        public List<GameClient> Player = new List<GameClient>();
        Thread thread = null;

        // event 0 is server stop request
        // events 1..n are ready signals from game server
        EventWaitHandle[] readyEvent;

        public void Start()
        {
            readyEvent = new EventWaitHandle[Player.Count + 1];
            // create server stop event
            readyEvent[0] = new EventWaitHandle(false, EventResetMode.AutoReset);
            // set game client ready events
            for (int i = 0; i < Player.Count; i++)
                readyEvent[i + 1] = Player[i].ReadyEvent;

            thread = new Thread(new ThreadStart(RunThread));
            thread.IsBackground = true;
            thread.Start();
        }

        public void Stop()
        {
            if (readyEvent != null)
                readyEvent[0].Set();

            if (thread != null && thread != Thread.CurrentThread && thread.IsAlive &&
                !thread.Join(StopTimeout))
                Log.Warning("AI player thread did not stop within five seconds.");
        }

        void RunThread()
        {
            while (true)
            {
                // wait for ready signal or server stop
                int finished = WaitHandle.WaitAny(readyEvent);
                // server stop event fired
                if (finished == 0)
                    break;

                // turn ai player
                TurnAiPlayer(Player[finished - 1] as GameClientAI);
            }
        }

        private void TurnAiPlayer(GameClientAI client)
        {
            if (client == null)
                return;

            client.Turn();

            // finish turn
            client.Finish();
        }
    }
}
