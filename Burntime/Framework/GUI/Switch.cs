using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Framework.Event;

namespace Burntime.Framework.GUI
{
    public class Switch : Button
    {
        public CommandEvent DownCommand;
        public CommandEvent UpCommand;

        public Switch(Module App)
            : base(App)
        {
        }

        public bool IsDown
        {
            get { return isDown; }
            set 
            { 
                isDown = value;
                if (isDown)
                    OnSwitchDown();
                else
                    OnSwitchUp();
            }
        }

        public override bool OnMouseClick(Burntime.Platform.Vector2 Position, Burntime.Platform.MouseButton Button)
        {
            isDown = !isDown;
            if (isDown)
                OnSwitchDown();
            else
                OnSwitchUp();
            OnButtonClick();

            return true;
        }

        public virtual void OnSwitchDown()
        {
            if (DownCommand != null)
                DownCommand.Execute();
        }

        public virtual void OnSwitchUp()
        {
            if (UpCommand != null)
                UpCommand.Execute();
        }
    }
}
