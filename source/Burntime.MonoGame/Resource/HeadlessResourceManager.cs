using Burntime.MonoGame.Graphics;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.Resource;

namespace Burntime.MonoGame.Resource;

/// <summary>
/// Loads game data while representing visual resources with unloaded placeholder sprites.
/// </summary>
internal sealed class HeadlessResourceManager : ResourceManagerBase, IDisposable
{
    public HeadlessResourceManager(ILoadingCounter loadingCounter) : base(loadingCounter)
    {
    }

    public override Font? GetFont(string file, PixelColor color) => null;

    public override Font? GetFont(string file, PixelColor color, PixelColor backColor) => null;

    public override ISprite GetImage(ResourceID id, ResourceLoadType loadType = ResourceLoadType.Delayed)
    {
        if (sprites.TryGetValue(id, out ISprite? cached))
            return cached.Clone();

        int frameCount = id.EndIndex >= id.Index && id.EndIndex >= 0
            ? id.EndIndex - id.Index + 1
            : 1;
        SpriteFrame[] frames = Enumerable.Range(0, frameCount).Select(_ => new SpriteFrame()).ToArray();
        SpriteAnimation? animation = frameCount > 1 ? new SpriteAnimation(frameCount) : null;
        Sprite sprite = new(this, id, frames, animation!);
        sprites.Add(id, sprite);
        return sprite.Clone();
    }

    public override void Reload(ISprite sprite, ResourceLoadType loadType = ResourceLoadType.Delayed)
    {
        // Visual data is intentionally never loaded in headless mode.
    }
}
