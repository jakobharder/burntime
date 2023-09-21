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
