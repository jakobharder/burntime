using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Classic.Logic;
using Burntime.Framework;
using Burntime.Framework.States;

namespace Burntime.Classic.AI
{
    public class AiPlayer : Framework.AI.GameClientAI
    {
        public AiPlayer(Module module, int player, StateManager sharedStateContainer) :
            base(module, player, sharedStateContainer)
        {
        }

        public override void Turn()
        {
            Player player = StateContainer.Root.Player[this.Player] as Player;
            // that shouldn't happen
            if (player == null || player.Type != PlayerType.Ai)
                throw new BurntimeLogicException();

            ClassicAiState ai = player.AiState as ClassicAiState;
            if (ai == null)
                throw new BurntimeLogicException();

            ai.Turn();
        }
    }
}
