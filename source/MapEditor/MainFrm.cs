using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Drawing.Imaging;

namespace MapEditor
{
    public partial class MainFrm : Form, IMapView
    {
        MapDocument document = null;
        Tile activeTile = null;
        List<TileSet> allTiles = new List<TileSet>();
        List<ListView> tileViews = new List<ListView>();

        TabPage entranceEditPage;
        TabPage wayEditPage;
        TabPage walkableEditPage;
        TabPage[] tilePages;

        String burntimePath;
        bool burntimePathOK;
        Button burntimePathButton;

        public MainFrm()
        {
            Cursor = Cursors.WaitCursor;

            InitializeComponent();

            entranceEditPage = editTabs.TabPages[0];
            wayEditPage = editTabs.TabPages[1];
            walkableEditPage = editTabs.TabPages[2];

            burntimePath = "burntime";
            burntimePathOK = true;

            PopulateTiles();
            UpdateTitle();

            mapWindow.Mode = MapWindow.EditMode.Tile;
            mapWindow.AttachedView = this;
            mapWindow.ClickEntrance += new EventHandler(mapWindow_ClickEntrance);
            mapWindow.Click += new EventHandler(mapWindow_Click);
            mapWindow.RightClick += new EventHandler(mapWindow_RightClick);
            mapWindow.TilePicked += MapWindow_TilePicked;

            mapWindow.AllwaysShowEntrances = showEntrances.Checked;
            mapWindow.AllwaysShowWays = showWaysToolStripMenuItem.Checked;
            mapWindow.AllwaysShowWayConnections = showWayConnectionsToolStripMenuItem.Checked;
            mapWindow.AllwaysShowWalkable = showWalkableToolStripMenuItem.Checked;

            Cursor = Cursors.Default;
        }

        private void MapWindow_TilePicked(object sender, EventArgs e)
        {
            var tile = mapWindow.Tile;
            SwitchMode(MapWindow.EditMode.Tile);

            for (int tileset = 0; tileset < tileViews.Count; tileset++)
            {
                for (int tileId = 0; tileId < allTiles[tileset].Tiles.Count; tileId++)
                {
                    if (tile != null && allTiles[tileset].Tiles[tileId] == tile)
                    {
                        activeTile = tile;
                        editTabs.SelectedIndex = tileset;
                        tileViews[tileset].Items[tileId].Selected = true;
                        return;
                    }
                    else if (tile == null && tileViews[tileset].Items[tileId].Selected)
                    {
                        tileViews[tileset].Items[tileId].Selected = false;
                    }
                }
            }

            activeTile = null;
            mapWindow.Tile = null;
        }

        void mapWindow_RightClick(object sender, EventArgs e)
        {
            btnAddPoint.Checked = false;
            btnAddEntrance.Checked = false;
        }

        public bool CheckBurntimePath()
        {
            return File.Exists(burntimePath + "\\burn_gfx\\zei_001.raw");
        }

