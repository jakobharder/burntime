using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace Burntime.Launcher
{
    public class AssemblyChecker
    {
        [STAThread]
        static void Main(string[] args)
        {
            CustomExceptionHandler handler = new CustomExceptionHandler();

            // automatically catch exceptions in release builds
#if !(DEBUG)
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(handler.OnThreadException);
#endif
            //AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);

            string pak = null;
            if (args.Length > 0)
                pak = args[0];
            Program.Run(pak);
        }

        static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // show message and quit if assembly was not found
            string name = new System.Reflection.AssemblyName(args.Name).Name + ".dll";
            
            throw new Exception("Could not find " + name + "!");
        }
    }
}
