
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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using Burntime.Framework;
using Burntime.Platform.IO;

namespace Burntime.Launcher.Update
{
    // settings integrity
    // backup & rollback
    // timeout

    /// <summary>
    /// Holds download instructions.
    /// </summary>
    struct DownloadInstructions
    {
        /// <summary>
        /// File list.
        /// </summary>
        public ModuleUpdateFilePath[] Files;

        /// <summary>
        /// Remote location.
        /// </summary>
        public string From;

        /// <summary>
        /// Local location.
        /// </summary>
        public string To;

        /// <summary>
        /// Action description text.
        /// </summary>
        public string Text;
    }

    class Updater
    {
        #region private attributes
        string basePath;
        string systemPath;
        string gamePath;
        UpdateProgress progress;

        PackageSystem vfs;

        // settings
        bool disableOnline;
        bool autoUpdate;
        string downloadLocation;
        bool useUnstableVersion;

        EventWaitHandle finished = new EventWaitHandle(false, EventResetMode.AutoReset);

        bool closeDialog = false;
        bool check = false;

        bool noConnection = false;
        BurntimeUpdateInfo updates;
        Version engineVersion;
        #endregion

        #region public attributes
        /// <summary>
        /// Engine version.
        /// </summary>
        public Version EngineVersion
        {
            get { return engineVersion; }
        }

        /// <summary>
        /// Disable online features flag.
        /// </summary>
        public bool DisableOnline
        {
            get { return disableOnline; }
        }

        /// <summary>
        /// Automatically start update flag.
        /// </summary>
        public bool AutoUpdate
        {
            get { return autoUpdate; }
        }

        /// <summary>
        /// Use unstable version flag.
        /// </summary>
        public bool UseUnstableVersion
        {
            get { return useUnstableVersion; }
        }

        /// <summary>
        /// Is in update mode.
        /// </summary>
        public bool IsUpdateMode
        {
            get
            {
                // if binary name is update then we are in update mode
                return Path.GetFileNameWithoutExtension(Application.ExecutablePath).Equals("update", StringComparison.InvariantCultureIgnoreCase);
            }
        }

        /// <summary>
        /// No connection or connection lost.
        /// </summary>
        public bool NoConnection
        {
            get { return noConnection; }
        }
        #endregion

        public Updater()
        {
        }

        public Updater(PackageSystem vfs, string basePath)
        {
            this.vfs = vfs;
            this.basePath = basePath;
        }

        public void ReadSettings()
        {
            systemPath = basePath + "system/";
            gamePath = basePath + "game/";
            //if (IsUpdateMode)
            //{
            //    systemPath = basePath = "../system/";
            //    gamePath = basePath = "../game/";
            //}

            // open settings
            //Burntime.Platform.IO.File settingsStream = vfs.GetFile("settings.txt", FileOpenMode.Read);
            //settings = new Burntime.Platform.IO.ConfigFile();
            //settings.Open(settingsStream);
            //settingsStream.Close();

            // read settings
            disableOnline = Program.Settings["autoupdate"].GetBool("disable_online");
            autoUpdate = Program.Settings["autoupdate"].GetBool("autoupdate");
            downloadLocation = Program.Settings["autoupdate"].GetString("location");
            useUnstableVersion = Program.Settings["autoupdate"].GetBool("unstable");

            // delete previous temporary copy if necessary
            DeleteTemporaryCopy();
        }

        public VersionControl CheckForUpdates(PackageInfo info)
        {
            if (info.GameName == "launcher")
            {
                // set current engine version number
                engineVersion = info.Version;
            }

            // download update info if not already downloaded
            if (!GetUpdateInfo())
                return null;

            // cut .pak from package
            string package = Path.GetFileNameWithoutExtension(info.Package);

            // check only supported files
            if (!updates.Modules.ContainsKey(package))
                return null;

            ModuleUpdateInfo moduleUpdateInfo = updates.Modules[package];

            // get versions
            Version localVersion = info.Version;
            Version serverVersion = useUnstableVersion ? moduleUpdateInfo.Unstable : moduleUpdateInfo.Stable;

            // return null if not updateable
            if (serverVersion <= localVersion)
                return null;

            return new VersionControl(info.Package, serverVersion, localVersion, moduleUpdateInfo);
        }

        /// <summary>
        /// Get latest version of one file.
        /// </summary>
        /// <param name="fileName">file name</param>
        /// <returns>file path with version directory</returns>
        public ModuleUpdateFilePath GetLatestVersion(string fileName)
        {
            // download update info if not already downloaded
            if (!GetUpdateInfo())
                return null;

            string moduleName = Path.GetFileNameWithoutExtension(fileName);

            if (updates.Modules.ContainsKey(moduleName))
            {
                ModuleUpdateInfo info = updates.Modules[moduleName];
                return new ModuleUpdateFilePath(info.Stable, info.Base == "" ? info.Name : info.Base, fileName);
            }

            return null;
        }

