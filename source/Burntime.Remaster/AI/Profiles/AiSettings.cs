using System;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.AI;

/// <summary>
/// Serialized configuration shared by AI profile state objects.
/// </summary>
[Serializable]
public struct AiSettings
{
    // Serialized by v1.0.4. Keep these unused members so the legacy settings
    // contract remains explicit and old save data continues to bind cleanly.
    public int MinInterval;
    public int MaxInterval;
    public int MaxAdvance;

    // Zero intentionally maps old saves without this field to easy.
    [System.Runtime.Serialization.OptionalField]
    public int Difficulty;

    // Extended is zero so old saves retain the established Remaster AI.
    [System.Runtime.Serialization.OptionalField]
    public AiProfileId Profile;
}
