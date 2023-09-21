using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace Burntime.Launcher.Update
{
    public partial class UpdateProgress : Form
    {
        bool finished;
        bool started;
        BackgroundWorker worker;
        DownloadInstructions instr;
        string section;

        public float Progress
        {
            set
            {
                int v = (int)(value * 100);
                progressBar.Value = System.Math.Min(100, System.Math.Max(0, v));
            }
        }

        public string Message
        {
            set
            {
                lblMessage.Text = value;
            }
        }

        public UpdateProgress(string section)
        {
            InitializeComponent();

            this.section = section;
            Text = Program.Text[section].Get("title");
            btnAbort.Text = Program.Text[section].Get("cancelbutton");
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (!started)
            {
                started = true;
                worker.RunWorkerAsync(instr);
            }
        }

        internal DialogResult Run(BackgroundWorker worker, DownloadInstructions instr)
        {
            this.worker = worker;
            this.instr = instr;
            return ShowDialog();
        }

        public void Finish()
        {
            lock (this)
            {
                finished = true;
                btnAbort.Text = Program.Text[section].Get("close");
                btnAbort.Enabled = true;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            lock (this)
            {
                if (!finished && worker != null)
                {
                    worker.CancelAsync();
                    e.Cancel = true;

                    btnAbort.Enabled = false;
                }
            }
        }

        private void btnAbort_Click(object sender, EventArgs e)
        {
            lock (this)
            {
                if (finished)
                {
                    Close();
                }
                else if (worker != null)
                {
                    worker.CancelAsync();

                    btnAbort.Enabled = false;
                }
            }
        }
    }
}
