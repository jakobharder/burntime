namespace Burntime.Remaster.Logic;

internal enum WeaponReach
{
    Close,
    Medium,
    Long,
    Ranged
}

internal static class WeaponReachRules
{
    internal static WeaponReach FromRange(float range) => range switch
    {
        <= 16 => WeaponReach.Close,
        <= 22 => WeaponReach.Medium,
        <= 29 => WeaponReach.Long,
        _ => WeaponReach.Ranged
    };
}
