using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Data.BurnGfx;

namespace Burntime.Classic.PathFinding
{
    [Serializable]
    public class ComplexPath : PathState
    {
        sealed class WayPoint
        {
            public Vector2f Position;

            public WayPoint(Vector2f position)
            {
                Position = position;
            }
        }

        sealed class NodeComparer : IComparer<Node>
        {
            public int Compare(Node left, Node right)
            {
                return left.CompareTo(right);
            }
        }

        sealed class NodeComparerReverse : IComparer<Node>
        {
            public int Compare(Node left, Node right)
            {
                return right.TotalCost.CompareTo(left.TotalCost);
            }
        }

        sealed class Node : IComparable
        {
            public Vector2 Position;
            public Node Parent
            {
                get => parent_;
                set
                {
                    parent_ = value;
                    TotalCost = (parent_ is null) ? cost_ : parent_.TotalCost + cost_;
                }
            }
            private Node parent_;

            public float TotalCost { get; private set; }

            public float Cost
            {
                get => cost_;
                set
                {
                    cost_ = value;
                    TotalCost = (parent_ is null) ? cost_ : parent_.TotalCost + cost_;
                }
            }
            private float cost_;

            public float Remaining;

            public float Scoring
            {
                get { return TotalCost + Remaining; }
            }

            public Node(Vector2 start, Vector2 goal)
            {
                Position = start;
                Cost = 0;
                Parent = null;

                Vector2 dif = (Position - goal);
                Remaining = System.Math.Abs(dif.x) + System.Math.Abs(dif.y);
              //  Scoring = Cost + Remaining;
            }

            public Node(Node from, Vector2 toRelative, Vector2 goal)
            {
                Cost = toRelative.Length;
                Position = from.Position + toRelative;
                Parent = from;

                Vector2 dif = (Position - goal);
                Remaining = System.Math.Abs(dif.x) + System.Math.Abs(dif.y);
               // Scoring = Cost + Remaining;
            }

            public override bool Equals(object obj)
            {
                Node node = (Node)obj;
                return Position.Equals(node.Position);
            }

            public override int GetHashCode()
            {
                return Position.GetHashCode();
            }

            public int CompareTo(Node node)
            {
                return Scoring.CompareTo(node.Scoring);
            }

            int IComparable.CompareTo(object obj)
            {
                return this.CompareTo((Node)obj);
            }
        }

        sealed class CycleCounter
        {
            float counter = 0;

            public float CyclesPerSecond { get; set; } = 1;

            public int Cycles { get; private set; } = 0;
            public int CyclesTotal { get; private set; }

            public void Reset()
            {
                counter = 0;
                Cycles = 0;
                CyclesTotal = 0;
            }

            public void Process(float elapsed)
            {
                // do not process more then one second worth of cycles to prevent possible slow down
                counter += elapsed * CyclesPerSecond;
                int c = (int)System.Math.Floor(counter);
                counter -= c;
                Cycles = c;
                CyclesTotal += c;
            }
        }

        sealed class Path
        {
            List<WayPoint> wayPoints = new List<WayPoint>();
            List<WayPoint> wayPointsWalked = new List<WayPoint>();
            int undoToWayPoint = -1;

            bool finishedCalculation = false;
            Vector2f goal;
            Vector2 gridGoal;
            Vector2f position;
            Vector2f nearest;
            Node nearestNode;
            CycleCounter cycles = new CycleCounter();
            PathMask mask;
            Node lastNode;
            bool reverse;
            int scoreCap;

            Path reversePath;

            List<Node> openList;
            List<Node> closedList;

            public Vector2f Goal
            {
                get { return goal; }
            }

            public Vector2f Nearest
            {
                get { return nearest; }
            }
            
            public Vector2f NextWayPoint
            {
                get 
                {
                    if (undoToWayPoint != -1)
                        return wayPointsWalked[wayPointsWalked.Count - 1].Position;
                    return wayPoints[0].Position; 
                }
            }

            public bool IsEmpty
            {
                get { return wayPoints.Count == 0; }
            }

            public Path()
            {
                cycles.CyclesPerSecond = BurntimeClassic.Instance.Settings["game"].GetInt("pathfinding_cycles");
            }

            public bool FinishedCalculation
            {
                get { return finishedCalculation; }
            }
            
            public void Clear()
            {
                reversePath = null;

                wayPoints.Clear();
                wayPointsWalked.Clear();
                finishedCalculation = false;

                closedList = null;
                openList = null;
                mask = null;
            }

            public void Next()
            {
                if (wayPointsWalked.Count == undoToWayPoint)
                {
                    undoToWayPoint = -1;
                    return;
                }

                if (undoToWayPoint != -1)
                {
                    wayPointsWalked.RemoveAt(wayPointsWalked.Count - 1);
                    if (wayPointsWalked.Count == 0)
                        undoToWayPoint = -1;
                }
                else
                {
                    wayPointsWalked.Add(wayPoints[0]);
                    wayPoints.RemoveAt(0);
                }
            }

