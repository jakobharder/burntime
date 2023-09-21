using System;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;

using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Common;

namespace Burntime.Classic
{
    public partial class ClassicLauncher : Form
    {
        static readonly string gamepath = "game/classic";
        static readonly string launcher = "launcher/launcher.txt";
        static readonly bool enableMulti = false;

        ClassicMultiplayer server = null;
        BurntimePath path;
        TextResourceFile txt;
        Language language;
        bool debugMode;
        bool debugStart;

        public bool DebugStart
        {
            get { return debugStart; }
        }

        public ClassicLauncher(bool debugMode)
        {
            this.debugMode = debugMode;

            InitializeComponent();

            FileSystem.UseLocalization = false;
            FileSystem.BasePath = debugMode ? "../" : "";
            FileSystem.AddPackage("system", "system");
            FileSystem.AddPackage("classic", gamepath);
            FileSystem.SetUserFolder("Burntime");

            language = new Language("launcher/");

            ConfigFile config = new ConfigFile();
            Burntime.Platform.IO.File file = FileSystem.GetFile("system:settings.txt");
            config.Open(file);
            language.CurrentID = config["game"].GetString("language");
            file.Close();

            FileSystem.UseLocalization = true;
            FileSystem.LocalizationCode = language.CurrentID;

            file = FileSystem.GetFile(launcher);
            txt = new TextResourceFile(new Burntime.Platform.IO.File(file));

            Text = txt.Data[0];
            btnSingle.Text = txt.Data[5];
            btnMulti.Text = txt.Data[6];
            btnOptions.Text = txt.Data[7];
            btnExit.Text = txt.Data[8];
            bugReport.Text = txt.Data[9];

            path = new BurntimePath(FileSystem.BasePath + "system");
            btnSingle.Enabled = path.IsValid;
            btnMulti.Enabled = path.IsValid & enableMulti;

            int buttonSize = 30;
            int buttonHeight = 19;
            int buttonMargin = 4;
            int buttonTop = 110;
            int buttonCount = language.Languages.Length;
            int buttonOffset = (ClientSize.Width - (buttonSize * buttonCount + buttonMargin * (buttonCount - 1))) / 2;
            

            Button button;
            
            for (int i = 0; i < buttonCount; i++)
            {   
                button = new Button();
                button.Left = buttonOffset + i * (buttonSize + buttonMargin);
                button.Top = buttonTop;
                button.Width = buttonSize;
                button.Height = buttonHeight;
                button.Tag = language.Languages[i].ID;
                button.Image = language.Languages[i].Icon;
                button.Click += new EventHandler(btnLanguage_Click);
                Controls.Add(button);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            Burntime.Autoupdate.Updater updater = new Burntime.Autoupdate.Updater(debugMode ? "../" : "");
            lblVersion.Text = updater.LocalVersion;

            if (!updater.AlwaysCheck)
            {
                downloadUpdate.Text = "check for new version";
                downloadUpdate.Visible = true;
            }
            else
                downloadUpdate.Visible = false;


            if (updater.AlwaysCheck && updater.CheckForUpdates())
            {
                if (updater.AlwaysAsk)
                {
                    if (updater.ShowDownloadQuestion())
                    {
                        // update
                        updater.InitiateUpdate();
                        //requestRestart = true;

                        Close();
                        return;
                    }
                }

                lblVersion.Text = updater.LocalVersion;
                downloadUpdate.Text = "update to " + updater.VersionAvailable;
                downloadUpdate.Visible = true;
            }

            if (!path.IsValid)
            {
                if (DialogResult.OK == MessageBox.Show(txt.Data[21] + "\n" + txt.Data[22], txt.Data[23], MessageBoxButtons.OKCancel, MessageBoxIcon.Information))
                {
                    do
                    {
                        if (DialogResult.OK != path.ShowSelector())
                            break;

                        btnSingle.Enabled = path.IsValid;
                        btnMulti.Enabled = path.IsValid & enableMulti;
                        if (btnSingle.Enabled)
                            btnSingle.Focus();

                        if (!path.IsValid)
                            MessageBox.Show(txt.Data[24], txt.Data[23], MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        else
                            path.Save();
                    } while (!path.IsValid);
                }
            }
        }

        private void btnSingle_Click(object sender, EventArgs e)
        {
            if (debugMode)
            {
                FileSystem.Clear();
                debugStart = true;

                Close();
            }
            else
            {
                ProcessStartInfo startInfo = new ProcessStartInfo("burntime.exe");
                startInfo.WorkingDirectory = "system";
                startInfo.Arguments = "classic";
                Process.Start(startInfo);
            }
            Close();
        }

        private void btnMulti_Click(object sender, EventArgs e)
        {
            server = new ClassicMultiplayer(txt);
            server.Show();
            Close();
        }

        private void btnOptions_Click(object sender, EventArgs e)
        {
            ClassicSettings dlg = new ClassicSettings(path, txt);
            dlg.ShowDialog();

            btnSingle.Enabled = path.IsValid;
            btnMulti.Enabled = path.IsValid & enableMulti;
            if (btnSingle.Enabled)
                btnSingle.Focus();
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnLanguage_Click(object sender, EventArgs e)
        {
            language.CurrentID = (sender as Button).Tag as String;
            
            FileSystem.LocalizationCode = language.CurrentID;
            Burntime.Platform.IO.File file = FileSystem.GetFile(launcher);
            txt = new TextResourceFile(file);

            Text = txt.Data[0];
            btnSingle.Text = txt.Data[5];
            btnMulti.Text = txt.Data[6];
            btnOptions.Text = txt.Data[7];
            btnExit.Text = txt.Data[8];
            bugReport.Text = txt.Data[9];

            // set user folder to systems settings.txt
            FileSystem.SetUserFolder("Burntime");

            ConfigFile config = new ConfigFile();
            file = FileSystem.GetFile("system:settings.txt");
            config.Open(file);
            config["game"].Set("language", language.CurrentID);
            file.Close();
            file = FileSystem.CreateFile("user:settings.txt");
            config.Save(file);
            file.Close(); 
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (server == null || !server.Visible)
                Application.Exit();
        }

        private void bugReport_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ProcessStartInfo sInfo = new ProcessStartInfo("http://code.google.com/p/burntimedeluxe/issues/entry");
            Process.Start(sInfo);
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ProcessStartInfo sInfo = new ProcessStartInfo("http://www.burntime.org");
            Process.Start(sInfo);
        }

        private void downloadUpdate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Burntime.Autoupdate.Updater updater = new Burntime.Autoupdate.Updater(debugMode ? "../" : "");

            if (updater.CheckForUpdates())
            {
                if (updater.ShowDownloadQuestion())
                {
                    // update
                    updater.InitiateUpdate();
                    //requestRestart = true;

                    Close();
                    return;
                }

                lblVersion.Text = updater.LocalVersion;
                downloadUpdate.Text = "update to " + updater.VersionAvailable;
                downloadUpdate.Visible = true;
            }
            else
            {
                MessageBox.Show("No new version available.");
                downloadUpdate.Visible = false;
            }
        }
    }
}
