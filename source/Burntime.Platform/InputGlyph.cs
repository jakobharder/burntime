namespace Burntime.Platform;

/// <summary>Small local glyphs used when a controller label is not represented by the UI font.</summary>
public enum InputGlyph
{
    None = 0,
    FaceSouth,
    FaceEast,
    FaceWest,
    FaceNorth,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
    Menu,
    View,
    DPadHorizontal,
    RightStick,
    Plus,
    Minus,
    LeftShoulder,
    RightShoulder
}

public enum GamepadLabelStyle
{
    Xbox,
    PlayStation,
    Steam,
    Switch
}

public enum ControllerGlyphMode
{
    Auto,
    Xbox,
    PlayStation,
    Steam,
    Switch
}

/// <summary>
/// Translates the XInput-shaped controls consumed by the game into device-appropriate UI glyphs.
/// </summary>
public interface IInputGlyphProvider
{
    /// <summary>Changes when the active controller family changes.</summary>
    int Revision { get; }
    GamepadLabelStyle LabelStyle { get; }

    /// <summary>Returns no glyph when the localized text fallback should be used.</summary>
    InputGlyph GetGlyph(GamepadControl control);

    /// <summary>Returns a device label when Steam translated the XInput button to a differently named control.</summary>
    string? GetLabelOverride(GamepadControl control);
}

public sealed class TextInputGlyphProvider : IInputGlyphProvider
{
    public static TextInputGlyphProvider Instance { get; } = new();
    public int Revision => 0;
    public GamepadLabelStyle LabelStyle => GamepadLabelStyle.Xbox;
    public InputGlyph GetGlyph(GamepadControl control) => InputGlyph.None;
    public string? GetLabelOverride(GamepadControl control) => null;

    private TextInputGlyphProvider() { }
}
