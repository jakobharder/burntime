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
            Vector2f dif = moveTo - this.position;

            float speedElapsed = elapsed * speed;

            // quit if goal is reached
            if (dif.Length < 0.1f)
                return position;

            // calculate distance
            if (dif.Length > speedElapsed)
            {
                dif.Normalize();
                dif *= speedElapsed;
            }

            // calculate new position
            Vector2f newpos = this.position + dif;
            Rectf boundaries = new Rectf(0, 0, mask.Width * mask.Resolution, mask.Height * mask.Resolution);
            if (boundaries.PointInside(newpos))
            {
                // transform to map mask position
                Vector2 maskpos = mask.ConvertMapToMask(newpos);

                // set new position if there is no obstacle
                if (mask[maskpos])
                    this.position = newpos;
                else // otherwise just cancel walking
                    moveTo = this.position;
            }
            else // otherwise just cancel walking
                moveTo = this.position;

            return this.position;
        }

        public override void DebugRender(Burntime.Platform.Graphics.RenderTarget target)
        {
            if (position != moveTo)
                target.RenderLine(position, moveTo, new PixelColor(255, 0, 0));
        }
    }
}
