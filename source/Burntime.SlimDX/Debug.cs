using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Platform
{
    public static class Debug
    {
        static internal DebugForm form;

        static public void SetInfo(string name, string info)
        {
            if (form == null)
                return;

            form.SetInfo(name, info);
        }

        static public void SetInfoMB(string name, int bytes)
        {
            if (form == null)
                return;

            form.SetInfo(name, ((float)bytes / 1024 / 1024).ToString("F02") + " MB");
        }
    }
}