        public void SelectBurntimePath()
        {
            burntimePathOK = false;

            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.ShowNewFolderButton = false;
            while (!burntimePathOK && DialogResult.OK == dlg.ShowDialog())
            {
                burntimePath = dlg.SelectedPath;
                burntimePathOK = CheckBurntimePath();
                if (!burntimePathOK)
                {
                    MessageBox.Show("Could not find burntime.", "Burntime Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    TextWriter writer = new StreamWriter("path.txt");
                    writer.WriteLine(burntimePath);
                    writer.Close();

                    PopulateClassicTiles();

                    if (burntimePathButton != null)
                        burntimePathButton.Visible = false;
                }
            }

            burntimePathOK = CheckBurntimePath();
        }

        public void UpdateTitle()
        {
            if (document != null)
            {
                Text = "Burntime MapEditor - " + document.Title + (document.Saved ? "" : "*") + " [" + document.Size.Width + "x" + document.Size.Height + "]";
                
                exportToolStripMenuItem.Enabled = true;
                closeToolStripMenuItem.Enabled = true;
                saveAsToolStripMenuItem.Enabled = true;
                saveToolStripMenuItem.Enabled = true;
                btnSave.Enabled = true;
                resizeMapToolStripMenuItem.Enabled = true;
                btnAddEntrance.Enabled = true;
                btnRemoveEntrance.Enabled = mapWindow.IsEntranceSelected;
                btnAddPoint.Enabled = mapWindow.IsWaySelected;
                btnAddWay.Enabled = mapWindow.IsEntranceSelected;
                numDays.Enabled = mapWindow.IsWaySelected;
                btnRemoveWay.Enabled = mapWindow.IsWaySelected;

                btnTileMode.Enabled = true;
                btnEntranceMode.Enabled = true;
                btnWayMode.Enabled = true;
                btnWalkableMode.Enabled = true;
            }
            else
            {
                Text = "Burntime MapEditor";
                
                exportToolStripMenuItem.Enabled = false;
                closeToolStripMenuItem.Enabled = false;
                saveAsToolStripMenuItem.Enabled = false;
                saveToolStripMenuItem.Enabled = false;
                btnSave.Enabled = false;
                resizeMapToolStripMenuItem.Enabled = false;
                btnAddEntrance.Enabled = false;
                btnRemoveEntrance.Enabled = false;
                btnAddPoint.Enabled = false;
                btnAddWay.Enabled = false;
                numDays.Enabled = false;
                btnRemoveWay.Enabled = false;

                btnTileMode.Checked = true;
                btnEntranceMode.Checked = false;
                btnWayMode.Checked = false;
                btnWalkableMode.Checked = false;

                SwitchMode(MapWindow.EditMode.Tile);
            }

            btnSaveWalkable.Enabled = TileManager.Changed;
        }

        public void UpdateMap()
        {
            mapWindow.UpdateMap();
        }

        public void UpdateObjects()
        {
            mapWindow.UpdateObjects();
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OnNewDocument();
        }

        void PopulateTiles()
        {
            SuspendLayout();

            TileManager.LoadTiles(allTiles);

            if (burntimePathOK)
            {
                TileManager.LoadClassicTiles(allTiles[0], burntimePath);
            }

            editTabs.TabPages.Clear();

            foreach (TileSet set in allTiles)
            {
                editTabs.TabPages.Add(set.Name);

                ListView view = new ListView();
                view.Left = 0;
                view.Top = 0;
                view.Width = editTabs.TabPages[editTabs.TabPages.Count - 1].ClientSize.Width;
                view.Height = editTabs.TabPages[editTabs.TabPages.Count - 1].ClientSize.Height;
                view.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
                view.TileSize = new Size(38, 38);
                view.View = View.Tile;
                view.LargeImageList = new ImageList();
                view.LargeImageList.ImageSize = new Size(32, 32);
                view.ShowItemToolTips = true;
                editTabs.TabPages[editTabs.TabPages.Count - 1].Controls.Add(view);

                view.LargeImageList.ColorDepth = ColorDepth.Depth24Bit;
                foreach (Tile tile in set.Tiles)
                    view.LargeImageList.Images.Add(tile.Image);

                for (int i = 0; i < set.Tiles.Count; i++)
                {
                    Tile tile = set.Tiles[i];

                    ListViewItem item = new ListViewItem();
                    item.ImageIndex = i;
                    item.ToolTipText = "[" + tile.SubSet.ToString("D3") + "x" + tile.ID.ToString("D2") + "]";

                    view.Items.Add(item);
                }

                tileViews.Add(view);

                if (set.Name == "classic")
                {
                    if (!burntimePathOK)
                    {
                        view.Visible = false;
                        burntimePathButton = new Button();
                        burntimePathButton.Size = new Size(128, 24);
                        burntimePathButton.Location = new Point(8, 8);
                        burntimePathButton.Text = "Select Burntime path...";
                        burntimePathButton.Click += new EventHandler(selectBurntimePathToolStripMenuItem_Click);
                        editTabs.TabPages[editTabs.TabPages.Count - 1].Controls.Add(burntimePathButton);
                    }
                }
            }

            // add another tab "more" explaining tiles location
            editTabs.TabPages.Add("more");
            Label text = new Label();
            text.Location = new Point(8, 8);
            text.Text = "Add more tiles:\n- create a new folder under \"tiles\"\n - store tiles in format \"setname/000_00.png\"";
            text.AutoSize = true;
            editTabs.TabPages[editTabs.TabPages.Count - 1].Controls.Add(text);

            tilePages = new TabPage[editTabs.TabPages.Count];
            for (int i = 0; i < editTabs.TabPages.Count; i++)
            {
                tilePages[i] = editTabs.TabPages[i];
            }

            ResumeLayout(false);

            foreach (TileSet set in allTiles)
            {
                set.Sort();
            }
        }

        void PopulateClassicTiles()
        {
            Cursor = Cursors.WaitCursor;

            if (burntimePathOK && allTiles[0].Tiles.Count == 0)
            {
                TileManager.LoadClassicTiles(allTiles[0], burntimePath);
                TileSet set = allTiles[0];
                ListView view = tileViews[0];

                view.LargeImageList.ColorDepth = ColorDepth.Depth24Bit;
                foreach (Tile tile in set.Tiles)
                    view.LargeImageList.Images.Add(tile.Image);

                for (int i = 0; i < set.Tiles.Count; i++)
                {
                    Tile tile = set.Tiles[i];

                    ListViewItem item = new ListViewItem();
                    item.ImageIndex = i;
                    item.ToolTipText = "[" + tile.SubSet.ToString("D3") + "x" + tile.ID.ToString("D2") + "]";

                    view.Items.Add(item);
                }

                view.Visible = true;
            }

            allTiles[0].Sort();

            Cursor = Cursors.Default;
        }

        bool OnSaveAsDocument()
        {
            String old = document.FilePath;
            document.FilePath = null;

            if (!OnSaveDocument())
            {
                document.FilePath = old;
                return false;
            }

            return true;
        }

        bool OnSaveDocument()
        {
            if (document.FilePath == null)
            {
                SaveFileDialog dlg = new SaveFileDialog();
                dlg.Filter = "Burntime Map (*.burnmap)|*.burnmap||";
                dlg.AddExtension = true;
                dlg.FileName = document.Title;

                if (DialogResult.OK != dlg.ShowDialog())
                    return false;

                document.FilePath = dlg.FileName;
                document.Title = Path.GetFileNameWithoutExtension(dlg.FileName);
            }

            document.Save(document.FilePath, allTiles);
            return true;
        }

        bool OnCloseDocument()
        {
            if (document != null)
            {
                if (!document.Saved)
                {
                    DialogResult res = MessageBox.Show("There are unsaved changes in \"" + document.Title + "\".\nSave now?", "Close", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (res == DialogResult.Cancel)
                        return false;

                    if (res == DialogResult.Yes)
                        if (!OnSaveDocument())
                            return false;
                }

                document.Close();
                document = null;

                mapWindow.SetDocument(null);

                UpdateTitle();
            }

            return true;
        }

        private void mapWindow_MouseEnter(object sender, EventArgs e)
        {
            if (mapWindow.Mode == MapWindow.EditMode.Tile)
            {
                if (editTabs.SelectedIndex != -1 && editTabs.SelectedIndex < tileViews.Count && tileViews[editTabs.SelectedIndex].SelectedIndices.Count > 0)
                {
                    activeTile = allTiles[editTabs.SelectedIndex].Tiles[tileViews[editTabs.SelectedIndex].SelectedIndices[0]];
                }
                else
                    activeTile = null;

            }
            else
                activeTile = null;

            mapWindow.Tile = activeTile;
        }

        private void mapWindow_MouseLeave(object sender, EventArgs e)
        {
            activeTile = null;
            mapWindow.Tile = activeTile;
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OnCloseDocument();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OnOpenDocument();
        }

        void OnNewDocument()
        {
            if (!OnCloseDocument())
                return;

            NewMap dlg = new NewMap();
            if (DialogResult.OK == dlg.ShowDialog())
            {
                document = new MapDocument(this);
                document.New(dlg.MapSize, dlg.TileSize);

                mapWindow.SetDocument(document);
            }
        }

        void OnOpenDocument()
        {
            if (!OnCloseDocument())
                return;

            mapWindow.Tile = null;

            OpenFileDialog dlg = new OpenFileDialog();
            dlg.CheckFileExists = true;
            dlg.CheckPathExists = true;
            dlg.Filter = "Burntime Map (*.burnmap)|*.burnmap";

            if (DialogResult.OK == dlg.ShowDialog())
            {
                document = new MapDocument(this);
                if (!document.Open(dlg.FileName, allTiles))
                {
                    MessageBox.Show("Error while opening \"" + dlg.FileName + "\"", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    document = null;
                    return;
                }
                document.FilePath = dlg.FileName;
                document.Title = Path.GetFileNameWithoutExtension(dlg.FileName);
                UpdateTitle();

                mapWindow.SetDocument(document);
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OnSaveDocument();
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OnSaveAsDocument();
        }

        private void SwitchMode(MapWindow.EditMode mode)
        {
            mapWindow.Mode = mode;
            btnTileMode.Checked = mode == MapWindow.EditMode.Tile;
            btnEntranceMode.Checked = mode == MapWindow.EditMode.Entrance;
            btnWalkableMode.Checked = mode == MapWindow.EditMode.Walkable;
            btnWayMode.Checked = mode == MapWindow.EditMode.Way;
            btnPickerMode.Checked = mode == MapWindow.EditMode.Picker;
            editTabs.TabPages.Clear();

            switch (mode)
            {
                case MapWindow.EditMode.Tile:
                    for (int i = 0; i < tilePages.Length; i++)
                        editTabs.TabPages.Add(tilePages[i]);
                    break;
                case MapWindow.EditMode.Entrance:
                    editTabs.TabPages.Add(entranceEditPage);
                    break;
                case MapWindow.EditMode.Walkable:
                    editTabs.TabPages.Add(walkableEditPage);
                    break;
                case MapWindow.EditMode.Way:
                    editTabs.TabPages.Add(wayEditPage);
                    break;
            }

            btnAddEntrance.Checked = false;
            btnAddWay.Checked = false;
            btnAddPoint.Checked = false;

            switch (mode)
            {
                case MapWindow.EditMode.Way:
                    btnAddWay.Enabled = false;
                    numDays.Enabled = false;
                    numDays.Value = 1;
                    break;
            }
        }

        private void btnRemoveEntrance_Click(object sender, EventArgs e)
        {
            mapWindow.RemoveSelectedEntrance();
        }

        private void resizeMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (document != null)
            {
                ResizeMap dlg = new ResizeMap(document.Size, document.TileSize);
                if (DialogResult.OK == dlg.ShowDialog())
                {
                    document.Resize(dlg.MapSize, dlg.Offset, dlg.TileSize);
                }
            }
        }

        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!OnCloseDocument())
                e.Cancel = true;

            base.OnClosing(e);
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox dlg = new AboutBox();
            dlg.ShowDialog();
        }

        private void pngToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Png Image (*.png)|*.png";
            dlg.AddExtension = true;

            if (DialogResult.OK != dlg.ShowDialog())
                return;

            mapWindow.MapImage.Save(dlg.FileName);
        }

