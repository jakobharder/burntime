using System;
using Burntime.Classic.Logic;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform;

namespace Burntime.Classic.AI
{
    [Serializable]
    class FellowerMind : CharacterMind
    {
        protected StateLink<Character> leader;
        public Character Leader
        {
            get { return leader; }
            set { leader = value; }
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
            
            // if too far from leader, then follow
            if (distance > 150)
            {
                distance = (Leader.Position - Owner.Path.MoveTo).Length;

                // update path only if leader position and own destination are too far away
                if (distance > 140)
                {
                    double r = (Burntime.Platform.Math.Random.Next() % 360) * System.Math.PI / 180;
                    Vector2 margin;
                    margin.x = (int)(System.Math.Sin(r) * 14);
                    margin.y = (int)(System.Math.Cos(r) * 14);

                    Owner.Path.MoveTo = Leader.Position;// +margin;
                }
            }
            else if (distance > 30)
            {
                distance = (Leader.Position - Owner.Path.MoveTo).Length;

                // update path only if leader position and own destination are too far away
                if (distance > 20)
                {
                    double r = (Burntime.Platform.Math.Random.Next() % 360) * System.Math.PI / 180;
                    Vector2 margin;
                    margin.x = (int)(System.Math.Sin(r) * 14);
                    margin.y = (int)(System.Math.Cos(r) * 14);

                    Owner.Path.MoveTo = Leader.Position;// +margin;
                }
            }
            else if (distance < 15)
            {
                distance = (Leader.Position - Owner.Path.MoveTo).Length;

                // update path only if leader position and own destination are too close
                if (distance < 10)
                {
                    double r = (Burntime.Platform.Math.Random.Next() % 360) * System.Math.PI / 180;
                    Vector2 margin;
                    margin.x = (int)(System.Math.Sin(r) * 14);
                    margin.y = (int)(System.Math.Cos(r) * 14);

                    Owner.Path.MoveTo = Leader.Position + margin;
                }
            }
        }
    }
}
