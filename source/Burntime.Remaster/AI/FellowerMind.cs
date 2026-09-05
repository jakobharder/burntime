using System;
using Burntime.Remaster.Logic;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform;

namespace Burntime.Remaster.AI
{
    [Serializable]
    class FellowerMind : CharacterMind
    {
        const float CatchUpDistance = 40;
        const float CatchUpSpeedMultiplier = 1.2f;
        const float FollowStartStagger = 0.025f;
        const int FollowStartJitterMilliseconds = 31;
        const int ManualFollowLookAhead = 24;
        const float ManualFollowStopDelay = 1;

        protected StateLink<Character> leader;
        [NonSerialized]
        int formationRadius;
        [NonSerialized]
        Vector2 lastLeaderDestination;
        [NonSerialized]
        bool hasLeaderDestination;
        [NonSerialized]
        float followStartDelay;
        [NonSerialized]
        bool manualFollowActive;
        [NonSerialized]
        float manualFollowStartDelay;
        [NonSerialized]
        float manualFollowStopElapsed;
        [NonSerialized]
        Vector2f lastManualFollowDirection;
        [NonSerialized]
        bool hasManualFollowDirection;

        public Character Leader
        {
            get { return leader; }
            set { leader = value; }
        }

        private Vector2 GetFollowTarget(Vector2 center, int radius)
        {
            int followerIndex = 0;
            int followerCount = 0;
            foreach (Character character in Leader.GetGroup())
            {
                if (character == Leader)
                    continue;
                if (character == Owner)
                    followerIndex = followerCount;
                followerCount++;
            }

            // Distribute followers evenly, rotated by 45 degrees so a full
            // group occupies the diagonal slots instead of the cardinal axes.
            double followAngle = System.Math.PI / 4 + 2 * System.Math.PI *
                followerIndex / System.Math.Max(1, followerCount);
            Vector2 offset;
            offset.x = (int)(System.Math.Sin(followAngle) * radius);
            offset.y = (int)(System.Math.Cos(followAngle) * radius);
            return center + offset;
        }

        private Vector2 GetFollowTarget(int radius) =>
            GetFollowTarget(Leader.Position, radius);

        int FormationRadius
        {
            get
            {
                if (formationRadius == 0)
                    formationRadius = Burntime.Platform.Math.Random.Next(14, 21);
                return formationRadius;
            }
        }

        int GetFollowerIndex()
        {
            int index = 0;
            foreach (Character character in Leader.GetGroup())
            {
                if (character == Leader)
                    continue;
                if (character == Owner)
                    return index;
                index++;
            }
            return index;
        }

        float GetFollowStartDelay() =>
            FollowStartStagger * (GetFollowerIndex() + 1) +
            Burntime.Platform.Math.Random.Next(0, FollowStartJitterMilliseconds) / 1000f;

        protected override void InitInstance(object[] parameter)
        {
            if (parameter == null || parameter.Length < 2 || !(parameter[1] is Character))
                throw new InvalidStateObjectConstruction(this);

            leader = parameter[1] as Character;

            base.InitInstance(parameter);
        }