        public bool ShowDownloadQuestion(VersionControl[] vcs)
        {
            bool savegameCompatible = true;
            bool full = false;

            foreach (VersionControl vc in vcs)
            {
                if (!vc.SavegameCompatible)
                {
                    // if only one update is not savegame compatible show warning
                    savegameCompatible = false;
                }

                if (vc.FullUpdate)
                {
                    // if only one update needs a full update, then display full update warning
                    full = true;
                }
            }

            string message = Program.Text[""].GetString("autoupdate_question") + "\n";

            foreach (VersionControl vc in vcs)
            {
                string name = vc.Name;
                if (name == "launcher")
                    name = "engine";
                message += string.Format(Program.Text[""].GetString("autoupdate_list"), name, vc.ServerVersion.ToString());
                if (!vc.Stable)
                    message += " " + Program.Text[""].GetString("unstable");
                message += "\n";
            }

            message += Program.Text[""].GetString("autoupdate_question1") + "\n";
            if (!savegameCompatible || full)
            {
                message += Program.Text[""].GetString("autoupdate_warning") + "\n";

                if (!savegameCompatible)
                    message += Program.Text[""].GetString("savegame_warning") + "\n";
                if (full)
                    message += Program.Text[""].GetString("settings_warning") + "\n";
            }
            
            DialogResult result = MessageBox.Show(message, Program.Text[""].GetString("autoupdate"), MessageBoxButtons.YesNo);
            return (result == DialogResult.Yes);
        }

        public void InitiateUpdate()
        {
            // create temporary copy
            CreateTemporaryCopy();
            // run from temporary copy so we can also update common dlls
            RunFromTemporaryCopy();
        }

        public void Update(VersionControl[] vcs)
        {
            //if (!CheckForUpdates())
            //    return;

            //Burntime.Platform.IO.ConfigSection section = config.GetSection(localVersion.ToString());
            //if (section == null)
            //{
            //    // no update for this available, download full version
            //    localVersion = new Version("0.0.0");
            //}

            BackgroundWorker worker = new BackgroundWorker();
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
            worker.ProgressChanged += new ProgressChangedEventHandler(worker_ProgressChanged);
            worker.DoWork += new DoWorkEventHandler(worker_DoWork);
            worker.WorkerSupportsCancellation = true;
            worker.WorkerReportsProgress = true;

            closeDialog = false;
            progress = new UpdateProgress("autoupdate");

            // construct download instructions
            List<ModuleUpdateFilePath> fileList = new List<ModuleUpdateFilePath>();

            foreach (VersionControl vc in vcs)
            {
                // add game packages
                foreach (ModuleUpdateFilePath path in vc.Info.Add)
                {
                    fileList.Add(path);
                }
            }

            // start download
            DownloadInstructions instr;
            instr.Files = fileList.ToArray();
            instr.From = downloadLocation + "files/";
            instr.To = "";
            instr.Text = "autoupdate";

            finished.Reset();
            progress.Run(worker, instr);
            finished.WaitOne();
        }

        public void DownloadFiles(ModuleUpdateFilePath[] files, string locationFrom, string locationTo)
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
            worker.ProgressChanged += new ProgressChangedEventHandler(worker_ProgressChanged);
            worker.DoWork += new DoWorkEventHandler(worker_DoWork);
            worker.WorkerSupportsCancellation = true;
            worker.WorkerReportsProgress = true;

            closeDialog = true;
            progress = new UpdateProgress("download");

            DownloadInstructions instr;
            instr.Files = files;
            instr.From = locationFrom;
            instr.To = locationTo;
            instr.Text = "download";

            finished.Reset();
            progress.Run(worker, instr);
            finished.WaitOne();
        }

        void CreateTemporaryCopy()
        {
            if (!Directory.Exists(basePath + "tmp"))
                Directory.CreateDirectory(basePath + "tmp");

            try
            {
                // copy launcher and related dlls
                System.IO.File.Copy(basePath + "Launcher.exe", basePath + "tmp/update.exe", true);
                System.IO.File.Copy(systemPath + "Platform.dll", basePath + "tmp/Platform.dll", true);
                System.IO.File.Copy(systemPath + "Framework.dll", basePath + "tmp/Framework.dll", true);
            }
            catch
            {
                List<Process> toKill = new List<Process>();

                // the last updater.exe is propably still runnnig so kill it
                foreach (Process p in Process.GetProcesses())
                {
                    if (p.ProcessName.Equals("update", StringComparison.InvariantCultureIgnoreCase))
                    {
                        toKill.Add(p);
                    }
                }

                foreach (Process p in toKill)
                {
                    p.Kill();
                }

                System.Threading.Thread.Sleep(500);

                // copy launcher and related dlls
                System.IO.File.Copy(basePath + "Launcher.exe", basePath + "tmp/update.exe", true);
                System.IO.File.Copy(systemPath + "Platform.dll", basePath + "tmp/Platform.dll", true);
                System.IO.File.Copy(systemPath + "Framework.dll", basePath + "tmp/Framework.dll", true);
            }
        }

