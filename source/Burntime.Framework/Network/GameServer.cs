using System;
using System.Collections.Generic;
using System.Threading;

using Burntime.Platform;
using Burntime.Framework.States;

namespace Burntime.Framework.Network
{

    struct ClientProcessQueueItem
    {
        public GameClient Client;
        public int Queue;
    }

    class ClientProcessMultiQueue
    {
        Queue<GameClient>[] queues;

        public ClientProcessMultiQueue(int lines)
        {
            queues = new Queue<GameClient>[lines];
            for (int i = 0; i < lines; i++)
                queues[i] = new Queue<GameClient>();
        }

        public void Enqueue(int queue, GameClient client)
        {
            queues[queue].Enqueue(client);
        }

        public GameClient Dequeue(int queue)
        {
            if (queues[queue].Count == 0)
                return null;
            return queues[queue].Dequeue();
        }
    }

    class PlayerTurnLogic
    {
        public ClientProcessMultiQueue Process(WorldState world, GameClient[] clients)
        {
            // gather leader location

            // update leader locations

            // find collisions

            // active not colliding ai / network player

            // active first collision leader

            // wait for collision leader ready & launch next

            // wait for all

            ClientProcessMultiQueue queue = new ClientProcessMultiQueue(clients.Length);
            foreach (GameClient client in clients)
            {
                if (client.State != GameClientState.Dead && !world.Player[client.Player].IsFinished && !world.Player[client.Player].IsTraveling)
                    queue.Enqueue(0, client);
            }

            return queue;
        }
    }

    class GameServerObject
    {
        bool stop = false;
        
        WorldState world;
        StateManager container;

        EventWaitHandle serverStop = new EventWaitHandle(false, EventResetMode.AutoReset);
        Queue<ITurnNews> news = new Queue<ITurnNews>();

        public WorldState World
        {
            get { return world; }
        }

        public List<GameClient> Clients = new List<GameClient>();

        public GameServerObject(WorldState world, StateManager container)
        {
            this.world = world;
            this.container = container;
        }

        public void Run()
        {
            Thread.CurrentThread.Name = "GameServer";
            bool gameOver = false;

            // check if we can skip any synchronization (all clients are local)
            bool skipAnySynchronization = true;
            foreach (GameClient client in Clients)
            {
                if (!client.IsLocal)
                {
                    skipAnySynchronization = false;
                    break;
                }
            }

            // run game server loop
            while (!stop)
            {
                PlayerTurnLogic PlayerTurnLogic = new PlayerTurnLogic();
                ClientProcessMultiQueue queues = PlayerTurnLogic.Process(world, Clients.ToArray());

                List<ClientProcessQueueItem> wait = new List<ClientProcessQueueItem>();

                for (int i = 0; i < Clients.Count; i++)
                {
                    GameClient client = queues.Dequeue(i);
                    if (client != null)
                    {
                        ClientProcessQueueItem item;
                        item.Client = client;
                        item.Queue = i;
                        wait.Add(item);
                    }
                }

                // merge and update server game state
                if (!skipAnySynchronization)
                    container.Synchronize();

                for (int i = 0; i < wait.Count; i++)
                {
                    // if local client then activate without synchronization
                    if (wait[i].Client.IsLocal)
                        wait[i].Client.Activate();
                    else // otherwise send changes
                        wait[i].Client.Activate(wait[i].Client.NeedsAllStates ? container.GetAllStates() : container.GetChanges(wait[i].Client.TravelDays + 1));
                }

                while (wait.Count != 0)
                {
                    EventWaitHandle[] ewh = new EventWaitHandle[wait.Count + 1];
                    ewh[0] = serverStop;

                    for (int i = 0; i < wait.Count; i++)
                        ewh[i + 1] = wait[i].Client.FinishedEvent;

                    // wait for any finished client or server stop event
                    int finished = WaitHandle.WaitAny(ewh);
                    if (finished == 0)
                        break;

                    // get finished client
                    GameClient finishedClient = wait[finished - 1].Client;

                    // get next client in queue
                    int queue = wait[finished - 1].Queue;
                    GameClient newclient = queues.Dequeue(queue);
                    wait.RemoveAt(finished - 1);

                    // if client is available, then signal it ready
                    if (newclient != null)
                    {
                        // no need to synchronize result if last client was local
                        if (!finishedClient.IsLocal)
                            container.Synchronize(false);

                        ClientProcessQueueItem item;
                        item.Client = newclient;
                        item.Queue = queue;
                        wait.Add(item);

                        // if local client then activate without synchronization
                        if (newclient.IsLocal)
                            newclient.Activate();
                        else // otherwise send changes
                            newclient.Activate(newclient.NeedsAllStates ? container.GetAllStates() : container.GetChanges(newclient.TravelDays + 1));
                    }
                }

                //container.CheckConsistency(); // DEBUG

                if (!stop)
                {
                    // game turn operations

                    // turn world
                    world.Turn();

                    bool onePlayerAlive = false;

                    // turn all traveling clients and retrieve state
                    for (int i = 0; i < Clients.Count; i++)
                    {
                        if (world.Player[Clients[i].Player].IsDead && Clients[i].State != GameClientState.Dead)
                        {
                            Clients[i].Die();
                            news.Enqueue(new DeathNews(world.Player[Clients[i].Player].Name));
                        }
                        else if (world.Player[Clients[i].Player].IsTraveling)
                        {
                            Clients[i].Travel();
                        }

                        // turn player
                        world.Player[Clients[i].Player].Turn();

                        if (Clients[i].State != GameClientState.Dead)
                            onePlayerAlive = true;

                        world.Player[Clients[i].Player].IsFinished = false;
                    }

                    // quit game server if everybody died
                    if (!onePlayerAlive)
                        break;

                    // check for any victory
                    PlayerState winner = world.CheckWinner();
                    if (winner != null)
                    {
                        news.Clear();
                        news.Enqueue(new VictoryNews(winner));
                        gameOver = true;
                        break;
                    }

                    if (!skipAnySynchronization)
                        container.Synchronize(false);

                    //container.CheckConsistency(); // DEBUG
                }
            }

            foreach (GameClient client in Clients)
            {
                client.isServerStopped = true;
                client.isGameOver = gameOver;
            }
        }

