/*
 *  Burntime MapEditor
 *  Copyright (C) 2009 Juernjakob Harder (原田ゆあん)
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
 *  contact: burntimedeluxe@gmail.com or yn.harada@gmail.com
 * 
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace MapEditor
{
    public partial class Import : Form
    {
        public int Set;
        public int SubSet;
        public int TileSize;

        public Import(List<TileSet> TileSets)
        {
            InitializeComponent();

            for (int i = 1; i < TileSets.Count; i++)
            {
                cmbSet.Items.Add(TileSets[i].Name);
            }

            cmbSet.SelectedIndex = 0;
            cmbTileSize.SelectedIndex = 0;
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            Set = cmbSet.SelectedIndex;
            SubSet = (int)numSubSet.Value;
            TileSize = int.Parse(cmbTileSize.SelectedItem.ToString());
        }

        private void cmbSet_SelectedIndexChanged(object sender, EventArgs e)
        {
            numSubSet.Enabled = (cmbSet.SelectedIndex != 0);
        }
    }
}
