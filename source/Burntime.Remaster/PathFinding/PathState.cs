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

        public virtual void Stop(Vector2 position)
        {
            MoveTo = position;
            precisePosition = position;
            precisePositionInitialized = true;
            movementStartPosition = position;
            MovementDirection = Vector2f.Zero;
        }

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

        protected virtual bool IsPositionWalkable(PathMask mask, Vector2 position) =>
            mask.IsWalkableMapPosition(position);

        internal Vector2 GetSceneEntryPosition(PathMask mask, Vector2 position) =>
            IsPositionWalkable(mask, position)
                ? position
                : GetNearestWalkablePosition(mask, position, checkPosition: false);

        internal static Vector2 GetNearestWalkablePosition(PathMask mask, Vector2 position)
            => GetNearestWalkablePosition(mask, position, checkPosition: true);

        static Vector2 GetNearestWalkablePosition(PathMask mask, Vector2 position,
            bool checkPosition)
        {
            if (checkPosition && position.x >= 0 && position.y >= 0 &&
                position.x < mask.Width * mask.Resolution &&
                position.y < mask.Height * mask.Resolution &&
                mask.IsWalkableMapPosition(position))
                return position;

            Vector2 nearest = position;
            long nearestDistanceSquared = long.MaxValue;
            for (int y = 0; y < mask.Height; y++)
            {
                for (int x = 0; x < mask.Width; x++)
                {
                    if (!mask[x, y])
                        continue;

                    Vector2 candidate = new Vector2(x, y) * mask.Resolution +
                        mask.Resolution / 2;
                    long differenceX = candidate.x - position.x;
                    long differenceY = candidate.y - position.y;
                    long distanceSquared = differenceX * differenceX +
                        differenceY * differenceY;
                    if (distanceSquared < nearestDistanceSquared)
                    {
                        nearest = candidate;
                        nearestDistanceSquared = distanceSquared;
                    }
                }
            }
            return nearest;
        }

        protected Vector2 CommitMovement(Vector2f position)
        {
            MovementDirection = position - movementStartPosition;
            precisePosition = position;
            return precisePosition;
        }
    }
}
