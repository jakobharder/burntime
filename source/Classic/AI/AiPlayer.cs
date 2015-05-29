
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

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
