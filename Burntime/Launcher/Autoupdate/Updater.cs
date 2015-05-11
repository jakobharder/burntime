using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.ComponentModel;
using System.Net;
using System.Windows.Forms;
using System.Diagnostics;

namespace Burntime.Autoupdate
{
    // settings integrity
    // backup & rollback

    class Updater
    {
        Burntime.Platform.IO.ConfigFile config;
        Burntime.Platform.IO.ConfigFile settings;
        string basePath;
        string systemPath;
        string gamePath;
        UpdateProgress progress;
        bool finished;

        // version info
        int version;
        string curVersion;
        string newVersion;
        bool savegameCompatible;

        // settings
        bool alwaysCheck;
        bool alwaysAsk;
        string downloadLocation;

        public bool AlwaysCheck
        {
            get { return alwaysCheck; }
        }

        public bool AlwaysAsk
        {
            get { return alwaysAsk; }
        }

        public string LocalVersion
        {
            get { return curVersion; }
        }

        public string VersionAvailable
        {
            get { return newVersion; }
        }

        public bool IsUpdateMode
        {
            get
            {
                // if binary name is update then we are in update mode
                return Path.GetFileNameWithoutExtension(Application.ExecutablePath).Equals("update", StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public Updater(string basePath)
        {
            this.basePath = basePath;
            systemPath = basePath + "system/";
            gamePath = basePath + "game/";
            if (IsUpdateMode)
            {
                systemPath = basePath = "../system/";
                gamePath = basePath = "../game/";
            }

            // open settings
            FileStream settingsStream = new FileStream(systemPath + "settings.txt", FileMode.Open);
            settings = new Burntime.Platform.IO.ConfigFile();
            settings.Open(settingsStream);
            settingsStream.Close();

            // read settings
            alwaysCheck = settings["autoupdate"].GetBool("always_check");
            alwaysAsk = settings["autoupdate"].GetBool("always_ask");
            downloadLocation = settings["autoupdate"].GetString("location");

            // read local version
            curVersion = settings["autoupdate"].GetString("version");
            version = ParseVersionNumber(curVersion);

            // delete previous temporary copy if necessary
            DeleteTemporaryCopy();
        }

        public bool CheckForUpdates()
        {
            // try to download version control
            try
            {
                using (System.Net.WebClient client = new System.Net.WebClient())
                {
                    client.DownloadFile(downloadLocation + "versioncontrol.txt", basePath + "vc.txt");
                }
            }
            catch
            {
                // if failed then just ignore auto-update
                return false;
            }

            Stream stream = new FileStream(basePath + "vc.txt", FileMode.Open);

            config = new Burntime.Platform.IO.ConfigFile();
            config.Open(stream);
            stream.Close();

            newVersion = config[""].GetString("version");
            int current = ParseVersionNumber(newVersion);

            savegameCompatible = config[curVersion].GetBool("savegame_compatible");

            File.Delete(basePath + "vc.txt");

            return current > version;
        }

        public bool ShowDownloadQuestion()
        {
            string message = "There is a new version available. \nDownload v" + VersionAvailable + "?";
            if (!savegameCompatible)
                message += "\n\nWarning: Old savegames cannot be used with this version.";
            
            DialogResult result = MessageBox.Show(message, "Auto-Update", MessageBoxButtons.YesNo);
            return (result == DialogResult.Yes);
        }

        public void InitiateUpdate()
        {
            // create temporary copy
            CreateTemporaryCopy();
            // run from temporary copy so we can also update common dlls
            RunFromTemporaryCopy();
        }

        public void Update()
        {
            if (!CheckForUpdates())
                return;

            Burntime.Platform.IO.ConfigSection section = config.GetSection(curVersion);
            if (section == null)
            {
                // no update for this available, download full version
                curVersion = "full";
            }

            BackgroundWorker worker = new BackgroundWorker();
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
            worker.ProgressChanged += new ProgressChangedEventHandler(worker_ProgressChanged);
            worker.DoWork += new DoWorkEventHandler(worker_DoWork);
            worker.WorkerSupportsCancellation = true;
            worker.WorkerReportsProgress = true;

            progress = new Burntime.Autoupdate.UpdateProgress();

            worker.RunWorkerAsync();

            if (progress.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                worker.CancelAsync();
            }
        }

        void CreateTemporaryCopy()
        {
            if (!Directory.Exists(basePath + "tmp"))
            {
                // copy launcher and related dlls
                Directory.CreateDirectory(basePath + "tmp");
                File.Copy(basePath + "ClassicLauncher.exe", basePath + "tmp/update.exe");
                File.Copy(systemPath + "Platform.dll", basePath + "tmp/Platform.dll");
                File.Copy(systemPath + "Framework.dll", basePath + "tmp/Framework.dll");
            }
        }

        void RunFromTemporaryCopy()
        {
            //ProcessStartInfo startInfo = new ProcessStartInfo("update.exe");
            //startInfo.WorkingDirectory = Path.GetFullPath(basePath + "tmp");
            //Process.Start(startInfo); 

            AppDomainSetup domaininfo = new AppDomainSetup();
            domaininfo.ApplicationBase = Path.GetFullPath(basePath + "tmp");
            AppDomain domain = AppDomain.CreateDomain("launcher", null, domaininfo);

            Directory.SetCurrentDirectory(domaininfo.ApplicationBase);
            domain.ExecuteAssembly("update.exe");
        }

        void DeleteTemporaryCopy()
        {
            if (Directory.Exists(basePath + "tmp"))
            {
                // delete temporary copy
                Directory.Delete(basePath + "tmp", true);
            }
        }

        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;

            bool harddiskMode = System.IO.File.Exists(downloadLocation + "versioncontrol.txt");

            string path = downloadLocation;
            string[] download = config[curVersion].GetStrings("game_add");
            string[] remove = config[curVersion].GetStrings("game_remove");

            if (download.Length == 0)
                return;

            using (System.Net.WebClient client = new System.Net.WebClient())
            {
                // make backup
                worker.ReportProgress(0, "create backup...");

                // remove old files
                foreach (string str in remove)
                {
                    string[] filenames = Directory.GetFiles(gamePath, str);
                    foreach (string filename in filenames)
                        File.Delete(filename);
                }

                // retrieve file size
                int totalsize = 0;
                worker.ReportProgress(0, "retrieve file information...");

                int index = 0;
                // get info
                do
                {
                    string filename = path + newVersion + "/" + download[index];

                    if (harddiskMode)
                    {
                        FileInfo info = new FileInfo(filename);
                        totalsize += (int)info.Length;
                    }
                    else
                        totalsize += GetHttpFileSize(filename);

                    index++;
                } while (!worker.CancellationPending && index < download.Length);

                if (worker.CancellationPending)
                    return;

                int downloaded = 0;

                bool open = false;

                // download
                worker.ReportProgress(0, "download files...");
                index = 0;

                Stream strResponse = null;
                Stream strLocal = null;
                byte[] downBuffer = new byte[2048];

                do
                {
                    if (!open)
                    {
                        string filename = path + newVersion + "/" + download[index];

                        // get download stream
                        if (harddiskMode)
                        {
                            strResponse = new FileStream(filename, FileMode.Open);
                        }
                        else
                            strResponse = client.OpenRead(filename);

                        // create local file
                        strLocal = new FileStream(gamePath + download[index], FileMode.Create, FileAccess.Write, FileShare.None);

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
                        worker.ReportProgress(downloaded * 100 / totalsize, "download " + download[index] + " ...\n" + (downloaded / 1024.0f).ToString("0.00") + " KB of " + (totalsize / 1024.0f).ToString("0.00") + " KB");
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

                finished = true;
                worker.ReportProgress(100, "update finished.\n" + (totalsize / 1024.0f).ToString("0.00") + " KB");
            }
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
            if (progress != null)
            {
                if (!e.Cancelled && finished)
                {
                    settings["autoupdate"].Set("version", newVersion);

                    FileStream settingsStream = new FileStream(systemPath + "settings.txt", FileMode.OpenOrCreate);
                    settings.Save(settingsStream);
                    settingsStream.Close();
                }
            }
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
            return (int)webResponse.ContentLength;
        }
    }
}
