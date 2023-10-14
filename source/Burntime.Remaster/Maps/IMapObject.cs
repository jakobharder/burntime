using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.Maps
{
    public interface IMapObject
    {
        String GetTitle(IResourceManager ResourceManager);
        Vector2 MapPosition { get; }
        Rect MapArea { get; }
    }
}
