
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
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.ComponentModel;

using System.IO;
using Burntime.Platform.IO;
using Burntime.Framework;

namespace Burntime.Launcher
{
    class GamePackageTabPage : BrowserTabPage
    {
        GamePackage package;
        string downloadPath;
        string updatePath;

        string[] supportedLanguage;
        string defaultLanguage;
        string language;

        Burntime.Platform.IO.ConfigFile downloads;
        Burntime.Platform.IO.ConfigFile text;
        bool readyFlag;
        BackgroundWorker infoDownloader;

        HtmlSettings settings;
        HtmlVariables variables;

        public GamePackage Package
        {
            get { return package; }
        }
    
        public GamePackageTabPage(GamePackage package)
        {
            this.package = package;
            Text = package.Title;

            settings = new HtmlSettings(package.UserSettings);
            variables = new HtmlVariables();

            // set variables
            variables.SetText("version", package.Version.ToString());
            variables.SetFlag("is_update_available", Program.VersionControls.Count > 0 && 
                !Program.PackageManager.HasFolderPackages && !Program.NoConnection);
            variables.SetFlag("is_updated", Program.VersionControls.Count == 0 && 
                !Program.PackageManager.HasFolderPackages && !Program.NoConnection);

            variables.SetFlag("is_not_connected", !Program.PackageManager.HasFolderPackages &&
                Program.NoConnection && !Program.Settings["autoupdate"].GetBool("disable_online"));
            variables.SetFlag("is_online_disabled", Program.Settings["autoupdate"].GetBool("disable_online"));
            variables.SetFlag("is_has_folders", Program.PackageManager.HasFolderPackages &&
                !Program.Settings["autoupdate"].GetBool("disable_online"));

            // read change logs
            // string changeLogFile = ((package.Name != "launcher") ? package.Name : "Engine") + "-ChangeLog.txt";
            string changeLogFile = "ChangeLog.txt";
            if (System.IO.File.Exists(changeLogFile))
            {
                Stream changeLogStream = new FileStream(changeLogFile, FileMode.Open);
                variables.SetText("changelog", ChangelogToHtml.Convert(changeLogStream));
                changeLogStream.Close();
            }

            downloadPath = Program.Settings["autoupdate"].Get("downloads");
            updatePath = Program.Settings["autoupdate"].Get("location");

            if (!Program.NoConnection && package.Name == Program.EnginePackage)
            {
                infoDownloader = new BackgroundWorker();
                infoDownloader.DoWork += new DoWorkEventHandler(infoDownloader_DoWork);
                infoDownloader.RunWorkerCompleted += new RunWorkerCompletedEventHandler(infoDownloader_RunWorkerCompleted);
                infoDownloader.RunWorkerAsync();
            }
            else
                readyFlag = true;

            Uri url = new Uri(package.DataPath + "\\main.htm");
            browser.Url = url;
        }

        void infoDownloader_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            RefreshPage(false);
        }

        void infoDownloader_DoWork(object sender, DoWorkEventArgs e)
        {
            // download supported language info
            HttpHelper http = new HttpHelper();
            Stream stream = http.DownloadFileToMemory(downloadPath + "lang.txt");
            if (stream != null)
            {
                // read supoorted language info
                Burntime.Platform.IO.ConfigFile languageInfo = new Burntime.Platform.IO.ConfigFile();
                languageInfo.Open(stream);
                supportedLanguage = languageInfo[""].GetStrings("supported");
                defaultLanguage = languageInfo[""].GetString("default");

                // select default language
                language = defaultLanguage;

                // choose language
                foreach (string code in supportedLanguage)
                {
                    if (code.Equals(Program.VFS.LocalizationCode, StringComparison.InvariantCultureIgnoreCase))
                    {
                        language = Program.VFS.LocalizationCode;
                        break;
                    }
                }

                language = language.ToLower();

                // open config
                stream = http.DownloadFileToMemory(downloadPath + "downloads.txt");
                downloads = new Burntime.Platform.IO.ConfigFile();
                downloads.Open(stream);
                stream.Close();
                stream = http.DownloadFileToMemory(downloadPath + this.language + "/lang.txt");
                text = new Burntime.Platform.IO.ConfigFile();
                text.Open(stream);
                stream.Close();
            }

            readyFlag = true;
        }

        //public void RefreshPage()
        //{
        //    if (browser.Url == null || browser.Url == new Uri("about:blank"))
        //        browser.Navigate(package.DataPath + "\\main.htm");
        //    else
        //        browser.Navigate(browser.Url);
        //}