        public void Stop()
        {
            stop = true;
            serverStop.Set();
        }

        public ITurnNews PopNews()
        {
            if (news.Count == 0)
                return null;

            return news.Dequeue();
        }
    }

    public class GameServer : IGameServer
    {
        GameServerObject serverObj;
        Thread serverThread;
        AI.AIControl aiControl;
        StateManager stateContainer;

        public WorldState World
        {
            get { return serverObj.World; }
        }

        public StateManager StateContainer
        {
            get { return stateContainer; }
        }

        public GameClient[] Clients
        {
            get { return serverObj.Clients.ToArray(); }
        }

        public void Create(WorldState world, StateManager container)
        {
            stateContainer = container;
            serverObj = new GameServerObject(world, container);
            serverThread = new Thread(new ThreadStart(serverObj.Run));
            serverThread.IsBackground = true;
            aiControl = new Burntime.Framework.AI.AIControl();
        }

        public void Run()
        {
            Log.Info("Start game server thread...");
            serverThread.Start();
            Log.Info("Start ai player thread...");
            aiControl.Start();
        }

        public void AddClient(GameClient client)
        {
            serverObj.Clients.Add(client);
            client.server = this;
        }

        public void AddAI(GameClient client)
        {
            AddClient(client);
            aiControl.Player.Add(client);
            client.server = this;
        }

        public System.IO.Stream SendChanges(System.IO.Stream buffer)
        {
            lock(this)
            {
                return stateContainer.UpdateMain(buffer);
            }
        }

        public void Stop()
        {
            Log.Info("Stop game server thread...");
            if (serverObj != null)
                serverObj.Stop();
            Log.Info("Stop ai player thread...");
            if (aiControl != null)
                aiControl.Stop();
        }

        public bool IsStopped
        {
            get { if (serverThread == null) return true; return (serverThread.ThreadState == ThreadState.Stopped || serverThread.ThreadState == ThreadState.Unstarted); }
        }

        public ITurnNews PopNews()
        {
            return serverObj.PopNews();
        }
    }
}