using Burntime.Framework.States;

namespace Burntime.Remaster.Logic;

class AttackEvent : ILogicNotifycation
{
    public Character Attacker { get; init; }
    public Character Defender { get; init; }

    public AttackEvent(Character attacker, Character defender)
    {
        Attacker = attacker;
        Defender = defender;
    }
}
