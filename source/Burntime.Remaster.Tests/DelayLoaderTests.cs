using System.Collections.Generic;
using System.Threading;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.Resource;

namespace Burntime.Remaster.Tests;

using static Program;

static class DelayLoaderTests
{
    internal static IEnumerable<Case<int>> Cases()
    {
        yield return Int("queued resources wake the loader after suspension", 0, () =>
        {
            using TestResources resources = new();
            DelayLoader loader = new(resources);
            try
            {
                loader.Run();
                loader.Enqueue(new TestSprite());
                Equal(true, resources.Loaded.WaitOne(2000), "enqueue wakes an empty loader");
                loader.SetSuspended(true);
                loader.Enqueue(new TestSprite());
                Equal(false, resources.Loaded.WaitOne(100), "queued work waits during suspension");
                loader.SetSuspended(false);
                Equal(true, resources.Loaded.WaitOne(2000), "resume processes queued work");
                loader.SetSuspended(true);
            }
            finally
            {
                Equal(true, loader.Stop(), "stop wakes a suspended loader");
            }
            return 0;
        });
    }

    sealed class TestResources : ResourceManagerBase, System.IDisposable
    {
        internal readonly AutoResetEvent Loaded = new(false);
        public TestResources() : base(new LoadingCounter()) { }
        public override Font? GetFont(string file, PixelColor color) => null;
        public override Font? GetFont(string file, PixelColor color, PixelColor backColor) => null;
        public override ISprite GetImage(ResourceID id,
            ResourceLoadType loadType = ResourceLoadType.Delayed) => new TestSprite();
        public override void Reload(ISprite sprite,
            ResourceLoadType loadType = ResourceLoadType.Delayed) => Loaded.Set();
        public new void Dispose() { base.Dispose(); Loaded.Dispose(); }
    }

    sealed class LoadingCounter : ILoadingCounter
    {
        public void IncreaseLoadingCount() { }
        public void DecreaseLoadingCount() { }
    }

    sealed class TestSprite : ISprite
    {
        public override ResourceID ID => "test.png";
        public override Vector2f Resolution { get; set; } = Vector2f.One;
        public override Vector2 Size => Vector2.One;
        public override SpriteAnimation Animation { get; set; } = null!;
        public override bool IsLoaded => true;
        public override bool HasSystemCopy => true;
        public override bool Touch() => true;
        public override void Update(float elapsed) { }
        public override int Unload() => 0;
        public override ISprite Clone() => this;
    }
}
