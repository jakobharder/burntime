using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Data.BurnGfx;

namespace Burntime.Remaster.PathFinding
{
    [Serializable]
    public abstract class PathState : StateObject
    {
        protected float speed;
        [NonSerialized]
        Vector2f precisePosition;
        [NonSerialized]
        bool precisePositionInitialized;
        [NonSerialized]
        Vector2f movementStartPosition;

        public Vector2f MovementDirection { get; private set; }

        public float Speed
        {
            get { return speed; }
            set { speed = value; }
        }

        public abstract Vector2 MoveTo { get; set; }
        public abstract Vector2 Process(PathMask mask, Vector2 position, float elapsed);
        public abstract void DebugRender(RenderTarget target);

        protected Vector2f BeginMovement(Vector2 rasterPosition)
        {
            if (!precisePositionInitialized || (Vector2)precisePosition != rasterPosition)
            {
                precisePosition = rasterPosition;
                precisePositionInitialized = true;
            }

            movementStartPosition = precisePosition;
            MovementDirection = Vector2f.Zero;
            return precisePosition;
        }

        protected Vector2 CommitMovement(Vector2f position)
        {
            MovementDirection = position - movementStartPosition;
            precisePosition = position;
            return precisePosition;
        }
    }
}
