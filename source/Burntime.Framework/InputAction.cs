namespace Burntime.Framework;

/// <summary>
/// Device-independent actions understood by the engine UI and Burntime scenes.
/// Physical controls are translated to these actions by the game bindings.
/// </summary>
public enum InputAction
{
    None = 0,
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    MoveUpLeft,
    MoveUpRight,
    MoveDownLeft,
    MoveDownRight,
    PanCameraUp,
    PanCameraDown,
    PanCameraLeft,
    PanCameraRight,
    LeftArea,
    RightArea,
    Back,
    Primary,
    Secondary,
    SceneAction,
    Options,
    Statistics,
    ToggleInteractionMode,
    NextTurn,
    Inventory,
    LocationInfo,
    WorldMap
}

public static class InputActionDirections
{
    public static bool IsUp(this InputAction action) => action == InputAction.MoveUp;
    public static bool IsDown(this InputAction action) => action == InputAction.MoveDown;
    public static bool IsLeft(this InputAction action) => action == InputAction.MoveLeft;
    public static bool IsRight(this InputAction action) => action == InputAction.MoveRight;
}
