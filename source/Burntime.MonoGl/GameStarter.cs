using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;
using Burntime.Framework;
using Burntime.Data.BurnGfx;
using Burntime.Classic;
//using System.IO;

namespace Burntime.MonoGl
{
    public class GameStarter
    {
        public void Run()
        {
            Log.Initialize("log.txt");
            string PakName = "classic";

            FileSystem.BasePath = "";
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
            
            runInternal(paketManager, assemblyControl, PakName);
        }

        void runInternal(PackageManager paketManager, AssemblyControl assembly, string pakName)
        {
            Engine engine = new();

            paketManager.LoadPackages(pakName, FileSystem.VFS, null);

            BurntimeClassic game = new();

            engine.SetGameResolution(game.VerticalRatio, game.Resolutions);

            game.Engine = engine;
            game.SceneManager = new SceneManager(game);
            game.DeviceManager = new DeviceManager(engine.Resolution, engine.GameResolution);
            game.Engine.DeviceManager = game.DeviceManager;

            game.Initialize(new ResourceManager(engine));

            BurnGfxModule burnGfx = new();
            burnGfx.Initialize(game.ResourceManager);

            //Log.Info("Run main module...");
            game.Run();

            //Log.Info("Start engine...");
            //engine.Start(new ApplicationInternal(module));
        }
    }
}
