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
    }
}
