using Burntime.Platform;
using Burntime.Framework.States;

namespace Burntime.Remaster.Logic
{
    class AttackEvent : ILogicNotifycation
    {
        Vector2 attacker;
        Vector2 defender;

        public Vector2 Attacker
        {
            get { return attacker; }
        }

        public Vector2 Defender
        {
            get { return defender; }
        }

        public AttackEvent(Vector2 attacker, Vector2 defender)
        {
            this.attacker = attacker;
            this.defender = defender;
        }
    }
}
