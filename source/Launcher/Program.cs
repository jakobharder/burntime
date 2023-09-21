
#region GNU General Public License - Burntime
/*
 *  Burntime
 *  Copyright (C) 2008-2011 Jakob Harder
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
*/
#endregion

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using Burntime.Framework;
using Burntime.Launcher.Update;
using Burntime.Platform.IO;

namespace Burntime.Launcher
{
    static class Program
    {
        public static readonly string EnginePackage = "launcher";

        static List<GamePackage> games;
        static Updater updater;
        static List<VersionControl> versionControls;
        static string[] languages;
        static PackageSystem vfs;
        static ConfigFile settings;
        static ConfigFile text;
        static PackageManager packageManager;
        static LauncherForm form;
        static string temporaryPath;
        static Version engineVersion;

        static bool noConnection = false;

        static public List<GamePackage> Games
        {
            get { return games; }
        }

        static public bool IsUpdateAvailable
        {
            get { return versionControls.Count != 0; }
        }

        static public Updater Updater
        {
            get { return updater; }
        }

        static public List<VersionControl> VersionControls
        {
            get { return versionControls; }
        }

        static public string[] Languages
        {
            get { return languages; }
        }

        static public PackageSystem VFS
        {
            get { return vfs; }
        }

        static public ConfigFile Settings
        {
            get { return settings; }
        }

        static public ConfigFile Text
        {
            get { return text; }
        }

        static public PackageManager PackageManager
        {
            get { return packageManager; }
        }

        static public string TemporaryPath
        {
            get { return temporaryPath; }
            set { temporaryPath = value; }
        }

        static public bool NoConnection
        {
            get { return noConnection; }
        }

        static public Version EngineVersion
        {
            get { return engineVersion; }
        }

        static internal void Run()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // check dlls
            if (!CheckDlls())
                return;

            if (System.IO.Path.GetFileNameWithoutExtension(Application.ExecutablePath).Equals("update", StringComparison.InvariantCultureIgnoreCase))
            {
                // updater mode
                Environment.CurrentDirectory = "../";
             
                // initialize vfs
                vfs = new PackageSystem();
                InitializeVFS("");

                // find all packages
                CheckPackages("");

                // get update info
                CheckUpdates("");

                // release all packages
                vfs.UnmountAll();

                // update
                updater.Update(versionControls.ToArray());

                // start launcher
                ProcessStartInfo startInfo = new ProcessStartInfo("launcher.exe");
                Process.Start(startInfo);
            }
            else
            {
                CreateTemporary();

                Application.UseWaitCursor = true;

                // load vfs
                vfs = new PackageSystem();
                if (!RefreshVFS(false))
                {
                    RemoveTemporary(); // code structure in this function is not nice
                    return;
                }

                // find all packages
                CheckPackages("");
                RefreshGamePackages();

                // check for updates
                CheckUpdates("");

                bool update = false;
                if (!noConnection && versionControls.Count > 0 && updater.AutoUpdate)
                {
                    // show question before auto-update
                    update = updater.ShowDownloadQuestion(versionControls.ToArray());
                }

                if (update)
                {
                    // enter update
                    updater.InitiateUpdate();
                }
                else
                {
                    // fully updated, run after update if necessary
                    Update.AfterUpdate.Check();
                    if (Update.AfterUpdate.IsAfterUpdate)
                    {
                        Update.AfterUpdate.RunAfterUpdate();
                    }

                    // show dialog
                    Application.Run(form = new LauncherForm());
                }
            }

            RemoveTemporary();
        }

        static bool CheckDlls()
        {
            // try to load dlls

            //// Platform.dll
            //try
            //{
            //    Burntime.Platform.PlatformDllAccess access = new Burntime.Platform.PlatformDllAccess();
            //    if (!access.Initialize())
            //        throw new Exception();
            //}
            //catch
            //{
            //    MessageBox.Show("Error: Could not load Platform.dll!", "Error");
            //    Environment.Exit(1);
            //    return false;
            //}

            //// Framework.dll
            //try
            //{
            //    Burntime.Framework.FrameworkDllAccess access = new Burntime.Framework.FrameworkDllAccess();
            //    if (!access.Initialize())
            //        throw new Exception();
            //}
            //catch
            //{
            //    MessageBox.Show("Error: Could not load Framework.dll!", "Error");
            //    Environment.Exit(1);
            //    return false;
            //}

            return true;
        }

