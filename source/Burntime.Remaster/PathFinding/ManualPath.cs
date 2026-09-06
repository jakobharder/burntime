using System.Collections.Generic;
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
        [System.NonSerialized]
        int slideSide;
        [System.NonSerialized]
        Queue<Vector2f>? detourWaypoints;
        [System.NonSerialized]
        Vector2 failedDetourPosition;
        [System.NonSerialized]
        bool hasFailedDetour;

        public Vector2f Direction
        {
            get => direction;
            set
            {
                Vector2f newDirection = value;
                if (newDirection != Vector2f.Zero)
                    newDirection.Normalize();
                if (newDirection != direction)
                {
                    slideSide = 0;
                    detourWaypoints?.Clear();
                    hasFailedDetour = false;
                }
                direction = newDirection;
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
            Vector2f manualPosition = BeginMovement(position);
            float distance = speed * elapsed;

            // Keep movement on the same per-update integer raster as ComplexPath,
            // while checking long frames in small steps so narrow boundaries are
            // not skipped.
            while (distance > 0 && direction != Vector2f.Zero)
            {
                float stepDistance = System.Math.Min(distance, 1);

                // A small concave obstacle may require briefly moving away from
                // the held direction. Keep following that escape route until it
                // is complete instead of retrying the stick direction each frame.
                if (detourWaypoints?.Count > 0)
                {
                    if (!MoveAlongDetour(mask, ref manualPosition, stepDistance))
                        break;

                    distance -= stepDistance;
                    continue;
                }

                Vector2f step = direction * stepDistance;
                Vector2f fullPosition = manualPosition + step;

                if (IsWalkable(mask, fullPosition))
                {
                    manualPosition = fullPosition;
                    slideSide = 0;
                    hasFailedDetour = false;
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
                    {
                        int side = GetSlideSide(mask, manualPosition, stepDistance);
                        if (side == 0)
                        {
                            if (!TryBeginDetour(mask, manualPosition) ||
                                !MoveAlongDetour(mask, ref manualPosition, stepDistance))
                                break;

                            distance -= stepDistance;
                            continue;
                        }

                        Vector2f slideDirection = Rotate90(direction, side);
                        Vector2f slidePosition = manualPosition + slideDirection * stepDistance;
                        if (!IsWalkable(mask, slidePosition))
                        {
                            slideSide = 0;
                            break;
                        }

                        manualPosition = slidePosition;
                    }
                }

                distance -= stepDistance;
            }

            this.position = CommitMovement(manualPosition);
            moveTo = this.position;
            return this.position;
        }

        bool TryBeginDetour(PathMask mask, Vector2f start)
        {
            const int MaximumRadiusInCells = 4;
            const int MaximumPathLength = MaximumRadiusInCells * 3;

            Vector2 rasterStart = start;
            if (hasFailedDetour && failedDetourPosition == rasterStart)
                return false;

            Vector2 startCell = GetMaskPosition(mask, start);
            if (startCell.x < 0 || startCell.y < 0 ||
                startCell.x >= mask.Width || startCell.y >= mask.Height)
                return false;

            int cellCount = mask.Width * mask.Height;
            int[] previous = new int[cellCount];
            int[] depth = new int[cellCount];
            System.Array.Fill(previous, -2);

            int startIndex = startCell.x + startCell.y * mask.Width;
            previous[startIndex] = -1;
            Queue<int> open = new();
            open.Enqueue(startIndex);
            int goalIndex = -1;
            Vector2[] neighbors =
            {
                new Vector2(1, 0), new Vector2(-1, 0),
                new Vector2(0, 1), new Vector2(0, -1)
            };

            while (open.Count > 0 && goalIndex == -1)
            {
                int currentIndex = open.Dequeue();
                if (depth[currentIndex] >= MaximumPathLength)
                    continue;

                Vector2 current = new(
                    currentIndex % mask.Width, currentIndex / mask.Width);
                foreach (Vector2 neighborOffset in neighbors)
                {
                    Vector2 neighbor = current + neighborOffset;
                    if (System.Math.Abs(neighbor.x - startCell.x) > MaximumRadiusInCells ||
                        System.Math.Abs(neighbor.y - startCell.y) > MaximumRadiusInCells ||
                        !mask[neighbor])
                        continue;

                    int neighborIndex = neighbor.x + neighbor.y * mask.Width;
                    if (neighborIndex < 0 || neighborIndex >= cellCount ||
                        previous[neighborIndex] != -2)
                        continue;

                    previous[neighborIndex] = currentIndex;
                    depth[neighborIndex] = depth[currentIndex] + 1;
                    if (IsDetourExit(mask, start, neighbor))
                    {
                        goalIndex = neighborIndex;
                        break;
                    }

                    open.Enqueue(neighborIndex);
                }
            }

            if (goalIndex == -1)
            {
                failedDetourPosition = rasterStart;
                hasFailedDetour = true;
                return false;
            }

            List<Vector2f> reversedPath = new();
            for (int index = goalIndex; index != startIndex; index = previous[index])
            {
                Vector2 cell = new(index % mask.Width, index / mask.Width);
                reversedPath.Add(GetMapPosition(mask, cell));
            }

            detourWaypoints = new Queue<Vector2f>();
            for (int i = reversedPath.Count - 1; i >= 0; i--)
                detourWaypoints.Enqueue(reversedPath[i]);
            slideSide = 0;
            hasFailedDetour = false;
            return detourWaypoints.Count > 0;
        }

        bool IsDetourExit(PathMask mask, Vector2f start, Vector2 cell)
        {
            Vector2f position = GetMapPosition(mask, cell);
            Vector2f displacement = position - start;
            float forwardProgress = displacement.x * direction.x +
                displacement.y * direction.y;
            if (forwardProgress < 0)
                return false;

            // Require two clear cells in the requested direction. This prevents
            // selecting a point inside the pocket that would immediately send
            // the character back into the same collision.
            return IsWalkable(mask, position + direction * mask.Resolution) &&
                IsWalkable(mask, position + direction * (mask.Resolution * 2));
        }

        bool MoveAlongDetour(PathMask mask, ref Vector2f position, float stepDistance)
        {
            while (detourWaypoints?.Count > 0)
            {
                Vector2f difference = detourWaypoints.Peek() - position;
                float distance = difference.Length;
                if (distance < 0.01f)
                {
                    detourWaypoints.Dequeue();
                    continue;
                }

                difference.Normalize();
                Vector2f nextPosition = position +
                    difference * System.Math.Min(distance, stepDistance);
                if (!IsWalkable(mask, nextPosition))
                {
                    detourWaypoints.Clear();
                    return false;
                }

                position = nextPosition;
                if (distance <= stepDistance)
                    detourWaypoints.Dequeue();
                return true;
            }

            return false;
        }

        int GetSlideSide(PathMask mask, Vector2f start, float stepDistance)
        {
            if (slideSide != 0)
            {
                Vector2f continuedSlide = start +
                    Rotate90(direction, slideSide) * stepDistance;
                if (IsWalkable(mask, continuedSlide))
                    return slideSide;

                slideSide = 0;
            }

            int positiveDistance = GetForwardClearanceDistance(mask, start, 1);
            int negativeDistance = GetForwardClearanceDistance(mask, start, -1);
            if (positiveDistance == int.MaxValue && negativeDistance == int.MaxValue)
                return 0;

            slideSide = positiveDistance <= negativeDistance ? 1 : -1;
            return slideSide;
        }

        int GetForwardClearanceDistance(PathMask mask, Vector2f start, int side)
        {
            Vector2f slideDirection = Rotate90(direction, side);
            Vector2f probe = start;
            int maximumDistance = mask.Resolution * 4;

            for (int distance = 1; distance <= maximumDistance; distance++)
            {
                probe += slideDirection;
                if (!IsWalkable(mask, probe))
                    return int.MaxValue;
                if (IsWalkable(mask, probe + direction))
                    return distance;
            }

            return int.MaxValue;
        }

        static Vector2f Rotate90(Vector2f vector, int side) => side > 0
            ? new Vector2f(-vector.y, vector.x)
            : new Vector2f(vector.y, -vector.x);

        static Vector2 GetMaskPosition(PathMask mask, Vector2f position) =>
            ((Vector2)position + (mask.Resolution / 2 - 1)) / mask.Resolution;

        static Vector2f GetMapPosition(PathMask mask, Vector2 cell) =>
            cell * mask.Resolution + mask.Resolution / 2;

        static bool IsWalkable(PathMask mask, Vector2f position)
        {
            if (position.x < 0 || position.y < 0)
                return false;

            // Use the same centered mask-cell sampling as ComplexPath. Some
            // original entry points sit just over a blocked cell boundary when
            // floor-sampled, even though pathfinding places them in the adjacent
            // walkable cell.
            return mask[GetMaskPosition(mask, position)];
        }

        public override void DebugRender(RenderTarget target)
        {
        }
    }
}
