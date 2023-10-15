using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Framework.States;

namespace Burntime.Framework.AI
{
    public class GameClientAI : Burntime.Framework.Network.GameClient
    {
        public GameClientAI(Module module, int player)
            : base(module, player)
        {

        }

        public GameClientAI(Module module, int player, StateManager sharedStateContainer)
            : base(module, player, sharedStateContainer)
        {

        }

        public virtual void Turn()
        {
        }
    }
}
