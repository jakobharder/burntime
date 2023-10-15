using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Framework.States
{
    [Serializable]
    public abstract class PlayerState : StateObject
    {
        protected internal bool isFinished;
        protected StateLink<AiState> aiState;

        public bool IsFinished
        {
            get { return isFinished; }
            set { isFinished = value; }
        }

        public AiState AiState
        {
            get { return aiState; }
            set { aiState = value; }
        }

        public abstract void Turn();

        public abstract bool IsDead { get; set; }
        public abstract string Name { get; set; }
        public abstract bool IsTraveling { get; }
        public abstract int Index { get; }
    }
}
