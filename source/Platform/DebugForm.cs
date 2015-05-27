/*
 *  Burntime Platform
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
 *  authors: 
 *    Juernjakob Harder (yn.harada@gmail.com)
 * 
*/

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
