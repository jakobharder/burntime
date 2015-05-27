using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

namespace Burntime.Classic
{
    class Launcher
    {
        static public void Run(bool debugMode)
        {
            Burntime.Autoupdate.Updater updater = new Burntime.Autoupdate.Updater(debugMode ? "../" : "");
            if (updater.IsUpdateMode)
            {
                updater.Update();

                ProcessStartInfo startInfo = new ProcessStartInfo("classiclauncher.exe");
                startInfo.WorkingDirectory = System.IO.Path.GetFullPath("../");
                Process.Start(startInfo); 
            }
            else
            {
                ClassicLauncher dlg = new ClassicLauncher(debugMode);
                dlg.Show();
                Application.Run();

                if (debugMode)
                {
                    if (dlg.DebugStart)
                    {
                        DebugLaunch debug = new DebugLaunch();
                        debug.Run();
                    }
                }
            }
        }
    }
}