            public void BeginProcess(PathMask mask, Vector2f position, Vector2f moveTo, float elapsed, bool reverse)
            {
                Clear();

                this.mask = mask;
                openList = new List<Node>();
                closedList = new List<Node>();

                goal = moveTo;
                nearest = position;
                this.position = position;
                gridGoal = ToGridPoint(goal);

                this.reverse = reverse;
                if (!reverse && !mask[gridGoal])
                {

                    reversePath = new Path();
                    reversePath.BeginProcess(mask, moveTo, position, elapsed, true);
                }

                Node node = new Node(ToGridPoint(position), gridGoal);
                nearestNode = node;
                openList.Add(node);

                cycles.Reset();
                scoreCap = (int)(System.Math.Max(mask.Width, mask.Height) * 1.5f);
                Process(elapsed, position);
            }

            public void Process(float elapsed, Vector2f currentPosition)
            {
                // BeginProcess not called
                if (openList == null)
                    return;

                if (reversePath != null)
                {
                    reversePath.Process(elapsed, currentPosition);
                    if (reversePath.FinishedCalculation)
                    {
                        // reset goal and starting point
                        goal = reversePath.Nearest;
                        gridGoal = ToGridPoint(goal);

                        //foreach (Node node in closedList)
                        //{
                        //    if (node.Position == gridGoal)
                        //    {
                        Node newnode = new Node(ToGridPoint(currentPosition), gridGoal);
                        nearestNode = newnode;
                        nearest = currentPosition;
                        openList.Clear();
                        closedList.Clear();
                        openList.Add(newnode);

                        wayPointsWalked.Clear();
                        wayPoints.Clear();
                        undoToWayPoint = -1;
                                //break;
                        //    }
                        //}

                        reversePath = null;

                        if (gridGoal == ToGridPoint(currentPosition))
                        {
                            openList.Clear();
                            finishedCalculation = true;
                        }
                    }
                }

                cycles.Process(elapsed);

                // process some cycles
                for (int i = 0; i < cycles.Cycles && openList.Count > 0; i++)
                {
                    // get best scored node
                    Node first = openList[0];
                    lastNode = first;

                    // move node from open to closed list
                    closedList.Add(first);
                    openList.RemoveAt(0);

                    if (first.Remaining < nearestNode.Remaining ||
                        reverse && (first.TotalCost < nearestNode.TotalCost && first.Remaining == 0))
                    {
                        nearestNode = first;

                        // update waypoints
                        nearest = ToMapPoint(nearestNode.Position);
                        wayPoints.Clear();

                        Node trace = nearestNode;
                        while (trace != null && trace.Parent != null)
                        {
                            wayPoints.Insert(0, new WayPoint(ToMapPoint(trace.Position)));

                            trace = trace.Parent;
                        }

                        // find last common node of path walked and new path
                        int leastCommon = -1;
                        for (int j = 0; j < wayPoints.Count && j < wayPointsWalked.Count; j++)
                        {
                            if (wayPoints[j].Position == wayPointsWalked[j].Position)
                                leastCommon = j;
                            else 
                                break;
                        }

                        // remove already passed waypoints
                        for (int j = 0; j <= leastCommon; j++)
                            wayPoints.RemoveAt(0);

                        // if least common is >= 0 but smaller than the walked path, then we have to
                        // undo some way points
                        undoToWayPoint = leastCommon + 1;
                        if (undoToWayPoint >= wayPointsWalked.Count)
                            undoToWayPoint = -1;

                        // reached goal
                        if (nearestNode.Position == gridGoal)
                        {
                            openList.Clear();
                            closedList.Clear();
                            finishedCalculation = true;
                            break;
                        }
                        // propably the most near walkable positions, break early to save cpu time
                        else if (reversePath != null && reversePath.FinishedCalculation && (reversePath.Nearest - Nearest).Length < 5)
                        {
                            openList.Clear();
                            closedList.Clear();
                            finishedCalculation = true;
                            break;
                        }
                        ////
                        //else if (reverse && nearestNode.Remaining == 0)
                        //{
                        //    openList.Clear();
                        //    closedList.Clear();
                        //    finishedCalculation = true;
                        //    break;
                        //}
                    }

                    if (reverse)
                        openList.Sort(new NodeComparerReverse());
                    else
                        openList.Sort(new NodeComparer());

                    Rect bounding = new Rect(Vector2.Zero, new Vector2(mask.Width, mask.Height) * mask.Resolution);

                    // add node neighbors
                    foreach (Vector2 to in new Rect(-1, -1, 3, 3))
                    {
                        if (to.x == 0 && to.y == 0)
                            continue;

                        Node neighbor = new Node(first, to, gridGoal);

                        // out of area
                        if (!bounding.PointInside(neighbor.Position))
                            continue;

                        // neighbor is ok
                        if ((reverse && !mask[first.Position] || !reverse && mask[neighbor.Position]) && !closedList.Contains(neighbor))
                        {
                            // check if this node is already listed
                            int found = openList.IndexOf(neighbor);
                            if (found >= 0)
                            {
                                // swap nodes if the new route is cheaper
                                if (openList[found].TotalCost > neighbor.TotalCost)
                                {
                                    openList[found].Parent = first;
                                    openList[found].Cost = neighbor.Cost;
                                    if (reverse)
                                        openList[found].Remaining = mask[neighbor.Position] ? 0 : 1;
                                    //openList[found].Scoring = neighbor.Scoring;
                                }
                            }
                            else if (neighbor.Scoring < scoreCap) // node not listed, just add
                            {
                                if (reverse)
                                    neighbor.Remaining = mask[neighbor.Position] ? 0 : 1;
                                openList.Add(neighbor);
                            }
                        }
                    }
                }

                if (openList.Count == 0)
                {
                    finishedCalculation = true;
                    closedList.Clear();
                }
            }

