﻿using System;
using System.Collections.Generic;

namespace Burntime.Framework.States
{
    [Serializable]
    public abstract class WorldState : StateObject, ITurnable
    {
        public virtual void Turn()
        {
            foreach (PlayerState player in Player)
            {
                player.isFinished = false;
            }
        }

        public abstract StateObject CurrentPlayer { get; }
        public abstract StateObject CurrentLocation { get; }

        public abstract PlayerState[] Player { get; }
        public abstract int CurrentPlayerIndex { get; }

        public abstract PlayerState CheckWinner();

        public abstract Dictionary<string, string> GetSaveHint();
        public abstract bool HasValidSaveHint { get; }
        public abstract void UpdateSaveHint();
    }
}
