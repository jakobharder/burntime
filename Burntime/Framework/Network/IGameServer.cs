using System;
using System.Collections.Generic;
using System.Threading;

using Burntime.Framework.States;

namespace Burntime.Framework.Network
{
    public interface IGameServer
    {
        void Create(WorldState world, StateManager container);

        void AddClient(GameClient client);
        bool IsStopped { get; }

        System.IO.Stream SendChanges(System.IO.Stream buffer);

        ITurnNews PopNews();
    }
}