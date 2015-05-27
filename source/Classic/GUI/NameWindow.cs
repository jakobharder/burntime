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