            Vector2 ToGridPoint(Vector2f position)
            {
                return ((Vector2)position + (mask.Resolution / 2 - 1)) / mask.Resolution;
            }

            Vector2f ToMapPoint(Vector2 grid)
            {
                return grid * mask.Resolution + mask.Resolution / 2;
            }

            public void DebugRender(Burntime.Platform.Graphics.RenderTarget target, Vector2f position)
            {
                if (mask == null)
                    return;

                if (reverse)
                {
                    if (!finishedCalculation)
                    {
                        if (lastNode != null)
                        {
                            Node last = lastNode;
                            Node trace = lastNode.Parent;
                            while (trace != null && trace.Parent != null)
                            {
                                target.RenderLine(ToMapPoint(last.Position), ToMapPoint(trace.Position), new PixelColor(255, 255, 0));

                                last = trace;
                                trace = trace.Parent;
                            }
                        }
                    }

                    for (int i = 0; i < wayPoints.Count; i++)
                    {
                        if (i > 0)
                            target.RenderLine(position, wayPoints[i].Position, finishedCalculation ? new PixelColor(255, 0, 255) : new PixelColor(255, 0, 0));
                        position = wayPoints[i].Position;
                    }
                }
                else
                {
                    if (reversePath != null)
                        reversePath.DebugRender(target, position);

                    if (!finishedCalculation)
                    {
                        if (!reverse)
                            target.RenderLine(nearest, goal, new PixelColor(0, 255, 0));

                        if (lastNode != null)
                        {
                            Node last = lastNode;
                            Node trace = lastNode.Parent;
                            while (trace != null && trace.Parent != null)
                            {
                                target.RenderLine(ToMapPoint(last.Position), ToMapPoint(trace.Position), new PixelColor(255, 255, 0));

                                last = trace;
                                trace = trace.Parent;
                            }
                        }
                    }

                    for (int i = 0; i < wayPoints.Count; i++)
                    {
                        target.RenderLine(position, wayPoints[i].Position, finishedCalculation ? new PixelColor(0, 0, 255) : new PixelColor(255, 0, 0));
                        position = wayPoints[i].Position;
                    }
                }
            }
        }

        Vector2f move;
        Vector2f adjustedMove;
        public override Vector2 MoveTo
        {
            get { return new Vector2(move); }
            set 
            { 
                move = new Vector2f(value);
                adjustedMove = move;
            }
        }

        Vector2f position;

        [NonSerialized]
        Path path;

        protected override void InitInstance(object[] parameter)
        {
            base.InitInstance(parameter);
            path = new Path();
        }

        protected override void AfterDeserialization()
        {
            base.AfterDeserialization();
            path = new Path();
        }

        public override Vector2 Process(PathMask mask, Vector2 position, float elapsed)
        {
            this.position = position;
            Vector2f dif = move - this.position;

            float elapsedSpeed =  speed* elapsed;

            // goal reached
            if (dif.Length < 0.1f)
            {
                path.Clear();
                return position;
            }

            // moveto position differs from active path goal, recalculate path
            if (adjustedMove != path.Goal)
                path.BeginProcess(mask, this.position, adjustedMove, elapsed, false);
            // in case path finding is not finished yet do further processing
            else if (!path.FinishedCalculation)
                path.Process(elapsed, this.position);

            // in case the goal was adjusted copy to move
            adjustedMove = path.Goal;

            //// wait until path is calculated
            //if (!path.FinishedCalculation)
            //    return this.position;

            // process way points
            while (elapsedSpeed > 0 && !path.IsEmpty)
            {
                Vector2f wayPoint = path.NextWayPoint;
                dif = wayPoint - this.position;
                
                // we will not reach the next waypoint, calculate position
                if (dif.Length > elapsedSpeed)
                {
                    dif.Normalize();
                    this.position += dif * elapsedSpeed;
                    elapsedSpeed = 0;
                }
                // otherwise walk path and remove waypoint from path queue
                else
                {
                    elapsedSpeed -= dif.Length;
                    path.Next();
                }
            }

            return this.position;
        }

        public override void DebugRender(Burntime.Platform.Graphics.RenderTarget target)
        {
            if (position != move)
                path.DebugRender(target, position);
        }
    }
}
