using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Classic.Logic;

namespace Burntime.Classic.Maps
{
    public interface IMapObject
    {
        String GetTitle(ResourceManager ResourceManager);
        Vector2 MapPosition { get; }
        Rect MapArea { get; }
    }
}
