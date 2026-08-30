using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform.IO;
using Burntime.Framework;
using Burntime.Framework.GUI;

namespace Burntime.Remaster
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
                RefreshText();
            }
        }

        public int MaxNameLength = 10;
        public bool HasManualName { get; private set; }
        public event Action? TextInputDeactivated;

        public void SetAutomaticName(string value)
        {
            HasManualName = false;
            Name = value;
        }

        bool textInputActive;
        public bool IsTextInputActive
        {
            get => textInputActive;
            set
            {
                bool wasActive = textInputActive;
                textInputActive = value;
                HasFocus = value;
                RefreshText();

                if (wasActive && !value)
                    TextInputDeactivated?.Invoke();
            }
        }
        public override bool WantsTextInput => textInputActive;

        bool caretVisible;
        bool ShouldShowCaret => textInputActive &&
            app.LastInputMode is InputMode.Mouse or InputMode.Keyboard;

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
            IsTextInputActive = true;
            base.OnSwitchDown();
        }

        public override void OnSwitchUp()
        {
            IsTextInputActive = false;
            base.OnSwitchUp();
        }

        void RefreshText()
        {
            caretVisible = ShouldShowCaret;
            string value = name + (caretVisible ? "{_" : "");
            Text = "[ " + value + " ]";
        }

        public override void OnUpdate(float elapsed)
        {
            if (caretVisible != ShouldShowCaret)
                RefreshText();

            base.OnUpdate(elapsed);
        }

        public override bool OnKeyPress(char Key)
        {
            if (Font.IsSupportetCharacter(Key) || Key == 8)
            {
                if (Key == 8)
                {
                    if (name.Length > 0)
                    {
                        Name = name.Substring(0, name.Length - 1);
                        HasManualName = name.Length > 0;
                    }
                }
                else
                {
                    if (name.Length < MaxNameLength)
                    {
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

                        HasManualName = true;
                    }
                }
            }

            return true;
        }
    }
}
