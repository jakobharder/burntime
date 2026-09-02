using Burntime.Platform;

namespace Burntime.MonoGame;

/// <summary>Deterministic controller presentation for local UI and glyph testing.</summary>
sealed class ForcedInputGlyphProvider(GamepadLabelStyle labelStyle) : IInputGlyphProvider
{
    static int _nextRevision;
    public int Revision { get; } = System.Threading.Interlocked.Increment(ref _nextRevision);
    public GamepadLabelStyle LabelStyle { get; } = labelStyle;

    public InputGlyph GetGlyph(GamepadControl control) => control switch
        {
            GamepadControl.A => InputGlyph.FaceSouth,
            GamepadControl.B => InputGlyph.FaceEast,
            GamepadControl.X => InputGlyph.FaceWest,
            GamepadControl.Y => InputGlyph.FaceNorth,
            GamepadControl.DPadUp => InputGlyph.DPadUp,
            GamepadControl.DPadDown => InputGlyph.DPadDown,
            GamepadControl.DPadLeft => InputGlyph.DPadLeft,
            GamepadControl.DPadRight => InputGlyph.DPadRight,
            GamepadControl.Menu => LabelStyle == GamepadLabelStyle.Switch
                ? InputGlyph.Plus
                : InputGlyph.Menu,
            GamepadControl.View => LabelStyle == GamepadLabelStyle.Switch
                ? InputGlyph.Minus
                : InputGlyph.View,
            GamepadControl.RightStick => InputGlyph.RightStick,
            GamepadControl.LeftShoulder => InputGlyph.LeftShoulder,
            GamepadControl.RightShoulder => InputGlyph.RightShoulder,
            _ => InputGlyph.None
        };

    public string? GetLabelOverride(GamepadControl control) => null;
}
