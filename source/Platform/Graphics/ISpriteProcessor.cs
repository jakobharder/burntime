using System.IO;

namespace Burntime.Platform.Resource;

public interface ISpriteProcessor
{
    void Process(ResourceID id);
    void Render(Stream s, int stride);
    Vector2 Size { get; }
}

public interface ISpriteAnimationProcessor
{
    int FrameCount { get; }
    Vector2 FrameSize { get; }
    bool SetFrame(int frame);
}
