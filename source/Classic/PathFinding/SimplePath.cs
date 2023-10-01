using System;
using Burntime.Data.BurnGfx;
using Burntime.Platform;

namespace Burntime.Classic.PathFinding
{
    [Serializable]
    public class SimplePath : PathState
    {
        protected Vector2f moveTo;
        protected Vector2f position;

        public override Vector2 MoveTo
        {
            get { return moveTo; }
            set { moveTo = value; }
        }

        public override Vector2 Process(PathMask mask, Vector2 position, float elapsed)
        {
            this.position = position;
            Vector2f walkVector = moveTo - this.position;

            // forecast position, half a mask tile ahead
            Vector2f forecastVector = walkVector;

            float speedElapsed = elapsed * speed;

            // quit if goal is reached
            if (walkVector.Length < 0.1f)
                return position;

            if (walkVector.Length > speedElapsed)
            {
                walkVector.Normalize();

                forecastVector = walkVector * (speedElapsed + mask.Resolution / 2);
                walkVector *= speedElapsed;
            }

            // calculate new position
            Vector2f walkPosition = this.position + walkVector;
            Vector2f forecastPosition = this.position + forecastVector;

            if (mask.IsWalkableMapPosition(walkPosition) && mask.IsWalkableMapPosition(forecastPosition))
            {
                this.position = walkPosition;
            }
            else // otherwise just cancel walking
                moveTo = this.position;

            return this.position;
        }

        public override void DebugRender(Burntime.Platform.Graphics.IRenderTarget target)
        {
            if (position != moveTo)
                target.RenderLine(position, moveTo, new PixelColor(255, 0, 0));
        }
    }
}
