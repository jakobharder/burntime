using Burntime.Platform;
using Steamworks;
using System;

namespace Burntime.MonoGame;

/// <summary>
/// Uses Steam only as an XInput-to-action-origin bridge. MonoGame remains the input backend.
/// Steam is queried on the main thread and the game/render thread reads the cached result.
/// </summary>
sealed class SteamInputGlyphProvider : IInputGlyphProvider, IDisposable
{
    readonly InputGlyph[] _glyphs = new InputGlyph[Enum.GetValues<GamepadControl>().Length];
    readonly string?[] _labelOverrides = new string?[Enum.GetValues<GamepadControl>().Length];
    readonly ForcedInputGlyphProvider _fallback = new(GamepadLabelStyle.Xbox);
    bool _initialized;
    string _controllerFamily = string.Empty;

    int _revision;
    public int Revision { get { lock (_glyphs) return _revision; } }
    public string? CurrentGameLanguage { get; private set; }
    public GamepadLabelStyle LabelStyle
    {
        get
        {
            lock (_glyphs)
                return _controllerFamily switch
                {
                    "PlayStation" => GamepadLabelStyle.PlayStation,
                    "Steam" => GamepadLabelStyle.Steam,
                    "Switch" => GamepadLabelStyle.Switch,
                    _ => GamepadLabelStyle.Xbox
                };
        }
    }

    public SteamInputGlyphProvider()
    {
        bool steamApiInitialized = false;
        try
        {
            steamApiInitialized = SteamAPI.Init();
            if (steamApiInitialized)
            {
                CurrentGameLanguage = SteamApps.GetCurrentGameLanguage();
                _initialized = SteamInput.Init(false);
                if (!_initialized)
                {
                    SteamAPI.Shutdown();
                    steamApiInitialized = false;
                }
            }
            Log.Info(_initialized
                ? "Steamworks initialized; Steam Input glyph bridge enabled"
                : "Steamworks unavailable; using Xbox controller glyphs");
        }
        catch (Exception exception)
        {
            if (steamApiInitialized)
                SteamAPI.Shutdown();
            Log.Info($"Steamworks unavailable ({exception.GetType().Name}); using Xbox controller glyphs");
        }
    }

    public static string? DetectCurrentGameLanguage()
    {
        bool initialized = false;
        try
        {
            initialized = SteamAPI.Init();
            return initialized ? SteamApps.GetCurrentGameLanguage() : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (initialized)
                SteamAPI.Shutdown();
        }
    }

    public void RunFrame()
    {
        if (!_initialized)
            return;

        try
        {
            SteamAPI.RunCallbacks();
            SteamInput.RunFrame();
            RefreshMappings();
        }
        catch (Exception exception)
        {
            Log.Warning($"Steam Input glyph bridge disabled: {exception.Message}");
            Shutdown();
        }
    }

    public InputGlyph GetGlyph(GamepadControl control)
    {
        int index = (int)control;
        lock (_glyphs)
        {
            InputGlyph glyph = index >= 0 && index < _glyphs.Length
                ? _glyphs[index]
                : InputGlyph.None;
            return glyph == InputGlyph.None ? _fallback.GetGlyph(control) : glyph;
        }
    }

    public string? GetLabelOverride(GamepadControl control)
    {
        int index = (int)control;
        lock (_glyphs)
            return index >= 0 && index < _labelOverrides.Length ? _labelOverrides[index] : null;
    }

    void RefreshMappings()
    {
        lock (_glyphs)
        {
            InputHandle_t handle = SteamInput.GetControllerForGamepadIndex(0);
            if (handle.m_InputHandle == 0)
            {
                ClearMappings();
                return;
            }

            EInputActionOrigin familyOrigin = SteamInput.GetActionOriginFromXboxOrigin(handle,
                EXboxOrigin.k_EXboxOrigin_A);
            string family = GetFamily(familyOrigin);
            bool changed = family != _controllerFamily;

            foreach (GamepadControl control in Enum.GetValues<GamepadControl>())
            {
                if (control == GamepadControl.None)
                    continue;

                EInputActionOrigin origin = SteamInput.GetActionOriginFromXboxOrigin(handle,
                    ToXboxOrigin(control));
                InputGlyph glyph = GetGlyphForOrigin(origin, family);
                string? labelOverride = GetLabelForOrigin(origin, family);
                int index = (int)control;
                if (_glyphs[index] == glyph && _labelOverrides[index] == labelOverride)
                    continue;

                _glyphs[index] = glyph;
                _labelOverrides[index] = labelOverride;
                changed = true;
            }

            if (!changed)
                return;

            bool familyChanged = family != _controllerFamily;
            _controllerFamily = family;
            _revision++;
            if (familyChanged && family.Length > 0)
                Log.Info($"Steam Input controller glyph family: {family}");
        }
    }

    void ClearMappings()
    {
        lock (_glyphs)
        {
            bool changed = _controllerFamily.Length > 0;
            _controllerFamily = string.Empty;
            for (int i = 0; i < _glyphs.Length; i++)
            {
                changed |= _glyphs[i] != InputGlyph.None || _labelOverrides[i] is not null;
                _glyphs[i] = InputGlyph.None;
                _labelOverrides[i] = null;
            }
            if (changed)
                _revision++;
        }
    }

