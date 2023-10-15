using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Platform.Graphics
{
    public abstract class Particle
    {
        public abstract Vector2 Position { get; }
        public abstract float Alpha { get; }
        public abstract ISprite Sprite { get; }
        public abstract bool IsAlive { get; }

        public abstract void Process(float elapsed);
    }
}
