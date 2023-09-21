using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Classic.Logic
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
        
        public override void Die()
        {
            if (Player.Type == PlayerType.Human && BurntimeClassic.Instance.Settings["debug"].GetBool("godmode") && BurntimeClassic.Instance.Settings["debug"].GetBool("enable_cheats"))
                health = 100; // magic!
            else
                base.Die();
        }
    }
}
