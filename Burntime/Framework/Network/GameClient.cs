using System;
using System.Collections.Generic;
using System.Threading;

using Burntime.Framework.States;

namespace Burntime.Framework.Network
{
    public enum GameClientState
    {
        Waiting, 
        Ready,
        Dead
    }

    public class GameClient
    {
        public static GameClient NoClient = new GameClient();

        GameClientState state = GameClientState.Waiting;
        internal EventWaitHandle FinishedEvent = new EventWaitHandle(false, EventResetMode.AutoReset);
        internal bool isServerStopped;
        internal bool isGameOver;
        internal IGameServer server;
        bool isLocal;
        protected EventWaitHandle readyEvent = new EventWaitHandle(false, EventResetMode.AutoReset);

        public bool NeedsAllStates
        {
            get { return stateContainer.Root == null; }
        }

        public bool IsLocal
        {
            get { return isLocal; }
        }

        public bool IsGameOver
        {
            get { return isGameOver; }
        }

        PlayerState PlayerState
        {
            get { return stateContainer.Root.Player[player]; }
        }

        StateManager stateContainer;
        public StateManager StateContainer
        {
            get { return stateContainer; }
        }

        public int MachineCode = 0;
        public int TravelDays = 0;

        int player;
        public int Player
        {
            get { return player; }
        }

        public EventWaitHandle ReadyEvent
        {
            get { return readyEvent; }
        }

        // Null client constructor
        GameClient()
        {
            player = -1;
        }

        public GameClient(Module module, int player)
        {
            this.player = player;

            if (module != null && module.ResourceManager != null)
                stateContainer = new StateManager(module.ResourceManager, "client #" + player);
            else
                throw new NullReferenceException();
        }

        public GameClient(Module module,int player, StateManager sharedStateContainer)
        {
            this.player = player;

            if (sharedStateContainer == null && module != null && module.ResourceManager != null)
                stateContainer = new StateManager(module.ResourceManager, "client #" + player);
            else
            {
                stateContainer = sharedStateContainer;
                isLocal = sharedStateContainer.ServerStateManager;
            }
        }

        public bool IsReady
        {
            get { return state == GameClientState.Ready; }
        }

        public GameClientState State
        {
            get { return state; }
        }

        public bool IsServerStopped
        {
            get { return isServerStopped; }
        }

        public void Process()
        {
            Finish();
        }

        public void Finish()
        {
            if (this == NoClient)
                return;

            // in case ready event was not used, we need to reset it manually
            readyEvent.Reset();

            if (PlayerState != null)
                PlayerState.isFinished = true;
            state = GameClientState.Waiting;

            if (!isLocal)
            {
                //stateContainer.CheckConsistency(); // DEBUG

                // send changes to server and update local object ids with global id
                System.IO.Stream buf = stateContainer.GetChanges(1);
                Burntime.Platform.Debug.SetInfoMB("sync out message size client #" + player, (int)buf.Length);

                buf = server.SendChanges(buf);
                stateContainer.UpdateIds(buf);

                //stateContainer.CheckConsistency(); // DEBUG
            }

            FinishedEvent.Set();
        }

        internal void Activate()
        {
            state = GameClientState.Ready;
            TravelDays = 0;

            //set ready event
            readyEvent.Set();
        }

        internal void Activate(System.IO.Stream stateChanges)
        {
            Burntime.Platform.Debug.SetInfoMB("sync in message size client #" + player, (int)stateChanges.Length);

            //stateContainer.CheckConsistency(); // DEBUG

            stateContainer.Update(stateChanges);
            stateContainer.MonitorChanges();

            state = GameClientState.Ready;
            TravelDays = 0;

            // set ready event
            readyEvent.Set();
        }

        internal void Travel()
        {
            TravelDays++;
        }

        public void Die()
        {
            state = GameClientState.Dead;
        }

    }
}
