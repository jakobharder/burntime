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

        protected StateLink<Character> leader;
        [NonSerialized]
        protected double followAngle;
        [NonSerialized]
        protected bool hasFollowAngle;

        public Character Leader
        {
            get { return leader; }
            set { leader = value; }
        }

        private Vector2 GetFollowTarget(int radius)
        {
            // Keep the same formation direction between path refreshes. The flag
            // also allows minds from older saves to initialize the angle lazily.
            if (!hasFollowAngle)
            {
                followAngle = (Burntime.Platform.Math.Random.Next() % 360) * System.Math.PI / 180;
                hasFollowAngle = true;
            }

            Vector2 offset;
            offset.x = (int)(System.Math.Sin(followAngle) * radius);
            offset.y = (int)(System.Math.Cos(followAngle) * radius);
            return Leader.Position + offset;
        }

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
                Vector2 followTarget = GetFollowTarget(14);
                distance = (followTarget - Owner.Path.MoveTo).Length;

                // update path only if leader position and own destination are too far away
                if (distance > 20)
                    Owner.Path.MoveTo = followTarget;
            }
            else if (distance < 15)
            {
                Vector2 followTarget = GetFollowTarget(14);
                distance = (followTarget - Owner.Path.MoveTo).Length;

                // settle into the follower's stable formation position
                if (distance > 1)
                    Owner.Path.MoveTo = followTarget;
            }
        }
    }
}
