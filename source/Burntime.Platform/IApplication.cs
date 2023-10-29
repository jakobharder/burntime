using Burntime.Platform.Graphics;

namespace Burntime.Platform;

public interface IApplication
{
    string Title { get; }
    int MaxVerticalResolution { get; }
    Vector2 MinResolution { get; }
    Vector2 MaxResolution { get; }

    void Render(RenderTarget Target);
    void Process(float Elapsed);
    void Reset();
    void Close();
}
