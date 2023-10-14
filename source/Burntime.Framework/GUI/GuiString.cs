using System;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.Resource;

namespace Burntime.Framework.GUI
{
    public class GuiString
    {
        protected string str;

        public static implicit operator GuiString(string id)
        {
            if (id?.StartsWith("@") == true)
                return new GuiString(Module.Instance.ResourceManager.GetString(id.Substring(1)));
            return new GuiString(id);
        }

        public GuiString(string str)
        {
            if (str?.StartsWith("@") == true)
                this.str = Module.Instance.ResourceManager.GetString(str.Substring(1));
            else
                this.str = str;
        }

        public static implicit operator string(GuiString right)
        {
            return right.str;
        }
    }
}