        private void burntimerawToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for (int y = 0; y < document.Size.Height; y++)
            {
                for (int x = 0; x < document.Size.Width; x++)
                {
                    if (document.GetTile(x, y) == null || document.GetTile(x, y).Set != "classic")
                    {
                        MessageBox.Show("Export to .raw map is only allowed with classic tiles.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }

            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Burntime Raw (mat_*.raw)|mat_*.raw";
            dlg.Title = "Select map to replace...";

            if (DialogResult.OK != dlg.ShowDialog())
                return;

            document.Export(dlg.FileName);
        }

        private void selectBurntimePathToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectBurntimePath();
        }

        private void btnTileMode_Click(object sender, EventArgs e)
        {
            SwitchMode(MapWindow.EditMode.Tile);
        }

        private void btnEntranceMode_Click(object sender, EventArgs e)
        {
            SwitchMode(MapWindow.EditMode.Entrance);
        }

        private void btnWalkableMode_Click(object sender, EventArgs e)
        {
            SwitchMode(MapWindow.EditMode.Walkable);
        }

        private void btnWayMode_Click(object sender, EventArgs e)
        {
            SwitchMode(MapWindow.EditMode.Way);
        }

        private void btnPickerMode_Click(object sender, EventArgs e)
        {
            SwitchMode(MapWindow.EditMode.Picker);
        }

        int addWayEntrance = -1;
        private void btnAddWay_Click(object sender, EventArgs e)
        {
            addWayEntrance = mapWindow.SelectedEntrance;
            btnAddPoint.Checked = false;
            btnAddEntrance.Checked = false;
        }

        void mapWindow_ClickEntrance(object sender, EventArgs e)
        {
            if (btnAddEntrance.Checked)
                btnAddEntrance.Checked = false;
            if (btnAddPoint.Checked)
                btnAddPoint.Checked = false;

            btnAddWay.Enabled = mapWindow.IsEntranceSelected && mapWindow.Mode == MapWindow.EditMode.Way;

            if (btnAddWay.Checked)
            {
                Way way = new Way();
                way.Entrance[0] = addWayEntrance;
                way.Entrance[1] = mapWindow.SelectedEntrance;

                if (way.Entrance[0] != way.Entrance[1])
                {
                    document.AddWay(way);
                }
                btnAddWay.Checked = false;
            }
        }

        void mapWindow_Click(object sender, EventArgs e)
        {
            MapClickEventArgs args = e as MapClickEventArgs;

            if (btnAddPoint.Checked)
            {
                document.Ways[mapWindow.SelectedWay].Points.Add(args.Position);
                document.Saved = false;
            }
            else if (btnAddEntrance.Checked)
            {
                document.AddEntrance(new Rectangle(args.Position.X - 8, args.Position.Y - 8, 16, 16));
            }

            if (mapWindow.IsWaySelected)
            {
                btnAddPoint.Enabled = true;
                numDays.Enabled = false;
                numDays.Value = document.Ways[mapWindow.SelectedWay].Days;
            }
            else
            {
                numDays.Enabled = false;
                btnAddPoint.Enabled = false;
            }
        }

        private void btnAddEntrance_Click(object sender, EventArgs e)
        {
            btnAddWay.Checked = false;
            btnAddPoint.Checked = false;
        }

        private void btnAddPoint_Click(object sender, EventArgs e)
        {
            btnAddEntrance.Checked = false;
            btnAddWay.Checked = false;
        }

        private void numDays_ValueChanged(object sender, EventArgs e)
        {
            if (mapWindow.IsWaySelected)
            {
                document.Ways[mapWindow.SelectedWay].Days = (int)numDays.Value;
                mapWindow.UpdateObjects();
                document.Saved = false;
            }
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            newToolStripMenuItem_Click(sender, e);
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            openToolStripMenuItem_Click(sender, e);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            saveToolStripMenuItem_Click(sender, e);
        }

        private void showEntrances_Click(object sender, EventArgs e)
        {
            mapWindow.AllwaysShowEntrances = showEntrances.Checked;
        }

        private void showWaysToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mapWindow.AllwaysShowWays = showWaysToolStripMenuItem.Checked;
        }

        private void showWayConnectionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mapWindow.AllwaysShowWayConnections = showWayConnectionsToolStripMenuItem.Checked;
        }

