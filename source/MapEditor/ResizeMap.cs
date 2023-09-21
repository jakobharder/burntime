using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace MapEditor
{
    public partial class ResizeMap : Form
    {
        public Size MapSize;
        public int TileSize;
        public Point Offset;
        Size oldSize;
        bool changed = false;
        bool auto = false;

        public ResizeMap(Size Size, int TileSize)
        {
            oldSize = Size;

            InitializeComponent();
            cmbTileSize.SelectedIndex = 0;

            cmbTileSize.SelectedItem = TileSize.ToString();
            numWidth.Value = Size.Width;
            numHeight.Value = Size.Height;
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            MapSize = new Size((int)numWidth.Value, (int)numHeight.Value);
            Offset = new Point((int)numLeft.Value, (int)numTop.Value);
            TileSize = int.Parse(cmbTileSize.SelectedItem.ToString());
        }

        private void numWidth_ValueChanged(object sender, EventArgs e)
        {
            auto = true;

            numLeft.Maximum = numWidth.Value - 1;
            numLeft.Minimum = -oldSize.Width + 1;

            if (!changed)
                numLeft.Value = (int)(numWidth.Value - oldSize.Width) / 2;

            auto = false;
        }

        private void numHeight_ValueChanged(object sender, EventArgs e)
        {
            auto = true;

            numTop.Maximum = numHeight.Value - 1;
            numTop.Minimum = -oldSize.Height + 1;

            if (!changed)
                numTop.Value = (int)(numHeight.Value - oldSize.Height) / 2;

            auto = false;
        }

        private void numLeft_ValueChanged(object sender, EventArgs e)
        {
            if (!auto)
                changed = true;
        }

        private void numTop_ValueChanged(object sender, EventArgs e)
        {
            if (!auto)
                changed = true;
        }
    }
}
