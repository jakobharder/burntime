using System;

using Burntime.Framework;
using Burntime.Data.BurnGfx.ResourceProcessor;

namespace Burntime.Data.BurnGfx
{
    public class BurnGfxModule : Module
    {
        protected override void OnInitialize()
        {
            AddProcessor("raw", (Burntime.Platform.Resource.ISpriteProcessor)new SpriteLoaderRaw());
            AddProcessor("ani", (Burntime.Platform.Resource.ISpriteProcessor)new SpriteLoaderAni());
            AddProcessor("pac", new SpriteLoaderPac());
            AddProcessor("raw", new FontProcessor());
            AddProcessor("burngfxtile", new TileProcessor());
            AddProcessor("burngfxani", (Burntime.Platform.Resource.ISpriteProcessor)new SpriteLoaderAni());
        }
    }
}