        private void showWalkableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mapWindow.AllwaysShowWalkable = showWalkableToolStripMenuItem.Checked;
        }

        private void btnRemoveWay_Click(object sender, EventArgs e)
        {
            mapWindow.RemoveSelectedWay();
            btnAddPoint.Checked = false;
        }

        private void btnSaveWalkable_Click(object sender, EventArgs e)
        {
            TileManager.Save();
            btnSaveWalkable.Enabled = TileManager.Changed;
        }

        private void importAsNewMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!OnCloseDocument())
                return;

            mapWindow.Tile = null;

            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Title = "Select Burntime RAW or PNG image...";
            dlg.Filter = "All Formats|mat_*.raw;*.png;*.bmp|Burntime Raw (mat_*.raw)|mat_*.raw|Image (*.png, *.bmp)|*.png;*.bmp";
            dlg.CheckFileExists = true;
            dlg.CheckPathExists = true;
            if (DialogResult.OK == dlg.ShowDialog())
            {
                if (dlg.FileName.EndsWith(".raw", StringComparison.InvariantCultureIgnoreCase))
                {
                    FileStream file = new FileStream(dlg.FileName, FileMode.Open);

                    // check & size selection dialog
                    document = new MapDocument(this);
                    document.Import(file, 32, allTiles[0]);
                    document.Title = Path.GetFileNameWithoutExtension(dlg.FileName);
                    UpdateTitle();

                    mapWindow.SetDocument(document);

                    file.Dispose();
                }
                else
                {
                    var sharedTiles = allTiles;

                    //Import dlg2 = new Import(allTiles);
                    //if (DialogResult.OK != dlg2.ShowDialog())
                    //    return;

                    Cursor = Cursors.WaitCursor;

                    var addSet = new TileSet() { Name = "_", LastId = Tile.LAST_ID };

                    document = new MapDocument(this);
                    document.Import(dlg.FileName, 32, sharedTiles, addSet);
                    document.Title = Path.GetFileNameWithoutExtension(dlg.FileName);
                    UpdateTitle();

                    if (addSet?.Tiles.Count > 0)
                    {
                        document.CustomTiles = addSet;
                    }

                    mapWindow.SetDocument(document);

                    Cursor = Cursors.Default;
                }
            }
        }

        private void convertToMegaTileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string directory = Path.GetDirectoryName(mapWindow.Document.FilePath);
            string fileName = Path.GetFileNameWithoutExtension(mapWindow.Document.FilePath);

            string filePath = Path.Combine(directory, fileName);

            mapWindow.Document.ConvertToMegaTile();
            mapWindow.MapImage.Save(filePath + ".png");
        }

        private void updateUpscaledTilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new()
            {
                Title = "Select upscaled PNG image...",
                Filter = "Image (*.png)|*.png",
                CheckFileExists = true,
                CheckPathExists = true
            };
            if (DialogResult.OK == dlg.ShowDialog())
            {
                mapWindow.Document.UpdateUpscaled(dlg.FileName, allTiles, mapWindow.MapImage);
            }
        }
    }
}
