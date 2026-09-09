namespace Burntime.Remaster.Logic.Rules;

// Damage before armour. Defense is percentage reduction; null hides the defence label.
internal readonly record struct CombatPreview(int Minimum, int Maximum, int? Defense);
