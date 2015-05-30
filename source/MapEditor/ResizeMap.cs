
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

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
