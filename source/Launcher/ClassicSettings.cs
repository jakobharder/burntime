/*
 *  Burntime Classic Launcher
 *  Copyright (C) 2009
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
 *  contact: 
 *    juern - burntimedeluxe@gmail.com or yn.harada@gmail.com
 * 
 *  authors: 
 *    Juernjakob Harder - 原田ゆあん (yn.harada@gmail.com)
 * 
*/

using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

using Burntime.Platform;
using Burntime.Common;
using Burntime.Platform.IO;

namespace Burntime.Classic
{
    public partial class ClassicSettings : Form
    {
        static readonly String settings = "settings.txt";

        BurntimePath path;
        TextResourceFile txt;
        String lang;

        public ClassicSettings(BurntimePath path, TextResourceFile txt)
        {
            InitializeComponent();

            this.path = path;
            this.txt = txt;

            // set user folder to systems settings.txt
            FileSystem.SetUserFolder("Burntime");

            ConfigFile config = new ConfigFile();
            Burntime.Platform.IO.File file = FileSystem.GetFile("system:settings.txt");
            config.Open(file);
            lang = config["game"].GetString("language");
            file.Close();

            // set user folder to game specific location
            FileSystem.SetUserFolder("Burntime/Classic");

            file = FileSystem.GetFile(settings);
            config.Open(file);
            file.Close();

            Text = txt.Data[0];
            grpSettings.Text = txt.Data[10];
            lblPresentation.Text = txt.Data[11];
            cmbPresentation.Items.Add(txt.Data[12]);
            cmbPresentation.Items.Add(txt.Data[13]);
            cmbPresentation.Items.Add(txt.Data[14]);
            lblBurntimePath.Text = txt.Data[15];
            btnChoose.Text = txt.Data[16];
            lblPorted.Text = txt.Data[17];
            lblMail.Text = txt.Data[18];
            btnOK.Text = txt.Data[3];
            btnCancel.Text = txt.Data[4];

            if (config["system"].GetBool("windowmode"))
            {
                Vector2 res = config["system"].GetVector2("resolution");
                cmbPresentation.SelectedIndex = (res.x == 960 && res.y == 600) ? 2 : 1;
            }
            else
            {
                cmbPresentation.SelectedIndex = 0;
            }


            UpdatePathInfo();

            txtPath.Text = path.Path;
        }

        void UpdatePathInfo()
        {
            if (txtPath.Text == "")
            {
                lblValid.Text = "";
            }
            else
            {
                if (path.IsValid)
                {
                    lblValid.Text = txt.Data[19];
                    lblValid.ForeColor = Color.Blue;
                }
                else
                {
                    lblValid.Text = txt.Data[20];
                    lblValid.ForeColor = Color.Red;
                }
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            path.Save();
            
            ConfigFile config = new ConfigFile();
            Burntime.Platform.IO.File file = file = FileSystem.GetFile(settings);
            config.Open(file);
            file.Close();

            if (cmbPresentation.SelectedIndex == 0)
            {
                config["system"].Set("windowmode", false);
            }
            else
            {
                config["system"].Set("windowmode", true);
                if (cmbPresentation.SelectedIndex == 2)
                    config["system"].Set("resolution", new Vector2(960, 600));
                else
                    config["system"].Set("resolution", new Vector2(640, 400));
            }

            FileSystem.RemoveFile(settings);
            file = FileSystem.CreateFile(settings);
            config.Save(file);
            file.Close();

            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void txtPath_TextChanged(object sender, EventArgs e)
        {
            path.Path = txtPath.Text;
            UpdatePathInfo();
        }

        private void btnChoose_Click(object sender, EventArgs e)
        {
            if (DialogResult.OK == path.ShowSelector())
                txtPath.Text = path.Path;
        }

        private void lblMail_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ProcessStartInfo sInfo = new ProcessStartInfo("mailto:burntimedeluxe@gmail.com");
            Process.Start(sInfo);
        }
    }
}
