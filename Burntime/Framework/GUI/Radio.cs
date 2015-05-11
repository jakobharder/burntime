using System;

namespace Burntime.Framework.GUI
{
    public enum RadioMode
    {
        Normal,
        Round
    }

    public class Radio : Switch
    {
        public RadioMode Mode;

        public Radio(Module App)
            : base(App)
        {
        }

        public int Value
        {
            get
            {
                if (parent == null)
                    return IsDown ? 0 : -1;
                else
                {
                    Window[] group = parent.Windows.GetGroup(Group);
                    int value = 0;
                    foreach (Window window in group)
                    {
                        if (window is Radio)
                        {
                            if ((window as Radio).IsDown)
                                return value;
                            value++;
                        }
                    }

                    return IsDown ? 0 : -1;
                }
            }
        }

        public override void OnSwitchDown()
        {
            if (parent == null)
                return;

            Window[] group = parent.Windows.GetGroup(Group);
            foreach (Window window in group)
            {
                if (window == this)
                    continue;

                if (window is Radio)
                {
                    Radio radio = window as Radio;
                    radio.IsDown = false;
                }
            }
        }

        public override bool OnButtonClick()
        {
            if (Mode == RadioMode.Normal)
            {
                isDown = true;
            }
            else if (Mode == RadioMode.Round)
            {
                if (isDown || parent == null)
                    return true;

                bool found = false;
                bool me = false;

                Window[] group = parent.Windows.GetGroup(Group);
                foreach (Window window in group)
                {
                    if (window == this)
                    {
                        me = true;
                        continue;
                    }

                    if (window is Radio && me)
                    {
                        Radio radio = window as Radio;
                        radio.IsDown = true;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    foreach (Window window in group)
                    {
                        if (window is Radio)
                        {
                            Radio radio = window as Radio;
                            radio.IsDown = true;
                            break;
                        }
                    }
                }
            }

            return true;
        }
    }
}
