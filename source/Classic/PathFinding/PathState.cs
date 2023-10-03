using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Data.BurnGfx;

namespace Burntime.Classic.PathFinding
{
    [Serializable]
    public abstract class PathState : StateObject
    {
        protected float speed;
        public float Speed
        {
            get { return speed; }
            set { speed = value; }
        }

        public abstract Vector2 MoveTo { get; set; }
        public abstract Vector2 Process(PathMask mask, Vector2 position, float elapsed);
        public abstract void DebugRender(RenderTarget target);
    }
}