    static InputGlyph GetGlyphForOrigin(EInputActionOrigin actionOrigin, string family)
    {
        string origin = actionOrigin.ToString();
        if (family == "PlayStation")
        {
            if (origin.EndsWith("_X", StringComparison.Ordinal))
                return InputGlyph.FaceSouth;
            if (origin.EndsWith("_Circle", StringComparison.Ordinal))
                return InputGlyph.FaceEast;
            if (origin.EndsWith("_Square", StringComparison.Ordinal))
                return InputGlyph.FaceWest;
            if (origin.EndsWith("_Triangle", StringComparison.Ordinal))
                return InputGlyph.FaceNorth;
        }
        else if (family == "Switch")
        {
            if (origin.EndsWith("_B", StringComparison.Ordinal))
                return InputGlyph.FaceSouth;
            if (origin.EndsWith("_A", StringComparison.Ordinal))
                return InputGlyph.FaceEast;
            if (origin.EndsWith("_Y", StringComparison.Ordinal))
                return InputGlyph.FaceWest;
            if (origin.EndsWith("_X", StringComparison.Ordinal))
                return InputGlyph.FaceNorth;
        }
        else
        {
            if (origin.EndsWith("_A", StringComparison.Ordinal))
                return InputGlyph.FaceSouth;
            if (origin.EndsWith("_B", StringComparison.Ordinal))
                return InputGlyph.FaceEast;
            if (origin.EndsWith("_X", StringComparison.Ordinal))
                return InputGlyph.FaceWest;
            if (origin.EndsWith("_Y", StringComparison.Ordinal))
                return InputGlyph.FaceNorth;
        }

        if (origin.Contains("_DPad_North", StringComparison.Ordinal))
            return InputGlyph.DPadUp;
        if (origin.Contains("_DPad_South", StringComparison.Ordinal))
            return InputGlyph.DPadDown;
        if (origin.Contains("_DPad_West", StringComparison.Ordinal))
            return InputGlyph.DPadLeft;
        if (origin.Contains("_DPad_East", StringComparison.Ordinal))
            return InputGlyph.DPadRight;
        if (origin.EndsWith("_Plus", StringComparison.Ordinal))
            return InputGlyph.Plus;
        if (origin.EndsWith("_Minus", StringComparison.Ordinal))
            return InputGlyph.Minus;
        if (origin.EndsWith("_Menu", StringComparison.Ordinal) ||
            origin.EndsWith("_Options", StringComparison.Ordinal))
            return InputGlyph.Menu;
        if (origin.EndsWith("_View", StringComparison.Ordinal) ||
            origin.EndsWith("_Share", StringComparison.Ordinal) ||
            origin.EndsWith("_Create", StringComparison.Ordinal) ||
            origin.EndsWith("_Capture", StringComparison.Ordinal))
            return InputGlyph.View;
        if (origin.EndsWith("_LeftStick_Click", StringComparison.Ordinal))
            return InputGlyph.None;
        if (origin.EndsWith("_RightStick_Click", StringComparison.Ordinal))
            return InputGlyph.RightStick;
        if (origin.EndsWith("_LeftBumper", StringComparison.Ordinal))
            return InputGlyph.LeftShoulder;
        if (origin.EndsWith("_RightBumper", StringComparison.Ordinal))
            return InputGlyph.RightShoulder;
        return InputGlyph.None;
    }

    static string? GetLabelForOrigin(EInputActionOrigin actionOrigin, string family) => null;

    static string GetFamily(EInputActionOrigin origin)
    {
        string name = origin.ToString();
        if (name.Contains("_PS3_", StringComparison.Ordinal) ||
            name.Contains("_PS4_", StringComparison.Ordinal) ||
            name.Contains("_PS5_", StringComparison.Ordinal))
            return "PlayStation";
        if (name.Contains("_Switch_", StringComparison.Ordinal))
            return "Switch";
        if (name.Contains("_SteamDeck_", StringComparison.Ordinal) ||
            name.Contains("_SteamController_", StringComparison.Ordinal))
            return "Steam";
        if (name.Contains("_XBox", StringComparison.Ordinal))
            return "Xbox";
        return string.Empty;
    }

    static EXboxOrigin ToXboxOrigin(GamepadControl control) => control switch
    {
        GamepadControl.A => EXboxOrigin.k_EXboxOrigin_A,
        GamepadControl.B => EXboxOrigin.k_EXboxOrigin_B,
        GamepadControl.X => EXboxOrigin.k_EXboxOrigin_X,
        GamepadControl.Y => EXboxOrigin.k_EXboxOrigin_Y,
        GamepadControl.Menu => EXboxOrigin.k_EXboxOrigin_Menu,
        GamepadControl.View => EXboxOrigin.k_EXboxOrigin_View,
        GamepadControl.LeftShoulder => EXboxOrigin.k_EXboxOrigin_LeftBumper,
        GamepadControl.RightShoulder => EXboxOrigin.k_EXboxOrigin_RightBumper,
        GamepadControl.LeftStick => EXboxOrigin.k_EXboxOrigin_LeftStick_Click,
        GamepadControl.RightStick => EXboxOrigin.k_EXboxOrigin_RightStick_Click,
        GamepadControl.LeftTrigger => EXboxOrigin.k_EXboxOrigin_LeftTrigger_Pull,
        GamepadControl.RightTrigger => EXboxOrigin.k_EXboxOrigin_RightTrigger_Pull,
        GamepadControl.DPadUp => EXboxOrigin.k_EXboxOrigin_DPad_North,
        GamepadControl.DPadDown => EXboxOrigin.k_EXboxOrigin_DPad_South,
        GamepadControl.DPadLeft => EXboxOrigin.k_EXboxOrigin_DPad_West,
        GamepadControl.DPadRight => EXboxOrigin.k_EXboxOrigin_DPad_East,
        _ => EXboxOrigin.k_EXboxOrigin_A
    };

    public void Dispose() => Shutdown();

    void Shutdown()
    {
        if (!_initialized)
            return;

        SteamInput.Shutdown();
        SteamAPI.Shutdown();
        _initialized = false;
        ClearMappings();
    }
}
