using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace MapEditor
{
    public partial class NewMap : Form
    {
        public Size MapSize;
        public int TileSize;

        public NewMap()
        {
            InitializeComponent();
            cmbTileSize.SelectedIndex = 0;
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            MapSize = new Size((int)numWidth.Value, (int)numHeight.Value);
            TileSize = int.Parse(cmbTileSize.SelectedItem.ToString());
        }
    }
}
