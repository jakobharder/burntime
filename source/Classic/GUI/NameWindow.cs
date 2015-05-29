
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

using Burntime.Platform.IO;
using Burntime.Framework;
using Burntime.Framework.GUI;

namespace Burntime.Classic
{
    public class NameWindow : Switch
    {
        ConfigFile table;

        string name;
        public String Name
        {
            get { return name; }
            set 
            { 
                name = value;
                Text = "[ " + name + " ]";
            }
        }

        public int MaxNameLength = 10;

        public ConfigFile Table
        {
            get { return table; }
            set { table = value; }
        }

        public NameWindow(Module App)
            : base(App)
        {
            Name = "";
        }

        public override void OnSwitchDown()
        {
            HasFocus = true;
            base.OnSwitchDown();
        }

        public override void OnSwitchUp()
        {
            HasFocus = false;
            base.OnSwitchUp();
        }

        public override bool OnKeyPress(char Key)
        {
            if (Font.IsSupportetCharacter(Key) || Key == 8)
            {
                if (Key == 8)
                {
                    if (name.Length > 0)
                        Name = name.Substring(0, name.Length - 1);
                }
                else
                {
                    if (name.Length < MaxNameLength)
                        Name += Key;

                    // convert last characters (3, 2, 1)

                    for (int i = 3; i > 0; i--)
                    {
                        if (Name.Length >= i)
                        {
                            if (table[""].ContainsKey(Name.Substring(Name.Length - i)))
                                Name = Name.Substring(0, Name.Length - i) + table[""].Get(Name.Substring(Name.Length - i));
                        }
                    }
                }
            }

            return true;
        }
    }
}
