using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Burntime.Autoupdate
{
    public partial class UpdateProgress : Form
    {
        bool finished;

        public float Progress
        {
            set
            {
                progressBar.Value = (int)(value * 100);
            }
        }

        public string Message
        {
            set
            {
                lblMessage.Text = value;
            }
        }

        public UpdateProgress()
        {
            InitializeComponent();

            //progressBar.Minimum = 0;
            //progressBar.Maximum = 100;
        }

        public void Finish()
        {
            finished = true;
            btnAbort.Text = "Close";
        }

        private void btnAbort_Click(object sender, EventArgs e)
        {
            DialogResult = finished ? DialogResult.OK : DialogResult.Abort;
            Close();
        }
    }
}
