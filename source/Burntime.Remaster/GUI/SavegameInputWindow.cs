using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Framework;
using Burntime.Framework.GUI;

namespace Burntime.Remaster
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
            string caret = textInputVisuallyActive ? "{_" : "";
            string filename = name + caret + ".SAV";
            Text = savegameModeStrings[(int)savegameMode].Replace("|A", filename);
        }

        string[] savegameModeStrings = new string[4];

        public int MaxNameLength = 8;
        public Action? ActivationAction { get; set; }
        bool textInputActive;
        bool textInputVisuallyActive;
        public bool IsTextInputActive
        {
            get => textInputActive;
            set
            {
                textInputActive = value;
                if (!value)
                    textInputVisuallyActive = false;
                else if (!textInputVisuallyActive)
                    textInputVisuallyActive = true;
                HasFocus = value;
                RefreshText();
            }
        }
        public bool IsTextInputVisuallyActive
        {
            get => textInputVisuallyActive;
            set
            {
                textInputVisuallyActive = value && textInputActive;
                RefreshText();
            }
        }
        public override bool WantsTextInput => textInputActive;

        public SavegameInputWindow(Module App)
            : base(App)
        {
            savegameModeStrings[0] = "|A";
            savegameModeStrings[1] = App.ResourceManager.GetString("burn?382") + " |A ...";
            savegameModeStrings[2] = App.ResourceManager.GetString("burn?383") + " |A ...";
            savegameModeStrings[3] = App.ResourceManager.GetString("burn?384") + " |A ...";
            Name = "";
            IsTextInputActive = true;
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

        public override bool OnButtonClick()
        {
            ActivationAction?.Invoke();
            return true;
        }
    }
}
