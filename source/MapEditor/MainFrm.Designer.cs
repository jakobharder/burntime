namespace MapEditor
{
    partial class MainFrm
    {
        /// <summary>
        /// 必要なデザイナ変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージ リソースが破棄される場合 true、破棄されない場合は false です。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows フォーム デザイナで生成されたコード

        /// <summary>
        /// デザイナ サポートに必要なメソッドです。このメソッドの内容を
        /// コード エディタで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainFrm));
            this.menu = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.newToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveAsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem3 = new System.Windows.Forms.ToolStripSeparator();
            this.closeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripSeparator();
            this.importToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importAsNewMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.updateTilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.updateUpscaledTilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.convertToMegaTileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pngToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.burntimerawToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem4 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showEntrances = new System.Windows.Forms.ToolStripMenuItem();
            this.showWaysToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showWayConnectionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showWalkableToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.resizeMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.selectBurntimePathToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editTabs = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.btnAddEntrance = new System.Windows.Forms.CheckBox();
            this.btnRemoveEntrance = new System.Windows.Forms.Button();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.btnRemoveWay = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.numDays = new System.Windows.Forms.NumericUpDown();
            this.btnAddWay = new System.Windows.Forms.CheckBox();
            this.btnAddPoint = new System.Windows.Forms.CheckBox();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.btnSaveWalkable = new System.Windows.Forms.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.mapWindow = new MapEditor.MapWindow();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.btnNew = new System.Windows.Forms.ToolStripButton();
            this.btnOpen = new System.Windows.Forms.ToolStripButton();
            this.btnSave = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.btnTileMode = new System.Windows.Forms.ToolStripButton();
            this.btnPickerMode = new System.Windows.Forms.ToolStripButton();
            this.btnEntranceMode = new System.Windows.Forms.ToolStripButton();
            this.btnWayMode = new System.Windows.Forms.ToolStripButton();
            this.btnWalkableMode = new System.Windows.Forms.ToolStripButton();
            this.menu.SuspendLayout();
            this.editTabs.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numDays)).BeginInit();
            this.tabPage3.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.toolStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // menu
            // 
            this.menu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.viewToolStripMenuItem,
            this.editToolStripMenuItem,
            this.helpToolStripMenuItem});
            this.menu.Location = new System.Drawing.Point(0, 0);
            this.menu.Name = "menu";
            this.menu.Padding = new System.Windows.Forms.Padding(7, 2, 0, 2);
            this.menu.Size = new System.Drawing.Size(976, 24);
            this.menu.TabIndex = 0;
            this.menu.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newToolStripMenuItem,
            this.openToolStripMenuItem,
            this.toolStripMenuItem1,
            this.saveToolStripMenuItem,
            this.saveAsToolStripMenuItem,
            this.toolStripMenuItem3,
            this.closeToolStripMenuItem,
            this.toolStripMenuItem2,
            this.importToolStripMenuItem,
            this.convertToMegaTileToolStripMenuItem,
            this.exportToolStripMenuItem,
            this.toolStripMenuItem4,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // newToolStripMenuItem
            // 
            this.newToolStripMenuItem.Name = "newToolStripMenuItem";
            this.newToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.newToolStripMenuItem.Text = "New...";
            this.newToolStripMenuItem.Click += new System.EventHandler(this.newToolStripMenuItem_Click);
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.openToolStripMenuItem.Text = "Open...";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(179, 6);
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.saveToolStripMenuItem.Text = "Save";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.saveToolStripMenuItem_Click);
            // 
            // saveAsToolStripMenuItem
            // 
            this.saveAsToolStripMenuItem.Name = "saveAsToolStripMenuItem";
            this.saveAsToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.saveAsToolStripMenuItem.Text = "Save as...";
            this.saveAsToolStripMenuItem.Click += new System.EventHandler(this.saveAsToolStripMenuItem_Click);
            // 
            // toolStripMenuItem3
            // 
            this.toolStripMenuItem3.Name = "toolStripMenuItem3";
            this.toolStripMenuItem3.Size = new System.Drawing.Size(179, 6);
            // 
            // closeToolStripMenuItem
            // 
            this.closeToolStripMenuItem.Name = "closeToolStripMenuItem";
            this.closeToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.closeToolStripMenuItem.Text = "Close";
            this.closeToolStripMenuItem.Click += new System.EventHandler(this.closeToolStripMenuItem_Click);
            // 
            // toolStripMenuItem2
            // 
            this.toolStripMenuItem2.Name = "toolStripMenuItem2";
            this.toolStripMenuItem2.Size = new System.Drawing.Size(179, 6);
            // 
            // importToolStripMenuItem
            // 
            this.importToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.importAsNewMapToolStripMenuItem,
            this.updateTilesToolStripMenuItem,
            this.updateUpscaledTilesToolStripMenuItem});
            this.importToolStripMenuItem.Name = "importToolStripMenuItem";
            this.importToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.importToolStripMenuItem.Text = "Import";
            // 
            // importAsNewMapToolStripMenuItem
            // 
            this.importAsNewMapToolStripMenuItem.Name = "importAsNewMapToolStripMenuItem";
            this.importAsNewMapToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.importAsNewMapToolStripMenuItem.Text = "Import as new map...";
            this.importAsNewMapToolStripMenuItem.Click += new System.EventHandler(this.importAsNewMapToolStripMenuItem_Click);
            // 
            // updateTilesToolStripMenuItem
            // 
            this.updateTilesToolStripMenuItem.Name = "updateTilesToolStripMenuItem";
            this.updateTilesToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.updateTilesToolStripMenuItem.Text = "Update tiles...";
            // 
            // updateUpscaledTilesToolStripMenuItem
            // 
            this.updateUpscaledTilesToolStripMenuItem.Name = "updateUpscaledTilesToolStripMenuItem";
            this.updateUpscaledTilesToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.updateUpscaledTilesToolStripMenuItem.Text = "Update upscaled tiles...";
            this.updateUpscaledTilesToolStripMenuItem.Click += new System.EventHandler(this.updateUpscaledTilesToolStripMenuItem_Click);
            // 
            // convertToMegaTileToolStripMenuItem
            // 
            this.convertToMegaTileToolStripMenuItem.Name = "convertToMegaTileToolStripMenuItem";
            this.convertToMegaTileToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.convertToMegaTileToolStripMenuItem.Text = "Convert to mega tile";
            this.convertToMegaTileToolStripMenuItem.Click += new System.EventHandler(this.convertToMegaTileToolStripMenuItem_Click);
            // 
            // exportToolStripMenuItem
            // 
            this.exportToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.pngToolStripMenuItem,
            this.burntimerawToolStripMenuItem});
            this.exportToolStripMenuItem.Name = "exportToolStripMenuItem";
            this.exportToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.exportToolStripMenuItem.Text = "Export";
            this.exportToolStripMenuItem.Click += new System.EventHandler(this.exportToolStripMenuItem_Click);
            // 
            // pngToolStripMenuItem
            // 
            this.pngToolStripMenuItem.Name = "pngToolStripMenuItem";
            this.pngToolStripMenuItem.Size = new System.Drawing.Size(157, 22);
            this.pngToolStripMenuItem.Text = "Png Image...";
            this.pngToolStripMenuItem.Click += new System.EventHandler(this.pngToolStripMenuItem_Click);
            // 
            // burntimerawToolStripMenuItem
            // 
            this.burntimerawToolStripMenuItem.Name = "burntimerawToolStripMenuItem";
            this.burntimerawToolStripMenuItem.Size = new System.Drawing.Size(157, 22);
            this.burntimerawToolStripMenuItem.Text = "Burntime Raw...";
            this.burntimerawToolStripMenuItem.Click += new System.EventHandler(this.burntimerawToolStripMenuItem_Click);
            // 
            // toolStripMenuItem4
            // 
            this.toolStripMenuItem4.Name = "toolStripMenuItem4";
            this.toolStripMenuItem4.Size = new System.Drawing.Size(179, 6);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.showEntrances,
            this.showWaysToolStripMenuItem,
            this.showWayConnectionsToolStripMenuItem,
            this.showWalkableToolStripMenuItem});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.viewToolStripMenuItem.Text = "View";
            // 
            // showEntrances
            // 
            this.showEntrances.Checked = true;
            this.showEntrances.CheckOnClick = true;
            this.showEntrances.CheckState = System.Windows.Forms.CheckState.Checked;
            this.showEntrances.Name = "showEntrances";
            this.showEntrances.Size = new System.Drawing.Size(195, 22);
            this.showEntrances.Text = "Show entrances";
            this.showEntrances.Click += new System.EventHandler(this.showEntrances_Click);
            // 
            // showWaysToolStripMenuItem
            // 
            this.showWaysToolStripMenuItem.CheckOnClick = true;
            this.showWaysToolStripMenuItem.Name = "showWaysToolStripMenuItem";
            this.showWaysToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.showWaysToolStripMenuItem.Text = "Show ways";
            this.showWaysToolStripMenuItem.Click += new System.EventHandler(this.showWaysToolStripMenuItem_Click);
            // 
            // showWayConnectionsToolStripMenuItem
            // 
            this.showWayConnectionsToolStripMenuItem.Checked = true;
            this.showWayConnectionsToolStripMenuItem.CheckOnClick = true;
            this.showWayConnectionsToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.showWayConnectionsToolStripMenuItem.Name = "showWayConnectionsToolStripMenuItem";
            this.showWayConnectionsToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.showWayConnectionsToolStripMenuItem.Text = "Show way connections";
            this.showWayConnectionsToolStripMenuItem.Click += new System.EventHandler(this.showWayConnectionsToolStripMenuItem_Click);
            // 
            // showWalkableToolStripMenuItem
            // 
            this.showWalkableToolStripMenuItem.CheckOnClick = true;
            this.showWalkableToolStripMenuItem.Name = "showWalkableToolStripMenuItem";
            this.showWalkableToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.showWalkableToolStripMenuItem.Text = "Show walkable";
            this.showWalkableToolStripMenuItem.Click += new System.EventHandler(this.showWalkableToolStripMenuItem_Click);
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.resizeMapToolStripMenuItem});
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(39, 20);
            this.editToolStripMenuItem.Text = "Edit";
            // 
            // resizeMapToolStripMenuItem
            // 
            this.resizeMapToolStripMenuItem.Name = "resizeMapToolStripMenuItem";
            this.resizeMapToolStripMenuItem.Size = new System.Drawing.Size(142, 22);
            this.resizeMapToolStripMenuItem.Text = "Resize map...";
            this.resizeMapToolStripMenuItem.Click += new System.EventHandler(this.resizeMapToolStripMenuItem_Click);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.selectBurntimePathToolStripMenuItem,
            this.aboutToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "Misc";
            // 
            // selectBurntimePathToolStripMenuItem
            // 
            this.selectBurntimePathToolStripMenuItem.Name = "selectBurntimePathToolStripMenuItem";
            this.selectBurntimePathToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
            this.selectBurntimePathToolStripMenuItem.Text = "Select Burntime path...";
            this.selectBurntimePathToolStripMenuItem.Click += new System.EventHandler(this.selectBurntimePathToolStripMenuItem_Click);
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
            this.aboutToolStripMenuItem.Text = "About...";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutToolStripMenuItem_Click);
            // 
            // editTabs
            // 
            this.editTabs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.editTabs.Controls.Add(this.tabPage1);
            this.editTabs.Controls.Add(this.tabPage2);
            this.editTabs.Controls.Add(this.tabPage3);
            this.editTabs.Location = new System.Drawing.Point(649, 4);
            this.editTabs.Margin = new System.Windows.Forms.Padding(4);
            this.editTabs.Name = "editTabs";
            this.editTabs.SelectedIndex = 0;
            this.editTabs.Size = new System.Drawing.Size(304, 506);
            this.editTabs.TabIndex = 2;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.btnAddEntrance);
            this.tabPage1.Controls.Add(this.btnRemoveEntrance);
            this.tabPage1.Location = new System.Drawing.Point(4, 24);
            this.tabPage1.Margin = new System.Windows.Forms.Padding(4);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(4);
            this.tabPage1.Size = new System.Drawing.Size(296, 478);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Entrance";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // btnAddEntrance
            // 
            this.btnAddEntrance.Appearance = System.Windows.Forms.Appearance.Button;
            this.btnAddEntrance.Location = new System.Drawing.Point(9, 10);
            this.btnAddEntrance.Margin = new System.Windows.Forms.Padding(4);
            this.btnAddEntrance.Name = "btnAddEntrance";
            this.btnAddEntrance.Size = new System.Drawing.Size(149, 30);
            this.btnAddEntrance.TabIndex = 7;
            this.btnAddEntrance.Text = "Add";
            this.btnAddEntrance.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnAddEntrance.UseVisualStyleBackColor = true;
            this.btnAddEntrance.Click += new System.EventHandler(this.btnAddEntrance_Click);
            // 
            // btnRemoveEntrance
            // 
            this.btnRemoveEntrance.Location = new System.Drawing.Point(9, 50);
            this.btnRemoveEntrance.Margin = new System.Windows.Forms.Padding(4);
            this.btnRemoveEntrance.Name = "btnRemoveEntrance";
            this.btnRemoveEntrance.Size = new System.Drawing.Size(149, 30);
            this.btnRemoveEntrance.TabIndex = 1;
            this.btnRemoveEntrance.Text = "Remove";
            this.btnRemoveEntrance.UseVisualStyleBackColor = true;
            this.btnRemoveEntrance.Click += new System.EventHandler(this.btnRemoveEntrance_Click);
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.btnRemoveWay);
            this.tabPage2.Controls.Add(this.label1);
            this.tabPage2.Controls.Add(this.numDays);
            this.tabPage2.Controls.Add(this.btnAddWay);
            this.tabPage2.Controls.Add(this.btnAddPoint);
            this.tabPage2.Location = new System.Drawing.Point(4, 24);
            this.tabPage2.Margin = new System.Windows.Forms.Padding(4);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Size = new System.Drawing.Size(296, 478);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Way";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // btnRemoveWay
            // 
            this.btnRemoveWay.Location = new System.Drawing.Point(9, 50);
            this.btnRemoveWay.Margin = new System.Windows.Forms.Padding(4);
            this.btnRemoveWay.Name = "btnRemoveWay";
            this.btnRemoveWay.Size = new System.Drawing.Size(149, 30);
            this.btnRemoveWay.TabIndex = 11;
            this.btnRemoveWay.Text = "Remove way";
            this.btnRemoveWay.UseVisualStyleBackColor = true;
            this.btnRemoveWay.Click += new System.EventHandler(this.btnRemoveWay_Click);
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(9, 140);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(47, 24);
            this.label1.TabIndex = 10;
            this.label1.Text = "Days:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // numDays
            // 
            this.numDays.Location = new System.Drawing.Point(56, 140);
            this.numDays.Margin = new System.Windows.Forms.Padding(4);
            this.numDays.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numDays.Name = "numDays";
            this.numDays.Size = new System.Drawing.Size(103, 23);
            this.numDays.TabIndex = 9;
            this.numDays.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numDays.ValueChanged += new System.EventHandler(this.numDays_ValueChanged);
            // 
            // btnAddWay
            // 
            this.btnAddWay.Appearance = System.Windows.Forms.Appearance.Button;
            this.btnAddWay.Location = new System.Drawing.Point(9, 10);
            this.btnAddWay.Margin = new System.Windows.Forms.Padding(4);
            this.btnAddWay.Name = "btnAddWay";
            this.btnAddWay.Size = new System.Drawing.Size(149, 30);
            this.btnAddWay.TabIndex = 8;
            this.btnAddWay.Text = "Add way";
            this.btnAddWay.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnAddWay.UseVisualStyleBackColor = true;
            this.btnAddWay.Click += new System.EventHandler(this.btnAddWay_Click);
            // 
            // btnAddPoint
            // 
            this.btnAddPoint.Appearance = System.Windows.Forms.Appearance.Button;
            this.btnAddPoint.Location = new System.Drawing.Point(9, 90);
            this.btnAddPoint.Margin = new System.Windows.Forms.Padding(4);
            this.btnAddPoint.Name = "btnAddPoint";
            this.btnAddPoint.Size = new System.Drawing.Size(149, 30);
            this.btnAddPoint.TabIndex = 7;
            this.btnAddPoint.Text = "Add point";
            this.btnAddPoint.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnAddPoint.UseVisualStyleBackColor = true;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.label3);
            this.tabPage3.Controls.Add(this.label2);
            this.tabPage3.Controls.Add(this.btnSaveWalkable);
            this.tabPage3.Location = new System.Drawing.Point(4, 24);
            this.tabPage3.Margin = new System.Windows.Forms.Padding(4);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(296, 478);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Walkable";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(9, 70);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(0, 15);
            this.label3.TabIndex = 14;
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(9, 50);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(149, 100);
            this.label2.TabIndex = 13;
            this.label2.Text = "Walkable info is stored in the tile database. Any change will affect other maps.";
            // 
            // btnSaveWalkable
            // 
            this.btnSaveWalkable.Location = new System.Drawing.Point(9, 10);
            this.btnSaveWalkable.Margin = new System.Windows.Forms.Padding(4);
            this.btnSaveWalkable.Name = "btnSaveWalkable";
            this.btnSaveWalkable.Size = new System.Drawing.Size(149, 30);
            this.btnSaveWalkable.TabIndex = 12;
            this.btnSaveWalkable.Text = "Save";
            this.btnSaveWalkable.UseVisualStyleBackColor = true;
            this.btnSaveWalkable.Click += new System.EventHandler(this.btnSaveWalkable_Click);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 67.46032F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 32.53968F));
            this.tableLayoutPanel1.Controls.Add(this.mapWindow, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.editTabs, 1, 0);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(9, 70);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(4);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(957, 514);
            this.tableLayoutPanel1.TabIndex = 3;
            // 
            // mapWindow
            // 
            this.mapWindow.AllwaysShowEntrances = false;
            this.mapWindow.AllwaysShowWalkable = false;
            this.mapWindow.AllwaysShowWayConnections = false;
            this.mapWindow.AllwaysShowWays = false;
            this.mapWindow.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.mapWindow.AttachedView = null;
            this.mapWindow.AutoScroll = true;
            this.mapWindow.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.mapWindow.Location = new System.Drawing.Point(5, 5);
            this.mapWindow.Margin = new System.Windows.Forms.Padding(5);
            this.mapWindow.Mode = MapEditor.MapWindow.EditMode.Tile;
            this.mapWindow.Name = "mapWindow";
            this.mapWindow.Size = new System.Drawing.Size(635, 504);
            this.mapWindow.TabIndex = 1;
            this.mapWindow.MouseEnter += new System.EventHandler(this.mapWindow_MouseEnter);
            this.mapWindow.MouseLeave += new System.EventHandler(this.mapWindow_MouseLeave);
            // 
            // toolStrip
            // 
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnNew,
            this.btnOpen,
            this.btnSave,
            this.toolStripSeparator1,
            this.btnTileMode,
            this.btnPickerMode,
            this.btnEntranceMode,
            this.btnWayMode,
            this.btnWalkableMode});
            this.toolStrip.Location = new System.Drawing.Point(0, 24);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(976, 25);
            this.toolStrip.TabIndex = 4;
            this.toolStrip.Text = "toolStrip1";
            // 
            // btnNew
            // 
            this.btnNew.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnNew.Image = ((System.Drawing.Image)(resources.GetObject("btnNew.Image")));
            this.btnNew.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnNew.Name = "btnNew";
            this.btnNew.Size = new System.Drawing.Size(23, 22);
            this.btnNew.Text = "New";
            this.btnNew.Click += new System.EventHandler(this.btnNew_Click);
            // 
            // btnOpen
            // 
            this.btnOpen.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnOpen.Image = ((System.Drawing.Image)(resources.GetObject("btnOpen.Image")));
            this.btnOpen.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.Size = new System.Drawing.Size(23, 22);
            this.btnOpen.Text = "Open";
            this.btnOpen.Click += new System.EventHandler(this.btnOpen_Click);
            // 
            // btnSave
            // 
            this.btnSave.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnSave.Image = ((System.Drawing.Image)(resources.GetObject("btnSave.Image")));
            this.btnSave.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(23, 22);
            this.btnSave.Text = "Save";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // btnTileMode
            // 
            this.btnTileMode.Checked = true;
            this.btnTileMode.CheckOnClick = true;
            this.btnTileMode.CheckState = System.Windows.Forms.CheckState.Checked;
            this.btnTileMode.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnTileMode.Image = ((System.Drawing.Image)(resources.GetObject("btnTileMode.Image")));
            this.btnTileMode.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnTileMode.Name = "btnTileMode";
            this.btnTileMode.Size = new System.Drawing.Size(23, 22);
            this.btnTileMode.Text = "Tile mode";
            this.btnTileMode.Click += new System.EventHandler(this.btnTileMode_Click);
            // 
            // btnPickerMode
            // 
            this.btnPickerMode.CheckOnClick = true;
            this.btnPickerMode.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnPickerMode.Image = ((System.Drawing.Image)(resources.GetObject("btnPickerMode.Image")));
            this.btnPickerMode.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnPickerMode.Name = "btnPickerMode";
            this.btnPickerMode.Size = new System.Drawing.Size(23, 22);
            this.btnPickerMode.Text = "Tile Picker Mode";
            this.btnPickerMode.Click += new System.EventHandler(this.btnPickerMode_Click);
            // 
            // btnEntranceMode
            // 
            this.btnEntranceMode.CheckOnClick = true;
            this.btnEntranceMode.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnEntranceMode.Image = ((System.Drawing.Image)(resources.GetObject("btnEntranceMode.Image")));
            this.btnEntranceMode.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnEntranceMode.Name = "btnEntranceMode";
            this.btnEntranceMode.Size = new System.Drawing.Size(23, 22);
            this.btnEntranceMode.Text = "Entrance mode";
            this.btnEntranceMode.Click += new System.EventHandler(this.btnEntranceMode_Click);
            // 
            // btnWayMode
            // 
            this.btnWayMode.CheckOnClick = true;
            this.btnWayMode.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnWayMode.Image = ((System.Drawing.Image)(resources.GetObject("btnWayMode.Image")));
            this.btnWayMode.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnWayMode.Name = "btnWayMode";
            this.btnWayMode.Size = new System.Drawing.Size(23, 22);
            this.btnWayMode.Text = "Way mode";
            this.btnWayMode.Click += new System.EventHandler(this.btnWayMode_Click);
            // 
            // btnWalkableMode
            // 
            this.btnWalkableMode.CheckOnClick = true;
            this.btnWalkableMode.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnWalkableMode.Image = ((System.Drawing.Image)(resources.GetObject("btnWalkableMode.Image")));
            this.btnWalkableMode.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnWalkableMode.Name = "btnWalkableMode";
            this.btnWalkableMode.Size = new System.Drawing.Size(23, 22);
            this.btnWalkableMode.Text = "Walkable mode";
            this.btnWalkableMode.Click += new System.EventHandler(this.btnWalkableMode_Click);
            // 
            // MainFrm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(976, 591);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Controls.Add(this.menu);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menu;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "MainFrm";
            this.Text = "Form1";
            this.menu.ResumeLayout(false);
            this.menu.PerformLayout();
            this.editTabs.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.numDays)).EndInit();
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menu;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem newToolStripMenuItem;
        private MapWindow mapWindow;
        private System.Windows.Forms.TabControl editTabs;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.ToolStripMenuItem closeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveAsToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem2;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton btnTileMode;
        private System.Windows.Forms.ToolStripButton btnEntranceMode;
        private System.Windows.Forms.Button btnRemoveEntrance;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem resizeMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem3;
        private System.Windows.Forms.ToolStripMenuItem exportToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem4;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pngToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem burntimerawToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem selectBurntimePathToolStripMenuItem;
        private System.Windows.Forms.ToolStripButton btnWayMode;
        private System.Windows.Forms.ToolStripButton btnWalkableMode;
        private System.Windows.Forms.CheckBox btnAddEntrance;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown numDays;
        private System.Windows.Forms.CheckBox btnAddWay;
        private System.Windows.Forms.CheckBox btnAddPoint;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.ToolStripButton btnNew;
        private System.Windows.Forms.ToolStripButton btnOpen;
        private System.Windows.Forms.ToolStripButton btnSave;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showEntrances;
        private System.Windows.Forms.ToolStripMenuItem showWaysToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showWayConnectionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showWalkableToolStripMenuItem;
        private System.Windows.Forms.Button btnRemoveWay;
        private System.Windows.Forms.Button btnSaveWalkable;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ToolStripButton btnPickerMode;
        private System.Windows.Forms.ToolStripMenuItem importAsNewMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem updateTilesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem updateUpscaledTilesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem convertToMegaTileToolStripMenuItem;
    }
}

