﻿using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;
using Burntime.Framework;
//using System.IO;

namespace Burntime.Game
{
    public class GameStarter
    {
        public void Run(String PakName)
        {
            Log.Initialize("log.txt");
            if (PakName != null)
                PakName = PakName.ToLower();

            FileSystem.BasePath = "../";
            FileSystem.AddPackage("system", "system");
            
            ConfigFile engineSettings = new ConfigFile();
            engineSettings.Open("system:settings.txt");

            Log.DebugOut = engineSettings["engine"].GetBool("debug");

            AssemblyControl assemblyControl = new AssemblyControl(AppDomain.CurrentDomain);
            PackageManager paketManager = new PackageManager("game/");

            // check if package is available
            string str = PakName;
            PakName = null;
            foreach (PackageInfo info in paketManager.PackageInfos)
            {
                if (info.Type == Burntime.Framework.PackageType.Game &&
                    !info.IsHidden && str == info.Package)
                {
                    PakName = str;
                    break;
                }
            }

            // if package is either not available or not specified, take the first available
            if (PakName == null)
            {
                foreach (PackageInfo info in paketManager.PackageInfos)
                {
                    if (info.Type == Burntime.Framework.PackageType.Game &&
                        !info.IsHidden)
                    {
                        PakName = info.Package;
                        break;
                    }
                }

                if (PakName == null)
                {
                    throw new Exception("Could not find any game packages!");
                }
            }

            // TODO: redo this stuff

            bool safeMode = engineSettings["engine"].GetBool("safemode");

            /*if (safeMode)
            {
                try
                {
                    runInternal(paketManager, assemblyControl, PakName);
                }
                catch (Exception e)
                {
                    System.Windows.Forms.MessageBox.Show("Error: " + e.Message, "Error", 
                        System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Stop);

                    File file = FileSystem.GetFile("system:errorlog.txt", FileOpenMode.NoPackage | FileOpenMode.Write);
                    file.Seek(0, SeekPosition.End);

                    System.IO.StreamWriter writer = new System.IO.StreamWriter(file);
                    writer.WriteLine();
                    writer.WriteLine("----------------------------------------------------------------------");
                    writer.WriteLine(System.DateTime.Now.ToLocalTime().ToString());
                    writer.WriteLine("exception: " + e.Message);
                    writer.WriteLine("trace:");
                    writer.Write(e.StackTrace);
                    writer.WriteLine();
                    writer.Close();

                    file.Close();

                    Environment.Exit(1);
                }
            }
            else*/
                runInternal(paketManager, assemblyControl, PakName);
        }

        void runInternal(PackageManager paketManager, AssemblyControl assembly, string pakName)
        {
            Engine engine = new();

            paketManager.LoadPackages(pakName, FileSystem.VFS, assembly);
            PackageInfo info = paketManager.GetInfo(pakName);

            Module module = assembly.GetModule(info.MainModule, pakName);

            engine.SetGameResolution(module.VerticalRatio, module.Resolutions);

            module.Engine = engine;
            module.SceneManager = new SceneManager(module);
            module.ResourceManager = new ResourceManager(engine);
            module.DeviceManager = new DeviceManager(engine.Resolution, engine.GameResolution);
            module.Engine.DeviceManager = module.DeviceManager;

            assembly.InitAllModules(module.ResourceManager);

            Log.Info("Run main module...");
            module.Run();

            Log.Info("Start engine...");
            engine.Start(new ApplicationInternal(module));
        }
    }
}
