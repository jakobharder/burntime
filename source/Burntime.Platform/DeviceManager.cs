using Burntime.Platform.Utils;

namespace Burntime.Platform;

public enum MouseButton
{
    None = 0,
    Left = 1,
    Right = 2
}

public struct MouseClickInfo
{
    public bool Down;
    public Vector2 Position;
    public MouseButton Button;
}

public interface IMouseDevice
{
    Vector2 Position { get; }
    Nullable<Rect> Boundings { get; set; }
    bool IsRightDown { get; }

    /// <summary>
    /// thread-safe
    /// </summary>
    IEnumerable<MouseClickInfo> Clicks { get; }
}

sealed class MouseDevice : IMouseDevice
{
    private readonly Resolution _resolution;

    private Vector2 current = Vector2.Zero;
    private Nullable<Vector2> previous;
    private List<MouseClickInfo> clicks = new List<MouseClickInfo>();

    public bool IsRightDown { get; set; }
    public Rect? Boundings { get; set; }

    public MouseDevice(Resolution resolution)
    {
        _resolution = resolution;
    }

    public Vector2 Position
    {
        get { return current; }
        set
        {
            if (Boundings is not null)
                current.Clamp(Boundings.Value);
            else
                current.ClampMaxExcluding(Vector2.Zero, _resolution.Game);

            if (!previous.HasValue || previous.Value != current)
                previous = current;
            current = value;
        }
    }

    public Vector2 LastDirection
    {
        get
        {
            if (previous.HasValue)
                return current - previous.Value;

            return Vector2.Zero;
        }
    }

    public void ClearPrevious()
    {
        previous = null;
    }

    /// <summary>
    /// returns a copy, thread-safe
    /// </summary>
    public IEnumerable<MouseClickInfo> Clicks
    {
        get 
        {
            IEnumerable<MouseClickInfo> copy;
            lock (this)
                copy = clicks.ToArray();
            return copy; 
        }
    }

    /// <summary>
    /// thread-safe
    /// </summary>
    /// <param name="click"></param>
    public void AddClick(MouseClickInfo click)
    {
        lock (this)
            clicks.Add(click);
    }

    /// <summary>
    /// thread-safe
    /// </summary>
    public void ClearClicks()
    {
        lock (this)
            clicks.Clear();
    }
}

public enum SystemKey
{
    None = 0,
    F1,
    F2,
    F3,
    F4,
    F8,
    F9,
    Escape,
    Pause,
    Enter,
    Other,
    Up,
    Down,
    Left,
    Right,
    Tab
}

[Flags]
public enum ModifierKeys
{
    None = 0,
    LeftAlt,
    Shift
}

public readonly struct Key
{
    public char Character { get; init; }
    public SystemKey VirtualKey { get; init; }
    public ModifierKeys Modifier { get; init; }
    public bool IsVirtual => VirtualKey != SystemKey.None;

    public Key(char key, ModifierKeys modifier = ModifierKeys.None)
    {
        Character = key;
        VirtualKey = SystemKey.None;
        Modifier = modifier;
    }

    public Key(SystemKey vkey, ModifierKeys modifier = ModifierKeys.None)
    {
        Character = (char)0;
        VirtualKey = vkey;
        Modifier = modifier;
    }
}

public class Keyboard
{
    List<Key> keys = new List<Key>();
    /// <summary>
    /// returns a copy, thread-safe
    /// </summary>
    public Key[] Keys
    {
        get 
        {
            Key[] copy;
            lock (this)
                copy =  keys.ToArray();
            return copy;
        }
    }

    /// <summary>
    /// thread-safe
    /// </summary>
    /// <param name="key"></param>
    public void AddKey(Key key)
    {
        lock (this)
            keys.Add(key);
    }

    /// <summary>
    /// thread-safe
    /// </summary>
    public void ClearKeys()
    {
        lock (this)
            keys.Clear();
    }
}

public class DeviceManager
{
    private readonly MouseDevice _mouse;
    private readonly List<GamepadControl> _gamepadControls = new();
    private readonly HashSet<GamepadControl> _gamepadControlsDown = new();
    public IMouseDevice Mouse => _mouse;

    public GamepadControl[] GamepadControls
    {
        get { lock (_gamepadControls) return _gamepadControls.ToArray(); }
    }

    public GamepadControl[] ConsumeGamepadControls()
    {
        lock (_gamepadControls)
        {
            GamepadControl[] controls = _gamepadControls.ToArray();
            _gamepadControls.Clear();
            return controls;
        }
    }

    public GamepadControl[] GamepadControlsDown
    {
        get { lock (_gamepadControlsDown) return _gamepadControlsDown.ToArray(); }
    }

#warning TODO implement proper mouse state
    public bool IsRightDown
    {
        get => _mouse.IsRightDown;
        set => _mouse.IsRightDown = value;
    }

    public Keyboard Keyboard { get; } = new();

    private readonly Resolution _resolution;

    public DeviceManager(Resolution resolution)
    {
#warning TODO thread synchronization, update and UI thread may be different now
        _mouse = new MouseDevice(resolution);
        _resolution = resolution;
    }

    public void MouseMove(Vector2 Position)
    {
        _mouse.Position = new Vector2(Position);
    }

    public void MouseDown(Vector2 position, MouseButton button)
    {
        _mouse.AddClick(new()
        {
            Position = new Vector2(position),
            Button = button,
            Down = true
        });
    }

    public void MouseClick(Vector2 position, MouseButton button)
    {
        _mouse.AddClick(new()
        {
            Position = new Vector2(position),
            Button = button,
            Down = false
        });
    }

    public void MouseLeave()
    {
        if (_mouse.LastDirection == Vector2.Zero) return;

        Vector2 position = _mouse.Position;

        Rect bounds = new(Vector2.Zero, _resolution.Game);
        while (bounds.PointInside(position))
            position += _mouse.LastDirection;

        position.Clamp(bounds);
        _mouse.Position = position;
        _mouse.ClearPrevious();
    }

    public void KeyPress(char key, ModifierKeys modifier = ModifierKeys.None)
    {
        Keyboard.AddKey(new Key(key, modifier));
    }

    public void VKeyPress(SystemKey key, ModifierKeys modifier = ModifierKeys.None)
    {
        Keyboard.AddKey(new Key(key, modifier));
    }

    public void GamepadControlPress(GamepadControl control)
    {
        if (control == GamepadControl.None)
            return;
        lock (_gamepadControls)
            _gamepadControls.Add(control);
    }

    public void SetGamepadControlDown(GamepadControl control, bool isDown)
    {
        if (control == GamepadControl.None)
            return;
        lock (_gamepadControlsDown)
        {
            if (isDown)
                _gamepadControlsDown.Add(control);
            else
                _gamepadControlsDown.Remove(control);
        }
    }

    public void ClearGamepadControlsDown()
    {
        lock (_gamepadControlsDown)
            _gamepadControlsDown.Clear();
    }

    public void Clear()
    {
        _mouse.ClearClicks();
        Keyboard.ClearKeys();
    }
}