        public void RefreshPage(bool changedLanguage)
        {
            if (!readyFlag)
                return;

            lock (this)
            {
                if (changedLanguage && infoDownloader != null && !infoDownloader.IsBusy)
                    infoDownloader.RunWorkerAsync();
            }

            //// select default language
            //language = defaultLanguage;

            //// choose language
            //foreach (string code in supportedLanguage)
            //{
            //    if (code.Equals(Program.VFS.LocalizationCode, StringComparison.InvariantCultureIgnoreCase))
            //    {
            //        language = Program.VFS.LocalizationCode;
            //        break;
            //    }
            //}

            //language = language.ToLower();
            
            if (browser.Url == null || browser.Url == new Uri("about:blank"))
                browser.Navigate(package.DataPath + "\\main.htm");
            else
                browser.Navigate(browser.Url);

            //if (Program.VersionControls.Count > 0)
            //{
            //    // show update page
            //    if (!Uri.IsWellFormedUriString(updatePath + language + "/update.htm", UriKind.Absolute))
            //    {
            //        browser.Url = new Uri(Path.GetFullPath(updatePath + language + "/update.htm"));
            //    }
            //    else
            //        browser.Url = new Uri(updatePath + language + "/update.htm");
            //}
            //else
            //{
            //    // show download page
            //    if (!Uri.IsWellFormedUriString(downloadPath + language + "/downloads.htm", UriKind.Absolute))
            //    {
            //        browser.Url = new Uri(Path.GetFullPath(downloadPath + language + "/downloads.htm"));
            //    }
            //    else
            //        browser.Navigate(new Uri(downloadPath + language + "/downloads.htm"));
            //}
        }

        protected override void ReplaceVariables(HtmlWindow window, Uri url)
        {
            if (infoDownloader != null)
            {
                // create download page
                ReplaceVariablesDownload(window, url);
            }

            if (window.Document.Url == url)
            {
                // update variables 
                variables.UpdateHtml(window.Document);

                // last update settings
                settings.UpdateHtml(window.Document, true);
            }

            foreach (HtmlWindow frame in window.Frames)
                ReplaceVariables(frame, url);
        }

        protected void ReplaceVariablesDownload(HtmlWindow window, Uri url)
        {
            Burntime.Platform.IO.ConfigSection[] sections = downloads.GetAllSections();
            Burntime.Platform.IO.ConfigSection[] txts = text.GetAllSections();

            if (window.Document.Url == url)
            {
                HtmlElementCollection downloadSections = window.Document.GetElementsByTagName("download");
                if (downloadSections.Count == 1)
                {

                    HtmlElement downloadSection = downloadSections[0];
                    string innerHtml = downloadSection.InnerHtml;
                    downloadSection.InnerHtml = "";

                    for (int i = 1; i < sections.Length; i++)
                    {
                        downloadSection.InnerHtml += innerHtml;

                        Burntime.Platform.IO.ConfigSection section = sections[i];
                        Burntime.Platform.IO.ConfigSection txt = txts[i];

                        PackageInfo localPackage = Program.PackageManager.GetInfo(section.Name);
                        bool alreadyDownloaded = localPackage != null;
                        bool isUpdateable = false;// localPackage != null && section.GetVersion("version") > localPackage.Version;

                        Dictionary<string, bool> flags = new Dictionary<string, bool>();
                        flags.Add("alreadydownloaded", !isUpdateable && alreadyDownloaded);
                        flags.Add("update", isUpdateable);
                        flags.Add("download", !isUpdateable && !alreadyDownloaded);

                        // strings
                        foreach (HtmlElement element in window.Document.GetElementsByTagName("txt"))
                        {
                            string name = element.GetAttribute("name").ToLower();

                            if (name != "")
                                element.OuterHtml = txt.GetString(name);
                        }

                        // vars
                        foreach (HtmlElement element in window.Document.GetElementsByTagName("var"))
                        {
                            string name = element.GetAttribute("name").ToLower();

                            if (name != "")
                                element.OuterHtml = section.GetString(name);
                        }

                        // images
                        foreach (HtmlElement element in window.Document.GetElementsByTagName("img"))
                        {
                            string name = element.GetAttribute("name").ToLower();

                            if (name != "")
                            {
                                element.OuterHtml = "<img src=\"" + downloadPath + section.GetString(name) + "\"/>";
                            }
                        }

                        // if
                        foreach (HtmlElement element in window.Document.GetElementsByTagName("if"))
                        {
                            string name = element.GetAttribute("name").ToLower();

                            if (name != "")
                            {
                                if (flags.ContainsKey(name))
                                {
                                    if (flags[name])
                                    {
                                        element.OuterHtml = element.InnerHtml;
                                    }
                                    else
                                    {
                                        element.OuterHtml = "";
                                    }
                                }
                            }
                        }

                        // add pak name attribute to buttons
                        foreach (HtmlElement element in window.Document.GetElementsByTagName("button"))
                        {
                            string name = element.GetAttribute("command").ToLower();

                            if (name != "")
                            {
                                // ignore already set items
                                if (element.GetAttribute("pak") == "")
                                {
                                    // set pak name
                                    element.SetAttribute("pak", section.Name);
                                    element.SetAttribute("dep", section.GetString("dependencies"));

                                    // disable if base package not downloaded yet
                                    string bas = section.GetString("base");

                                    if (bas != "" && null == Program.PackageManager.GetInfo(bas))
                                    {
                                        element.SetAttribute("disabled", "true");
                                    }
                                }
                            }
                        }

                    }

                    // remove all loading elements
                    foreach (HtmlElement element in window.Document.GetElementsByTagName("loading"))
                    {
                        element.OuterHtml = "";
                    }

                    // finally display
                    downloadSection.Style = "visibility:visible";
                }

                // set selection
            }
        }