        static void CreateTemporary()
        {
            // create temporary directory
            do
            {
                temporaryPath = System.IO.Path.Combine("tmp", System.IO.Path.GetRandomFileName());

            } while (System.IO.Directory.Exists(temporaryPath) || System.IO.File.Exists(temporaryPath));

            System.IO.Directory.CreateDirectory(temporaryPath);
        }

        static void RemoveTemporary()
        {
            try
            {
                if (System.IO.Directory.Exists(temporaryPath))
                {
                    System.IO.Directory.Delete(System.IO.Path.GetDirectoryName(temporaryPath), true);
                }
            }
            catch
            {
                // just ignore errors
            }
        }

        static void CheckPackages(string basePath)
        {
            packageManager = new PackageManager(basePath + "game/");

            // disable auto-update and download functions if folder packages are available
            noConnection = packageManager.HasFolderPackages;

            // get engine version
            PackageInfo info = packageManager.GetInfo("launcher");
            if (info == null)
            {
                // if launcher.pak is not available then something went wrong
                throw new Exception(ErrorMsg.MsgCorruptedFiles);
            }

            engineVersion = info.Version;
        }

        static void RefreshGamePackages()
        {
            games = new List<GamePackage>();

            // find all game packages
            foreach (PackageInfo info in packageManager.PackageInfos)
            {
                if (info.Type == Burntime.Framework.PackageType.Game && !info.IsHidden)
                {
                    GamePackage game = new GamePackage(info);
                    games.Add(game);
                }
            }

            // last, add engine page
            games.Add(new GamePackage(packageManager.GetInfo("launcher")));
        }

        static void CheckUpdates(string basePath)
        {
            versionControls = new List<VersionControl>();

            if (noConnection)
                return;

            // check for updates
            updater = new Updater(vfs, basePath);

            updater.ReadSettings();

            if (!updater.DisableOnline)
            {
                foreach (PackageInfo info in packageManager.PackageInfos)
                {
                    VersionControl vc = updater.CheckForUpdates(info);
                    if (vc != null)
                        versionControls.Add(vc);
                    else if (updater.NoConnection)
                    {
                        // check if no connection is determined
                        noConnection = true;
                        break;
                    }
                }
            }
            else
                noConnection = true;
        }

        static bool InitializeVFS(string basePath)
        {
            // mount system folder
            if (!vfs.Mount("system", FileSystem.OpenPackage(basePath + "system")))
            {
                MessageBox.Show("Could not find system folder in working directory!\nWorking directory: " + System.IO.Path.GetFullPath(basePath + "/"), "File not found",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // load launcher package (with additional languages)
            PackageManager pakman = new PackageManager(basePath + "game/");
            if (!pakman.LoadPackages("launcher", vfs, null))
            {
                MessageBox.Show("Could not find launcher packages in game folder!\nPlease check: " + System.IO.Path.GetFullPath(basePath + "game/"), "File not found", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // get available languages
            languages = pakman.Languages;

            // add user folder
            vfs.Mount("user", FileSystem.GetUserPackage("Burntime"));

            // open settings file
            settings = new ConfigFile();
            settings.Open(vfs.GetFile("settings.txt", FileOpenMode.Read));

            // set current language
            vfs.LocalizationCode = settings["game"].Get("language");

            text = new ConfigFile();
            text.Open(vfs.GetFile("lang.txt", FileOpenMode.Read));
            return true;
        }

        static public void RefreshLanguage()
        {
            // set current language
            vfs.LocalizationCode = settings["game"].Get("language");

            text = new ConfigFile();
            text.Open(vfs.GetFile("lang.txt", FileOpenMode.Read));
        }

        static public bool RefreshVFS(bool recheckPackages)
        {
            vfs.UnmountAll();
            if (!InitializeVFS(""))
                return false;

            if (form != null)
            {
                form.RefreshLanguage();
                form.InitializeLanguageButtons();
            }

            // reload packages
            if (recheckPackages)
            {
                CheckPackages("");
                RefreshGamePackages();
                CheckUpdates("");
            }

            return true;
        }

        static public void SaveSettings()
        {
            try
            {
                Program.Settings.Save(Program.VFS.GetFile("user:settings.txt", Burntime.Platform.IO.FileOpenMode.Write));
            }
            catch
            {
                ErrorMsg.ShowError("Failed to write settings!");
            }
        }
    }
}