        public override void Process(float elapsed)
        {
            if (Leader.Player.SingleMode)
                return;

            float distance = (Leader.Position - Owner.Position).Length;

            // Matching the leader's speed is not enough to close a gap caused by
            // pathing or formation changes. Give followers a small boost only
            // while they are outside their normal formation range.
            if (distance > CatchUpDistance)
                Owner.Path.Speed = System.Math.Max(Owner.Path.Speed,
                    Leader.Path.Speed * CatchUpSpeedMultiplier);

            // Directional input has no stable final destination. Let the boss
            // move freely inside the group's safety distance; once a follower
            // falls outside it, follow a short projected formation anchor.
            if (Leader.Path is PathFinding.ManualPath manualPath &&
                manualPath.Direction != Vector2f.Zero)
            {
                hasLeaderDestination = false;
                followStartDelay = 0;
                manualFollowStopElapsed = 0;

                if (!manualFollowActive)
                {
                    if (distance <= CatchUpDistance)
                    {
                        if (Owner.Path.MoveTo != Owner.Position)
                            Owner.Path.MoveTo = Owner.Position;
                        return;
                    }

                    manualFollowActive = true;
                    manualFollowStartDelay = GetFollowStartDelay();
                    hasManualFollowDirection = false;
                }

                if (manualFollowStartDelay > 0)
                {
                    manualFollowStartDelay = System.Math.Max(0,
                        manualFollowStartDelay - elapsed);
                    return;
                }

                Owner.Path.Speed = System.Math.Max(Owner.Path.Speed,
                    Leader.Path.Speed * CatchUpSpeedMultiplier);

                Vector2f projectedOffset = manualPath.Direction * ManualFollowLookAhead;
                Vector2 projectedCenter = Leader.Position + new Vector2(
                    (int)System.Math.Round(projectedOffset.x),
                    (int)System.Math.Round(projectedOffset.y));
                Vector2 followTarget = GetFollowTarget(projectedCenter, FormationRadius);
                bool directionChanged = !hasManualFollowDirection ||
                    manualPath.Direction != lastManualFollowDirection;
                lastManualFollowDirection = manualPath.Direction;
                hasManualFollowDirection = true;

                if (Owner.Path is PathFinding.ComplexPath complexPath)
                    complexPath.UpdateMovingTarget(followTarget, directionChanged);
                else
                    Owner.Path.MoveTo = followTarget;
                return;
            }

            // Brief directional-input releases are common. Keep the current
            // follow run alive and reset its safety distance only after the boss
            // has remained stopped for a full second.
            if (manualFollowActive)
            {
                if (Leader.Path is PathFinding.ManualPath)
                {
                    manualFollowStopElapsed += elapsed;
                    if (manualFollowStopElapsed < ManualFollowStopDelay)
                    {
                        Owner.Path.Speed = System.Math.Max(Owner.Path.Speed,
                            Leader.Path.Speed * CatchUpSpeedMultiplier);
                        return;
                    }
                }

                manualFollowActive = false;
                manualFollowStartDelay = 0;
                manualFollowStopElapsed = 0;
                hasManualFollowDirection = false;
                Vector2 followTarget = GetFollowTarget(FormationRadius);
                if ((followTarget - Owner.Path.MoveTo).Length > 1)
                    Owner.Path.MoveTo = followTarget;
                return;
            }

            manualFollowStartDelay = 0;
            manualFollowStopElapsed = 0;

            // A clicked destination is stable, unlike the leader's moving
            // position. Start toward the final formation immediately and avoid
            // repeatedly restarting ComplexPath while the leader walks there.
            // Very distant followers keep using the recovery behavior below.
            if (distance <= 150 && Leader.Path is PathFinding.ComplexPath &&
                Leader.Path.MoveTo != Leader.Position)
            {
                Vector2 leaderDestination = Leader.Path.MoveTo;
                if (!hasLeaderDestination || leaderDestination != lastLeaderDestination)
                {
                    lastLeaderDestination = leaderDestination;
                    hasLeaderDestination = true;
                    followStartDelay = GetFollowStartDelay();
                }

                if (followStartDelay > 0)
                {
                    followStartDelay = System.Math.Max(0, followStartDelay - elapsed);
                    return;
                }

                Vector2 followTarget = GetFollowTarget(leaderDestination, FormationRadius);
                if ((followTarget - Owner.Path.MoveTo).Length > 1)
                    Owner.Path.MoveTo = followTarget;
                return;
            }

            hasLeaderDestination = false;
            followStartDelay = 0;
            
            // if too far from leader, then follow
            if (distance > 150)
            {
                Vector2 followTarget = GetFollowTarget(28);
                distance = (followTarget - Owner.Path.MoveTo).Length;

                // update path only if leader position and own destination are too far away
                if (distance > 120)
                    Owner.Path.MoveTo = followTarget;
            }
            else if (distance > CatchUpDistance)
            {
                Vector2 followTarget = GetFollowTarget(FormationRadius);
                distance = (followTarget - Owner.Path.MoveTo).Length;

                // update path only if leader position and own destination are too far away
                if (distance > 20)
                    Owner.Path.MoveTo = followTarget;
            }
            else if (distance < 15)
            {
                Vector2 followTarget = GetFollowTarget(FormationRadius);
                distance = (followTarget - Owner.Path.MoveTo).Length;

                // settle into the follower's stable formation position
                if (distance > 1)
                    Owner.Path.MoveTo = followTarget;
            }
        }
    }
}
