
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
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using System.Diagnostics;

using Burntime.Framework;
using Burntime.Platform.IO;

namespace Burntime.Launcher
{
    partial class LauncherForm : Form
    {
        ConfigFile text;
        Version version;
        List<RadioButton> buttons = new List<RadioButton>();

        public LauncherForm()
        {
            ClientSize = new System.Drawing.Size(427, 294);

            // get engines version
            ConfigFile config = new ConfigFile();
            config.Open(Program.VFS.GetFile("launcher:info.txt", FileOpenMode.Read));
            version = config[""].GetVersion("version");

            // initialize language buttons
            InitializeLanguageButtons();

            // initialize from controls
            InitializeComponent();

            // initialize game tabs
            InitializeTabs();

            // load texts
            text = new ConfigFile();
            RefreshLanguage();

            Application.UseWaitCursor = false;
        }

        void InitializeTabs()
        {
            // add a tab for every game
            foreach (GamePackage gamePackage in Program.Games)
            {
                TabPage page = new GamePackageTabPage(gamePackage);
                tabControl.TabPages.Add(page);

                if (gamePackage.Name.Equals(Program.Settings["game"].Get("last"), StringComparison.InvariantCultureIgnoreCase))
                {
                    tabControl.SelectedIndex = tabControl.TabPages.Count - 1;
                }
            }

            //// add download tab
            //if (!Program.NoConnection)
            //    tabControl.TabPages.Add(new DownloadsTabPage());
        }

        public void InitializeLanguageButtons()
        {
            int buttonSize = 30;
            int buttonHeight = 21;
            int buttonMargin = 4;
            int buttonTop = 5;
            int buttonCount = Program.Languages.Length;
            int buttonOffset = (ClientSize.Width - (buttonSize * buttonCount + buttonMargin * (buttonCount - 1))) - 15;

            SuspendLayout();

            Controls.Remove(tabControl);

            if (buttons.Count != 0)
            {
                foreach (RadioButton b in buttons)
                {
                    Controls.Remove(b);
                }

                buttons.Clear();
            }

            // disable localization as we need images for all languages
            Program.VFS.Localized = false;

            RadioButton button;
            for (int i = 0; i < buttonCount; i++)
            {
                // load image
                Burntime.Platform.IO.File bitmapFile = Program.VFS.GetFile("flagg-" + Program.Languages[i] + ".png", FileOpenMode.Read);
                Bitmap bitmap = new Bitmap(bitmapFile.Stream);
                bitmapFile.Close();

                // create button
                button = new RadioButton();
                button.Left = buttonOffset + i * (buttonSize + buttonMargin);
                button.Top = buttonTop;
                button.Width = buttonSize;
                button.Height = buttonHeight;
                button.Tag = Program.Languages[i];
                button.Image = bitmap;
                button.Click += new EventHandler(btnLanguage_Click);
                button.Appearance = Appearance.Button;
                Controls.Add(button);

                // select the language button if its the current language
                if (Program.Languages[i].Equals(Program.Settings["game"].Get("language"), StringComparison.InvariantCultureIgnoreCase))
                    button.Checked = true;

                buttons.Add(button);
            }

            // enable localization
            Program.VFS.Localized = true;

            Controls.Add(tabControl);

            ResumeLayout();
        }

        private void LauncherForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            foreach (GamePackage package in Program.Games)
                package.Dispose();
        }

        private void btnLanguage_Click(object sender, EventArgs e)
        {
            RadioButton button = sender as RadioButton;

            // set new language
            Program.VFS.LocalizationCode = button.Tag as string;
            Program.Settings["game"].Set("language", button.Tag as string);

            // refresh launcher main form
            RefreshLanguage();

            // save settings
            Program.Settings.Save(Program.VFS.GetFile("user:settings.txt", FileOpenMode.Write));
        }

        public void RefreshLanguage()
        {
            Program.RefreshLanguage();

            // refresh data
            foreach (GamePackage game in Program.Games)
                game.RefreshData();

            // refresh tabs
            foreach (TabPage page in tabControl.TabPages)
            {
                if (page is GamePackageTabPage)
                {
                    (page as GamePackageTabPage).RefreshPage(true);
                }
                else if (page is DownloadsTabPage)
                {
                    (page as DownloadsTabPage).RefreshPage(true);
                }
            }

            // reopen file
            text.Open(Program.VFS.GetFile("lang.txt", FileOpenMode.Read));

            // set title
            Text = string.Format(text[""].GetString("title"), version.ToString());
        }

        private void tabControl_Selected(object sender, TabControlEventArgs e)
        {
            GamePackageTabPage page = e.TabPage as GamePackageTabPage;

            // do not save if the last game tab is selected ("More")
            if (page == null || page == tabControl.TabPages[tabControl.TabPages.Count - 1])
                return;

            // save last selected game tab
            Program.Settings["game"].Set("last", page.Package.Name);
            Program.Settings.Save(Program.VFS.GetFile("user:settings.txt", Burntime.Platform.IO.FileOpenMode.Write));
        }
    }
}
