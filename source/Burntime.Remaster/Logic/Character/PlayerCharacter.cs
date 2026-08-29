using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Remaster.Logic
{
    [Serializable]
    public class PlayerCharacter : Character
    {
        protected string name;

        // overwrite name
        public override string Name
        {
            get { return name; }
            set { name = value; }
        }

        // AI bosses can bank one complete meat ration above the classic food
        // limit. Followers and human-controlled bosses retain the classic cap.
        public override int MaxFood => Player?.Type == PlayerType.Ai ? 14 : 9;
        
        public override void Die()
        {
            if (Player.Type == PlayerType.Human && BurntimeClassic.Instance.Settings["debug"].GetBool("godmode") && BurntimeClassic.Instance.Settings["debug"].GetBool("enable_cheats"))
                health = 100; // magic!
            else
                base.Die();
        }
    }
}
