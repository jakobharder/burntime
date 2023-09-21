using System;
using System.Windows.Forms;
using System.Net;

using Burntime.Platform;

namespace Burntime.Classic
{
    public partial class ClassicMultiplayer : Form
    {
        public ClassicMultiplayer(TextResourceFile txt)
        {
            InitializeComponent();

            btnClose.Text = txt.Data[8];
            Text = txt.Data[25];
            tabControl.TabPages[0].Text = txt.Data[26];
            tabControl.TabPages[1].Text = txt.Data[27];
            lblYourIP.Text = txt.Data[28];

            IPHostEntry ip = Dns.GetHostEntry(Dns.GetHostName());

            IPAddress address = null;
            foreach (IPAddress a in ip.AddressList)
            {
                if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    address = a;
                    break;
                }
            }
            lblIP.Text = address == null ? "" : address.ToString();
            lblYourName.Text = txt.Data[29];
            lblName.Text = Dns.GetHostName();

            btnOpen.Text = txt.Data[30];
            btnStart.Text = txt.Data[32];

            btnOpen.Enabled = false;
            listPlayer.Enabled = false;
            btnStart.Enabled = false;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Application.Exit();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
        }

        private void btnStart_Click(object sender, EventArgs e)
        {

        }
    }
}
