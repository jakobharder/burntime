using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework.States;

namespace Burntime.Classic.Maps
{
    public interface IMapViewOverlay
    {
        void MouseMoveOverlay(Vector2 Position);
        void UpdateOverlay(WorldState world, float elapsed);
        void RenderOverlay(IRenderTarget Target, Vector2 Offset, Vector2 Size);

        bool IsVisible { get; set; }
        IMapObject GetObjectAt(Vector2 position);
    }
}