        enum UpdateMode
        {
            WriteSettings,
            ReadSettings,
            UpdateControls
        }

        private void UpdateAllPages(HtmlWindow window, UpdateMode mode)
        {
            switch (mode)
            {
                case UpdateMode.WriteSettings:
                    // update settings data
                    settings.UpdateData(window.Document);
                    break;
                case UpdateMode.ReadSettings:
                    break;
                case UpdateMode.UpdateControls:
                    // update controls (disabled state, ...)
                    settings.UpdateControls(window.Document);
                    break;
            }

            foreach (HtmlWindow frame in window.Frames)
                UpdateAllPages(frame, mode);
        }

        protected override void OnClick(object sender, HtmlElementEventArgs args)
        {
            HtmlElement element = sender as HtmlElement;

            string commandline = element.GetAttribute("command");
            if (commandline == "")
                return;

            string[] commands = commandline.Split(';');
            foreach (string command in commands)
            {

                string[] cmdargs = command.Split(' ');
                if (cmdargs.Length == 0)
                    return;

                switch (cmdargs[0])
                {
                    case "back":
                        browser.GoBack();
                        break;
                    case "save":
                        // save user settings
                        UpdateAllPages(browser.Document.Window, UpdateMode.WriteSettings);
                        package.SaveSettings();
                        break;
                    case "run":
                        //try
                        {
                            ProcessStartInfo startInfo = new ProcessStartInfo("system/burntime.exe");
                            startInfo.WorkingDirectory = "system";
                            startInfo.Arguments = command.Substring(3).Trim();
                            Process.Start(startInfo);
                        }
                        //catch
                        //{
                        //    MessageBox.Show(string.Format(Program.Text[""].Get("error1"), command), Program.Text[""].Get("error"));
                        //    return;
                        //}
                        break;
                    case "exit":
                        Application.Exit();
                        break;
                    case "download":
                        DownloadPackage(element.GetAttribute("pak"), element.GetAttribute("dep").Split(' '));
                        break;
                    case "update":
                        Program.Updater.InitiateUpdate();
                        break;
                    //case "save":
                    //    SaveSettings(browser.Document.Window);
                    //    Program.Settings.Save(Program.VFS.GetFile("user:settings.txt", Burntime.Platform.IO.FileOpenMode.Write));
                    //    break;
                    default:
                        MessageBox.Show(string.Format(Program.Text[""].Get("error1"), command), Program.Text[""].Get("error"));
                        return;
                }
            }
        }

        void DownloadPackage(string name, string[] dependencies)
        {
            Update.Updater updater = Program.Updater;
            List<Update.ModuleUpdateFilePath> download = new List<Update.ModuleUpdateFilePath>();

            // add package
            download.Add(updater.GetLatestVersion(name + ".pak"));

            // add dependent packages
            foreach (string str in dependencies)
            {
                if (str == "")
                    continue;

                // only add if not already available
                if (null == Program.PackageManager.GetInfo(str))
                    download.Add(updater.GetLatestVersion(str + ".pak"));
            }

            // download
            updater.DownloadFiles(download.ToArray(), updatePath + "files/", "");

            Application.UseWaitCursor = true;

            // refresh file system and view
            Program.RefreshVFS(true);
            RefreshPage(false);

            Application.UseWaitCursor = false;
        }
    }
}
