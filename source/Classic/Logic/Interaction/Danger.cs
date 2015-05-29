
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

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

        public Sprite InfoIcon
        {
            get { return infoIcon; }
        }

        public string Type
        {
            get { return type; }
        }

        protected string infoString;
        protected int infoValue;
        protected Sprite infoIcon;
        protected string type;

        public Danger(string type, int value, string str, Sprite icon)
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
