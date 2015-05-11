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

namespace Burntime.Classic
{
    partial class ClassicLauncher
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ClassicLauncher));
            this.btnSingle = new System.Windows.Forms.Button();
            this.btnMulti = new System.Windows.Forms.Button();
            this.btnOptions = new System.Windows.Forms.Button();
            this.btnExit = new System.Windows.Forms.Button();
            this.bugReport = new System.Windows.Forms.LinkLabel();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.lblVersion = new System.Windows.Forms.Label();
            this.downloadUpdate = new System.Windows.Forms.LinkLabel();
            this.SuspendLayout();
            // 
            // btnSingle
            // 
            this.btnSingle.Location = new System.Drawing.Point(63, 146);
            this.btnSingle.Name = "btnSingle";
            this.btnSingle.Size = new System.Drawing.Size(192, 32);
            this.btnSingle.TabIndex = 0;
            this.btnSingle.Text = "Singleplayer";
            this.btnSingle.UseVisualStyleBackColor = true;
            this.btnSingle.Click += new System.EventHandler(this.btnSingle_Click);
            // 
            // btnMulti
            // 
            this.btnMulti.Location = new System.Drawing.Point(63, 186);
            this.btnMulti.Name = "btnMulti";
            this.btnMulti.Size = new System.Drawing.Size(192, 32);
            this.btnMulti.TabIndex = 1;
            this.btnMulti.Text = "Multiplayer";
            this.btnMulti.UseVisualStyleBackColor = true;
            this.btnMulti.Click += new System.EventHandler(this.btnMulti_Click);
            // 
            // btnOptions
            // 
            this.btnOptions.Location = new System.Drawing.Point(63, 226);
            this.btnOptions.Name = "btnOptions";
            this.btnOptions.Size = new System.Drawing.Size(192, 32);
            this.btnOptions.TabIndex = 2;
            this.btnOptions.Text = "Settings";
            this.btnOptions.UseVisualStyleBackColor = true;
            this.btnOptions.Click += new System.EventHandler(this.btnOptions_Click);
            // 
            // btnExit
            // 
            this.btnExit.Location = new System.Drawing.Point(63, 266);
            this.btnExit.Name = "btnExit";
            this.btnExit.Size = new System.Drawing.Size(192, 32);
            this.btnExit.TabIndex = 3;
            this.btnExit.Text = "Exit";
            this.btnExit.UseVisualStyleBackColor = true;
            this.btnExit.Click += new System.EventHandler(this.btnExit_Click);
            // 
            // bugReport
            // 
            this.bugReport.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.bugReport.AutoSize = true;
            this.bugReport.Location = new System.Drawing.Point(8, 310);
            this.bugReport.Name = "bugReport";
            this.bugReport.Size = new System.Drawing.Size(56, 12);
            this.bugReport.TabIndex = 4;
            this.bugReport.TabStop = true;
            this.bugReport.Text = "linkLabel1";
            this.bugReport.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.bugReport_LinkClicked);
            // 
            // linkLabel1
            // 
            this.linkLabel1.AutoSize = true;
            this.linkLabel1.Location = new System.Drawing.Point(216, 308);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(93, 12);
            this.linkLabel1.TabIndex = 5;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Text = "www.burntime.org";
            this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // lblVersion
            // 
            this.lblVersion.AutoSize = true;
            this.lblVersion.BackColor = System.Drawing.Color.Transparent;
            this.lblVersion.ForeColor = System.Drawing.Color.White;
            this.lblVersion.Location = new System.Drawing.Point(8, 8);
            this.lblVersion.Name = "lblVersion";
            this.lblVersion.Size = new System.Drawing.Size(33, 12);
            this.lblVersion.TabIndex = 6;
            this.lblVersion.Text = "0.1";
            // 
            // downloadUpdate
            // 
            this.downloadUpdate.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.downloadUpdate.AutoSize = true;
            this.downloadUpdate.BackColor = System.Drawing.Color.Transparent;
            this.downloadUpdate.Location = new System.Drawing.Point(8, 24);
            this.downloadUpdate.Name = "downloadUpdate";
            this.downloadUpdate.Size = new System.Drawing.Size(56, 12);
            this.downloadUpdate.TabIndex = 7;
            this.downloadUpdate.TabStop = true;
            this.downloadUpdate.Text = "linkLabel1";
            this.downloadUpdate.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.downloadUpdate_LinkClicked);
            // 
            // ClassicLauncher
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("$this.BackgroundImage")));
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.ClientSize = new System.Drawing.Size(319, 329);
            this.Controls.Add(this.downloadUpdate);
            this.Controls.Add(this.lblVersion);
            this.Controls.Add(this.linkLabel1);
            this.Controls.Add(this.bugReport);
            this.Controls.Add(this.btnExit);
            this.Controls.Add(this.btnOptions);
            this.Controls.Add(this.btnMulti);
            this.Controls.Add(this.btnSingle);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ClassicLauncher";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Classic";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnSingle;
        private System.Windows.Forms.Button btnMulti;
        private System.Windows.Forms.Button btnOptions;
        private System.Windows.Forms.Button btnExit;
        private System.Windows.Forms.LinkLabel bugReport;
        private System.Windows.Forms.LinkLabel linkLabel1;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.LinkLabel downloadUpdate;
    }
}