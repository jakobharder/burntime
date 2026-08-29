using Burntime.Data.BurnGfx;
using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Remaster.PathFinding
{
    [System.Serializable]
    public class ManualPath : PathState
    {
        Vector2 moveTo;
        Vector2 position;

        [System.NonSerialized]
        Vector2f direction;

        public Vector2f Direction
        {
            get => direction;
            set
            {
                direction = value;
                if (direction != Vector2f.Zero)
                    direction.Normalize();
            }
        }

        public override Vector2 MoveTo
        {
            get => moveTo;
            set => moveTo = value;
        }

        public override Vector2 Process(PathMask mask, Vector2 position, float elapsed)
        {
            this.position = position;
            Vector2f manualPosition = position;
            float distance = speed * elapsed;

            // Keep movement on the same per-update integer raster as ComplexPath,
            // while checking long frames in small steps so narrow boundaries are
            // not skipped.
            while (distance > 0 && direction != Vector2f.Zero)
            {
                float stepDistance = System.Math.Min(distance, 1);
                Vector2f step = direction * stepDistance;
                Vector2f fullPosition = manualPosition + step;

                if (IsWalkable(mask, fullPosition))
                {
                    manualPosition = fullPosition;
                }
                else
                {
                    bool moved = false;
                    Vector2f horizontalPosition = manualPosition + new Vector2f(step.x, 0);
                    if (step.x != 0 && IsWalkable(mask, horizontalPosition))
                    {
                        manualPosition = horizontalPosition;
                        moved = true;
                    }

                    Vector2f verticalPosition = manualPosition + new Vector2f(0, step.y);
                    if (step.y != 0 && IsWalkable(mask, verticalPosition))
                    {
                        manualPosition = verticalPosition;
                        moved = true;
                    }

                    if (!moved)
                        break;
                }

                distance -= stepDistance;
            }

            this.position = manualPosition;
            moveTo = this.position;
            return this.position;
        }

        static bool IsWalkable(PathMask mask, Vector2f position)
        {
            if (position.x < 0 || position.y < 0)
                return false;

            // Use the same centered mask-cell sampling as ComplexPath. Some
            // original entry points sit just over a blocked cell boundary when
            // floor-sampled, even though pathfinding places them in the adjacent
            // walkable cell.
            Vector2 maskPosition = ((Vector2)position + (mask.Resolution / 2 - 1)) /
                mask.Resolution;
            return mask[maskPosition];
        }

        public override void DebugRender(RenderTarget target)
        {
        }
    }
}