        void RunFromTemporaryCopy()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("update.exe");
            startInfo.WorkingDirectory = Path.GetFullPath(basePath + "tmp");
            Process.Start(startInfo);

            // prevent deletion of temporary
            Program.TemporaryPath = "";

            // close application
            Application.Exit();

            //AppDomainSetup domaininfo = new AppDomainSetup();
            //domaininfo.ApplicationBase = Path.GetFullPath(basePath + "tmp");
            //AppDomain domain = AppDomain.CreateDomain("launcher", null, domaininfo);

            //Directory.SetCurrentDirectory(domaininfo.ApplicationBase);
            //domain.ExecuteAssembly("update.exe");
        }

        void DeleteTemporaryCopy()
        {
            //if (!IsUpdateMode && Directory.Exists(basePath + "tmp"))
            //{
            //    // delete temporary copy
            //    Directory.Delete(basePath + "tmp", true);
            //}
        }

        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            check = false;

            BackgroundWorker worker = sender as BackgroundWorker;
            DownloadInstructions instr = (DownloadInstructions)e.Argument;

            bool harddiskMode = Directory.Exists(instr.From);

            string to = instr.To;
            string from = instr.From;//downloadLocation;

            ModuleUpdateFilePath[] download = instr.Files;//config[localVersion.ToString()].GetStrings("game_add");
            string[] remove = new string[0];//config[localVersion.ToString()].GetStrings("game_remove");

            if (download.Length == 0)
            {
                finished.Set();
                return;
            }

            int totalsize = 0;

            using (WebClientEx client = new WebClientEx())
            {
                // make backup
                worker.ReportProgress(0, Program.Text[instr.Text].Get("backup"));

                string backupPath = "tmp/backup/";
                Directory.CreateDirectory(backupPath);

                foreach (ModuleUpdateFilePath str in download)
                {
                    string dir = Path.GetDirectoryName(str.File);
                    if (!Directory.Exists(backupPath + dir))
                        Directory.CreateDirectory(backupPath + dir);

                    if (System.IO.File.Exists(str.File))
                        System.IO.File.Copy(str.File, backupPath + str.File, true);
                }

                // remove old files
                foreach (string str in remove)
                {
                    string[] filenames = Directory.GetFiles(gamePath, str);
                    foreach (string filename in filenames)
                        System.IO.File.Delete(filename);
                }

                // retrieve file size
                worker.ReportProgress(0, Program.Text[instr.Text].Get("retrieve"));

                int index = 0;
                // get info
                do
                {
                    string filename = from + download[index].FullPath;

                    try
                    {
                        if (harddiskMode)
                        {
                            FileInfo info = new FileInfo(filename);
                            totalsize += (int)info.Length;
                        }
                        else
                            totalsize += GetHttpFileSize(filename);
                    }
                    catch
                    {
                        MessageBox.Show(string.Format(Program.Text[instr.Text].Get("error1"), filename),
                            Program.Text[instr.Text].Get("error"));

                        worker.ReportProgress(-1);

                        finished.Set();
                        return;
                    }

                    index++;
                } while (!worker.CancellationPending && index < download.Length);

                if (worker.CancellationPending)
                {
                    finished.Set();
                    return;
                }

                int downloaded = 0;

                bool open = false;

                // download
                string msg = Program.Text[instr.Text].Get("download");
                worker.ReportProgress(0, string.Format(msg, GetSizeString(instr.Text, downloaded), GetSizeString(instr.Text, totalsize))); index = 0;

                Stream strResponse = null;
                Stream strLocal = null;
                byte[] downBuffer = new byte[2048];

                int progress = 0;

                do
                {
                    if (!open)
                    {
                        string filename = from + download[index].FullPath;
                        msg = Program.Text[instr.Text].Get("download_file");
                        worker.ReportProgress(progress, string.Format(msg, download[index], GetSizeString(instr.Text, downloaded), GetSizeString(instr.Text, totalsize)));

                        try
                        {
                            // get download stream
                            if (harddiskMode)
                            {
                                strResponse = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                            }
                            else
                                strResponse = client.OpenRead(filename);
                        }
                        catch
                        {
                            MessageBox.Show(string.Format(Program.Text[instr.Text].Get("error2"), filename),
                                Program.Text[instr.Text].Get("error"));

                            worker.ReportProgress(-1);
                            
                            break;
                        }

                        // create local file
                        strLocal = new FileStream(to + download[index].File, FileMode.Create, FileAccess.Write, FileShare.None);

                        open = true;
                    }

                    int bytesSize = 0;

                    // download 2kb
                    bytesSize = strResponse.Read(downBuffer, 0, downBuffer.Length);

                    if (bytesSize > 0)
                    {
                        // save to harddrive
                        strLocal.Write(downBuffer, 0, bytesSize);

                        // report progress
                        downloaded += bytesSize;

                        // update progress if changed
                        float fprogress = downloaded * 100.0f / totalsize;
                        //if (progress != (int)fprogress)
                        {
                            progress = (int)fprogress;
                            msg = Program.Text[instr.Text].Get("download_file");
                            worker.ReportProgress(progress, string.Format(msg, download[index].File, GetSizeString(instr.Text, downloaded), GetSizeString(instr.Text, totalsize)));
                        }
                    }
                    else
                    {
                        strLocal.Close();
                        strResponse.Close();
                        open = false;
                    }

                    if (!open)
                    {
                        index++;
                    }
                } while (!worker.CancellationPending && index < download.Length);

                strLocal.Close();
                strResponse.Close();

                // download was cancelled
                if (worker.CancellationPending)
                {
                    worker.ReportProgress(progress, Program.Text[instr.Text].Get("rollback"));

                    // delete downloaded files
                    foreach (ModuleUpdateFilePath file in download)
                    {
                        if (System.IO.File.Exists(to + file.File))
                        {
                            System.IO.File.Delete(to + file.File);
                        }
                    }

                    // restore backup
                    foreach (ModuleUpdateFilePath file in download)
                    {
                        if (System.IO.File.Exists(backupPath + file.File))
                        {
                            System.IO.File.Copy(backupPath + file.File, to + file.File, true);
                        }
                    }
                }

                // remove backup
                Directory.Delete(backupPath, true);
            }

            if (worker.CancellationPending)
                worker.ReportProgress(100, Program.Text[instr.Text].Get("cancel"));
            else
                worker.ReportProgress(100, string.Format(Program.Text[instr.Text].Get("finish"), GetSizeString(instr.Text, totalsize)));

            finished.Set();
            check = true;
        }

        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

            if (progress != null)
            {
                progress.Progress = System.Math.Min(100, e.ProgressPercentage) / 100.0f;
                progress.Message = (string)e.UserState;

                // signal progress dialog to change to finished mode
                if (e.ProgressPercentage == 100)
                {
                    progress.Finish();
                }
            }
        }

        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // something went wrong
            if (!check)
            {
                if (e.Error != null)
                {
                    MessageBox.Show(e.Error.Message,
                        Program.Text["download"].Get("error"));
                }
                else
                {
                    MessageBox.Show(Program.Text["download"].Get("error"),
                        Program.Text["download"].Get("error"));
                }
            }

            // in case of cancel we need to finish and close progress dialog
            progress.Finish();
            if (closeDialog || !check)
                progress.Close();
        }

        int ParseVersionNumber(string number)
        {
            string[] token = number.Split('.');
            if (token.Length != 3)
                return 0;

            int main = 0;
            int sub = 0;
            int rev = 0;

            int.TryParse(token[0], out main);
            int.TryParse(token[1], out sub);
            int.TryParse(token[2], out rev);

            return main * 10000 + sub * 100 + rev;
        }

        int GetHttpFileSize(string url)
        {
            // Create a request to the file we are downloading
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
            // Set default authentication for retrieving the file
            webRequest.Credentials = CredentialCache.DefaultCredentials;
            // Retrieve the response from the server
            HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse();
            // Ask the server for the file size and store it
            int length = (int)webResponse.ContentLength;
            webResponse.Close();
            return length;
        }

        string GetSizeString(string text, int bytes)
        {
            if (bytes < 1024 * 1000)
                return (bytes / 1024.0f).ToString(Program.Text[text].Get("kb"));
            else
                return (bytes / 1024.0f / 1024.0f).ToString(Program.Text[text].Get("mb"));
        }

        /// <summary>
        /// Get module download/update info from server.
        /// </summary>
        private bool GetUpdateInfo()
        {
            if (updates == null)
            {
                HttpHelper http = new HttpHelper();

                Stream stream;

                // but first check if updates.xml is available in system directory for debug purposes
                if (vfs.ExistsFile("updates.xml"))
                {
                    stream = vfs.GetFile("updates.xml", FileOpenMode.Read).Stream;
                }
                // otherwise download it
                else
                {
                    stream = http.DownloadFileToMemory(downloadLocation + "updates.xml");
                    if (stream == null)
                    {
                        // set to no connection
                        noConnection = true;
                        return false;
                    }
                }

                updates = new BurntimeUpdateInfo(stream);
                stream.Close();
            }

            return true;
        }
    }
}
