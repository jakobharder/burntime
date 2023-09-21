using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Framework;
using Burntime.Framework.GUI;

namespace Burntime.Classic
{
    public enum SavegameMode
    {
        None = 0,
        Load = 1,
        Save = 2,
        Delete = 3
    }

    public class SavegameInputWindow : Button
    {
        string name = "";
        public String Name
        {
            get { return name; }
            set
            {
                name = value;
                RefreshText();
            }
        }

        SavegameMode savegameMode = SavegameMode.None;
        public SavegameMode Mode
        {
            get { return savegameMode; }
            set
            {
                savegameMode = value;
                RefreshText();
            }
        }

        void RefreshText()
        {
            text = savegameModeStrings[(int)savegameMode].Replace("|A", name + ".SAV");
        }

        string[] savegameModeStrings = new string[4];

        public int MaxNameLength = 8;

        public SavegameInputWindow(Module App)
            : base(App)
        {
            savegameModeStrings[0] = "|A";
            savegameModeStrings[1] = App.ResourceManager.GetString("burn?385");
            savegameModeStrings[2] = App.ResourceManager.GetString("burn?386");
            savegameModeStrings[3] = App.ResourceManager.GetString("burn?387");
            Name = "";
            HasFocus = true;
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
                }
            }

            return true;
        }
    }
}
