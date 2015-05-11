using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.ComponentModel;

using Burntime.Framework;
using System.IO;

namespace Burntime.Launcher
{
    class DownloadsTabPage : BrowserTabPage
    {
        string downloadPath;
        string updatePath;

        string[] supportedLanguage;
        string defaultLanguage;
        string language;

        Burntime.Platform.IO.ConfigFile downloads;
        Burntime.Platform.IO.ConfigFile text;
        bool readyFlag;
        BackgroundWorker infoDownloader;

        public DownloadsTabPage()
        {
            Text = Program.Text[""].Get("tab_downloads");

            downloadPath = Program.Settings["autoupdate"].Get("downloads");
            updatePath = Program.Settings["autoupdate"].Get("location");

            infoDownloader = new BackgroundWorker();
            infoDownloader.DoWork += new DoWorkEventHandler(infoDownloader_DoWork);
            infoDownloader.RunWorkerCompleted += new RunWorkerCompletedEventHandler(infoDownloader_RunWorkerCompleted);
            infoDownloader.RunWorkerAsync();
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

        public void RefreshPage(bool changedLanguage)
        {
            if (!readyFlag || supportedLanguage == null)
                return;

            lock (this)
            {
                if (changedLanguage && !infoDownloader.IsBusy)
                    infoDownloader.RunWorkerAsync();
            }

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

            Text = Program.Text[""].Get("tab_downloads");

            if (Program.VersionControls.Count > 0)
            {
                // show update page
                if (!Uri.IsWellFormedUriString(updatePath + language + "/update.htm", UriKind.Absolute))
                {
                    browser.Url = new Uri(Path.GetFullPath(updatePath + language + "/update.htm"));
                }
                else
                    browser.Url = new Uri(updatePath + language + "/update.htm");
            }
            else
            {
                // show download page
                if (!Uri.IsWellFormedUriString(downloadPath + language + "/downloads.htm", UriKind.Absolute))
                {
                    browser.Url = new Uri(Path.GetFullPath(downloadPath + language + "/downloads.htm"));
                }
                else
                    browser.Navigate(new Uri(downloadPath + language + "/downloads.htm"));
            }
        }

        protected override void ReplaceVariables(HtmlWindow window, Uri url)
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
                                element.OuterHtml = "<img src=\"../" + section.GetString(name) + "\"/>";
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
                foreach (HtmlElement element in window.Document.GetElementsByTagName("option"))
                {
                    if (CheckSettingsConditions(element.GetAttribute("value")))
                    {
                        element.SetAttribute("selected", "true");
                    }
                }
                foreach (HtmlElement element in window.Document.GetElementsByTagName("input"))
                {
                    if (element.GetAttribute("type").Equals("checkbox", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // add click handler
                        element.SetAttribute("command", "save");
                        element.Click += new HtmlElementEventHandler(OnClick);

                        if (CheckSettingsConditions(element.GetAttribute("value")))
                            element.SetAttribute("checked", "true");
                    }
                }
            }

            foreach (HtmlWindow frame in window.Frames)
                ReplaceVariables(frame, url);
        }

        private bool CheckSettingsConditions(string conditions)
        {
            string[] values = conditions.Split(';');

            foreach (string condition in values)
            {
                string[] key_value = condition.Trim().Split('=');
                if (key_value.Length != 2)
                    return false;

                string key = key_value[0].Trim();
                string val = key_value[1].Trim();

                string[] key_section = key.Split('@');
                if (key_section.Length != 2)
                    return false;

                key = key_section[0];
                string section = key_section[1];

                string user_value = Program.Settings[section].Get(key);

                if (!user_value.Equals(val, StringComparison.InvariantCultureIgnoreCase))
                    return false;
            }

            return true;
        }

        private void SaveSettings(HtmlWindow window)
        {
            // save selection
            foreach (HtmlElement element in window.Document.GetElementsByTagName("option"))
            {
                string selected = element.GetAttribute("selected");
                if (selected.Equals("true", StringComparison.InvariantCultureIgnoreCase))
                {
                    SetSettings(element.GetAttribute("value"));
                }
            }

            foreach (HtmlElement element in window.Document.GetElementsByTagName("input"))
            {
                if (element.GetAttribute("type").Equals("checkbox", StringComparison.InvariantCultureIgnoreCase))
                {
                    string selected = element.GetAttribute("checked");
                    if (selected.Equals("true", StringComparison.InvariantCultureIgnoreCase))
                    {
                        SetSettings(element.GetAttribute("value"));
                    }
                    else
                        SetSettings(element.GetAttribute("not"));
                }
            }

            foreach (HtmlWindow frame in window.Frames)
                SaveSettings(frame);
        }

        private void SetSettings(string settings)
        {
            string[] values = settings.Split(';');

            foreach (string condition in values)
            {
                string[] key_value = condition.Trim().Split('=');
                if (key_value.Length != 2)
                    return;

                string key = key_value[0].Trim();
                string val = key_value[1].Trim();

                string[] key_section = key.Split('@');
                if (key_section.Length != 2)
                    return;

                key = key_section[0];
                string section = key_section[1];

                Program.Settings[section].Set(key, val);
            }
        }

        protected override void OnClick(object sender, HtmlElementEventArgs args)
        {
            HtmlElement element = sender as HtmlElement;
            switch (element.GetAttribute("command"))
            {
                case "download":
                    DownloadPackage(element.GetAttribute("pak"), element.GetAttribute("dep").Split(' '));
                    break;
                case "update":
                    Program.Updater.InitiateUpdate();
                    break;
                case "save":
                    SaveSettings(browser.Document.Window);
                    Program.Settings.Save(Program.VFS.GetFile("user:settings.txt", Burntime.Platform.IO.FileOpenMode.Write));
                    break;
            }
        }

        void DownloadPackage(string name, string[] dependencies)
        {
            //Update.Updater updater = new Burntime.Launcher.Update.Updater();
            //List<Update.ModuleUpdateFilePath> download = new List<Update.ModuleUpdateFilePath>();

            //// add package
            //download.Add(name + ".pak");

            //// add dependent packages
            //foreach (string str in dependencies)
            //{
            //    if (str == "")
            //        continue;

            //    // only add if not already available
            //    if (null == Program.PackageManager.GetInfo(str))
            //        download.Add(str + ".pak");
            //}

            //// download
            //updater.DownloadFiles(download.ToArray(), updatePath + "pak/", "game/");

            //Application.UseWaitCursor = true;

            //// refresh file system and view
            //Program.RefreshVFS(true);
            //RefreshPage(false);

            //Application.UseWaitCursor = false;
        }
    }
}
