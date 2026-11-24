using Burntime.Framework;

namespace Burntime.MonoGame;

public partial class BurntimeGame
{
    internal string AndroidContentPath { get; set; } = "";
    internal bool AndroidSmallDeviceUi { get; set; }
    int androidBackPending;

    // Android callbacks run on the UI thread. Dispatch game actions during Update.
    public void QueueAndroidBack() => Interlocked.Exchange(ref androidBackPending, 1);

    void HandleAndroidBack()
    {
        if (Interlocked.Exchange(ref androidBackPending, 0) != 0 && _mobileActive && IsActive)
            _burntimeApp.InputManager.Press(InputAction.Back);
    }
}
