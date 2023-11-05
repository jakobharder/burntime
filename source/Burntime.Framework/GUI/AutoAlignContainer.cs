using Burntime.Platform;
using Burntime.Platform.Graphics;
using System.Linq;

namespace Burntime.Framework.GUI;

public class AutoAlignContainer : Container
{
    public AutoAlignContainer(Module app) : base(app)
    {
    }

    public override void OnUpdate(float elapsed)
    {
        base.OnUpdate(elapsed);

        if (Windows.Count == 0)
            return;

        int totalSpaces = Size.x - Windows.Sum(window => window.Size.x);

        int margin = totalSpaces / (windows.Count - 1);
        int lastMargin = windows.Count > 1 ? totalSpaces - margin * (windows.Count - 2) : margin;
        Window? lastWindow = windows.Count > 1 ? windows[^2] : null;

        int position = 0;
        foreach (var window in windows)
        {
            window.Position = new Vector2(position, window.Position.y);
            position += window.Size.x;
            position += (window == lastWindow) ? lastMargin : margin;
        }
    }
}
