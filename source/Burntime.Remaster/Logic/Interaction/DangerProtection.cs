using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Framework.States;
using Burntime.Platform.Resource;
using Burntime.Platform.Graphics;
using Burntime.Framework;

namespace Burntime.Remaster.Logic.Interaction
{
    public class DangerProtection : DataObject
    {
        public float Rate
        {
            get { return rate; }
        }

        public string Type
        {
            get { return type; }
        }

        protected float rate;
        protected string type;

        public DangerProtection(string type, float rate)
        {
            this.type = type;
            this.rate = rate;
        }

        static public DangerProtection Instance(string type, float rate)
        {
            return (DangerProtection)BurntimeClassic.Instance.ResourceManager.GetData("protection@" + type + "??" + (int)(rate * 100));
        }
    }
}
