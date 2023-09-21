namespace Burntime.Classic
{
    partial class ClassicSettings
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
            this.grpSettings = new System.Windows.Forms.GroupBox();
            this.lblValid = new System.Windows.Forms.Label();
            this.btnChoose = new System.Windows.Forms.Button();
            this.lblMail = new System.Windows.Forms.LinkLabel();
            this.txtPath = new System.Windows.Forms.TextBox();
            this.lblBurntimePath = new System.Windows.Forms.Label();
            this.lblPorted = new System.Windows.Forms.Label();
            this.lblPresentation = new System.Windows.Forms.Label();
            this.cmbPresentation = new System.Windows.Forms.ComboBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.grpSettings.SuspendLayout();
            this.SuspendLayout();
            // 
            // grpSettings
            // 
            this.grpSettings.Controls.Add(this.lblValid);
            this.grpSettings.Controls.Add(this.btnChoose);
            this.grpSettings.Controls.Add(this.lblMail);
            this.grpSettings.Controls.Add(this.txtPath);
            this.grpSettings.Controls.Add(this.lblBurntimePath);
            this.grpSettings.Controls.Add(this.lblPorted);
            this.grpSettings.Controls.Add(this.lblPresentation);
            this.grpSettings.Controls.Add(this.cmbPresentation);
            this.grpSettings.Location = new System.Drawing.Point(8, 8);
            this.grpSettings.Name = "grpSettings";
            this.grpSettings.Size = new System.Drawing.Size(280, 176);
            this.grpSettings.TabIndex = 0;
            this.grpSettings.TabStop = false;
            this.grpSettings.Text = "Settings";
            // 
            // lblValid
            // 
            this.lblValid.AutoSize = true;
            this.lblValid.Location = new System.Drawing.Point(8, 96);
            this.lblValid.Name = "lblValid";
            this.lblValid.Size = new System.Drawing.Size(0, 12);
            this.lblValid.TabIndex = 7;
            // 
            // btnChoose
            // 
            this.btnChoose.Location = new System.Drawing.Point(184, 96);
            this.btnChoose.Name = "btnChoose";
            this.btnChoose.Size = new System.Drawing.Size(88, 23);
            this.btnChoose.TabIndex = 6;
            this.btnChoose.Text = "Choose...";
            this.btnChoose.UseVisualStyleBackColor = true;
            this.btnChoose.Click += new System.EventHandler(this.btnChoose_Click);
            // 
            // lblMail
            // 
            this.lblMail.AutoSize = true;
            this.lblMail.Location = new System.Drawing.Point(8, 152);
            this.lblMail.Name = "lblMail";
            this.lblMail.Size = new System.Drawing.Size(140, 12);
            this.lblMail.TabIndex = 5;
            this.lblMail.TabStop = true;
            this.lblMail.Text = "burntimedeluxe@gmail.com";
            this.lblMail.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lblMail_LinkClicked);
            // 
            // txtPath
            // 
            this.txtPath.Location = new System.Drawing.Point(8, 72);
            this.txtPath.Name = "txtPath";
            this.txtPath.Size = new System.Drawing.Size(264, 19);
            this.txtPath.TabIndex = 3;
            this.txtPath.TextChanged += new System.EventHandler(this.txtPath_TextChanged);
            // 
            // lblBurntimePath
            // 
            this.lblBurntimePath.AutoSize = true;
            this.lblBurntimePath.Location = new System.Drawing.Point(8, 56);
            this.lblBurntimePath.Name = "lblBurntimePath";
            this.lblBurntimePath.Size = new System.Drawing.Size(80, 12);
            this.lblBurntimePath.TabIndex = 2;
            this.lblBurntimePath.Text = "Burntime Path:";
            // 
            // lblPorted
            // 
            this.lblPorted.AutoSize = true;
            this.lblPorted.Location = new System.Drawing.Point(8, 136);
            this.lblPorted.Name = "lblPorted";
            this.lblPorted.Size = new System.Drawing.Size(87, 12);
            this.lblPorted.TabIndex = 4;
            this.lblPorted.Text = "Ported by Juern";
            // 
            // lblPresentation
            // 
            this.lblPresentation.Location = new System.Drawing.Point(8, 24);
            this.lblPresentation.Name = "lblPresentation";
            this.lblPresentation.Size = new System.Drawing.Size(96, 20);
            this.lblPresentation.TabIndex = 1;
            this.lblPresentation.Text = "Presentation:";
            this.lblPresentation.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // cmbPresentation
            // 
            this.cmbPresentation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPresentation.FormattingEnabled = true;
            this.cmbPresentation.Location = new System.Drawing.Point(120, 24);
            this.cmbPresentation.Name = "cmbPresentation";
            this.cmbPresentation.Size = new System.Drawing.Size(153, 20);
            this.cmbPresentation.TabIndex = 0;
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(200, 192);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(88, 24);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnOK
            // 
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(104, 192);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(88, 24);
            this.btnOK.TabIndex = 2;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // ClassicSettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(295, 222);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.grpSettings);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ClassicSettings";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "ClassicSettings";
            this.grpSettings.ResumeLayout(false);
            this.grpSettings.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox grpSettings;
        private System.Windows.Forms.Label lblPresentation;
        private System.Windows.Forms.ComboBox cmbPresentation;
        private System.Windows.Forms.Button btnChoose;
        private System.Windows.Forms.TextBox txtPath;
        private System.Windows.Forms.Label lblBurntimePath;
        private System.Windows.Forms.Label lblPorted;
        private System.Windows.Forms.LinkLabel lblMail;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Label lblValid;
    }
}