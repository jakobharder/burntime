using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.Logic.Rules;

/// <summary>
/// Curated original-content rules. DOS supplies the predictable territory
/// progression, recruitment and trader cycle; Amiga supplies fair regional
/// starts, production rates and useful minimum pump output.
/// </summary>
internal sealed class ClassicRules : DosRules
{
    public override RuleSet Id => RuleSet.Classic;

    protected override void SetOriginalStartLocations(ClassicGame game, GameSettings settings) =>
        StartLocationPlacement.ApplyRegional(game, settings);

    public override int CalculateWaterOutput(int baseOutput, bool handPump, bool industrialPump) =>
        RuleFormulas.OriginalWaterOutput(baseOutput, handPump, industrialPump);
}
