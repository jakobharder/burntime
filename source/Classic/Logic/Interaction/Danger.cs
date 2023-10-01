using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Framework.States;
using Burntime.Platform.Resource;
using Burntime.Platform.Graphics;
using Burntime.Framework;

namespace Burntime.Classic.Logic.Interaction
{
    public class Danger : DataObject
    {
        public float HealthDecrease
        {
            get { return type == "radiation" ? 1.35f : 0.5f; }
        }

        public string InfoString
        {
            get { return infoString; }
        }

        public ISprite InfoIcon
        {
            get { return infoIcon; }
        }

        public string Type
        {
            get { return type; }
        }

        protected string infoString;
        protected int infoValue;
        protected ISprite infoIcon;
        protected string type;

        public Danger(string type, int value, string str, ISprite icon)
        {
            this.type = type;
            infoValue = value;
            infoString = str.Replace("|E", value.ToString());
            infoIcon = icon;
        }

        static public Danger Instance(string type, int value)
        {
            return (Danger)BurntimeClassic.Instance.ResourceManager.GetData("danger@" + type + "??" + value);
        }
    }
}
