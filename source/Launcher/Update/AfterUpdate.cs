using System;
using System.Windows.Forms;
using Burntime.Platform.IO;

namespace Burntime.Launcher.Update
{
    static class AfterUpdate
    {
        static Version lastVersion;

        static public Version LastVersion
        {
            get { return lastVersion; }
        }

        static public bool IsAfterUpdate
        {
            get { return lastVersion < Program.EngineVersion; }
        }

        static public void Check()
        {
            // check engine version
            if (Program.EngineVersion < new Version("0.0.15"))
            {
                // if engine is older then 0.0.15, then something went wrong
                throw new Exception(ErrorMsg.MsgCorruptedFiles);
            }

            File file = FileSystem.GetFile("system/version.txt", FileOpenMode.NoPackage);
            if (file != null)
            {
                ConfigFile version = new ConfigFile();
                version.Open(file);

                // get last version
                lastVersion = version[""].GetVersion("version");

                // store new version
                version[""].Set("version", Program.EngineVersion.ToString());

                try
                {
                    version.Save(FileSystem.GetFile("system/version.txt", FileOpenMode.NoPackage | FileOpenMode.Write));
                }
                catch
                {
                    // this one is not critical, so show warning and don't quit
                    MessageBox.Show("Error: Unknown error occurred while finishing the update.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                // set to last version that didn't support after update handling
                lastVersion = new Version("0.0.14");

                try
                {
                    file = FileSystem.GetFile("system/version.txt", FileOpenMode.NoPackage | FileOpenMode.Write);
                    file.WriteLine("version=" + Program.EngineVersion.ToString());
                    file.Close();
                }
                catch (Exception e)
                {
                    // this one is not critical, so show warning and don't quit
                    ErrorMsg.ShowError("Unknown error occurred while finishing the update.", e);
                }
            }
        }

        static public void RunAfterUpdate()
        {
            if (lastVersion < new Version("0.0.15"))
            {
                // set safemode=on
                Program.Settings["engine"].Set("safemode", true);
                Program.SaveSettings();

                // vvv no need for that, update settings.txt just via auto-update vvv

                //// since settings are stored in user folder, change those in system/ too
                //ConfigFile systemSettings = new ConfigFile();

                //File file = FileSystem.GetFile("system/settings.txt", FileOpenMode.NoPackage);
                //if (file == null)
                //{
                //    // something went wrong
                //    Burntime.Launcher.ErrorMsg.ShowMsgCorruptFilesAndExit();
                //}

                //systemSettings.Open(file);
                //systemSettings["engine"].Set("safemode", true);

                //file = FileSystem.GetFile("system/settings.txt", FileOpenMode.NoPackage | FileOpenMode.Write);
                //if (file == null)
                //{
                //    // something went wrong
                //    Burntime.Launcher.ErrorMsg.ShowMsgCorruptFilesAndExit();
                //}

                //systemSettings.Save(file);
            }
        }
    }
}
