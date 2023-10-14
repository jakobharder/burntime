using System;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Interaction;

namespace Burntime.Remaster.AI
{
    [Serializable]
    public abstract class CharacterMind : StateObject
    {
        protected StateLink<Character> owner;
        public Character Owner 
        {
            get { return owner; }
            set { owner = value; }
        }

        protected override void InitInstance(object[] parameter)
        {
            if (parameter == null || parameter.Length < 1 || !(parameter[0] is Character))
                throw new InvalidStateObjectConstruction(this);

            owner = parameter[0] as Character;

            base.InitInstance(parameter);
        }

        public abstract void Process(float elapsed);
        public virtual void RequestToTalk() { }
        public virtual void MoveToObject(InteractionObject obj) { }
    }
}
