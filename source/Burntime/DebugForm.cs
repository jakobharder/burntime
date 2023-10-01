using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Burntime.Platform
{
    partial class DebugForm : Form
    {
        public DebugForm()
        {
            InitializeComponent();
        }

        delegate void SetInfoDelegate(string name, string info);
        public void SetInfo(string name, string info)
        {
            try
            {
                if (InvokeRequired)
                {
                    Invoke(new SetInfoDelegate(SetInfo), new object[] { name, info });
                }
                else
                {
                    for (int i = 0; i < dataGrid.Rows.Count; i++)
                    {
                        if ((string)dataGrid.Rows[i].Cells[0].Value == name)
                        {
                            dataGrid.Rows[i].Cells[1].Value = info;
                            return;
                        }
                    }

                    dataGrid.Rows.Add(new object[] { name, info });
                }
            }
            catch
            {
            }
        }
    }
}
