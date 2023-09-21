using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace MapEditor
{
    public class EntranceEventArgs : EventArgs
    {
        int entrance;
        public int Entrance
        {
            get { return entrance; }
        }

        public EntranceEventArgs(int entrance)
        {
            this.entrance = entrance;
        }
    }

    public class MapClickEventArgs : EventArgs
    {
        Point position;
        public Point Position
        {
            get { return position; }
        }

        public MapClickEventArgs(Point position)
        {
            this.position = position;
        }
    }

    public partial class MapWindow : UserControl
    {
        enum ResizeCorner
        {
            None = 0,
            Top = 1,
            Left = 2,
            Bottom = 4,
            Right = 8,
            TopLeft = 3,
            TopRight = 9,
            BottomLeft = 6,
            BottomRight = 12
        }

        public enum EditMode
        {
            Tile,
            Entrance,
            Way,
            Walkable
        };

        Image mapImage;
        MapDocument doc;
        Tile tile;
        Point mouse;
        bool mouseInside = false;
        EditMode mode = EditMode.Tile;
        IMapView attachedView;

        bool allwaysShowEntrances;
        bool allwaysShowWays;
        bool allwaysShowWayConnections;
        bool allwaysShowWalkable;

        // tile editing
        bool tilePainting = false;

        // entrance editing
        ResizeCorner entranceResizeCorner = ResizeCorner.None;
        int entranceResizeIndex = -1;
        bool entranceResizing = false;
        bool entranceMoving = false;
        Point entranceMoveOffset;
        int entranceMoveIndex = -1;
        int entranceSelected = -1;

        // way editing
        int wayHoverIndex = -1;
        int waySelectIndex = -1;
        int wayPointHoverIndex = -1;
        bool wayPointMoving = false;
        Point wayPointMoveOffset;
        int wayPointMoveIndex = -1;
        int wayMoveIndex = -1;

        // walkable editing
        bool walkablePainting = false;

        // --- public attributes
        public IMapView AttachedView
        {
            get { return attachedView; }
            set { attachedView = value; }
        }

        public EditMode Mode
        {
            get { return mode; }
            set
            {
                mode = value;

                waySelectIndex = -1;
                wayHoverIndex = -1;
                entranceMoveIndex = -1;
                entranceResizeIndex = -1;
                entranceSelected = -1;

                UpdateObjects();
            }
        }

        public Image MapImage
        {
            get { return mapImage; }
        }

        // tile editing
        internal Tile Tile
        {
            get { return tile; }
            set { tile = value; Invalidate(); }
        }

        // entrance editing
        public int SelectedEntrance
        {
            get { return entranceSelected; }
        }

        public bool IsEntranceSelected
        {
            get { return -1 != entranceSelected; }
        }

        // way editing
        public bool IsWaySelected
        {
            get { return waySelectIndex != -1; }
        }

        public int SelectedWay
        {
            get { return waySelectIndex; }
        }

        public bool AllwaysShowEntrances
        {
            get { return allwaysShowEntrances; }
            set { allwaysShowEntrances = value; UpdateObjects(); }
        }

        public bool AllwaysShowWays
        {
            get { return allwaysShowWays; }
            set { allwaysShowWays = value; UpdateObjects(); }
        }

        public bool AllwaysShowWayConnections
        {
            get { return allwaysShowWayConnections; }
            set { allwaysShowWayConnections = value; UpdateObjects(); }
        }

        public bool AllwaysShowWalkable
        {
            get { return allwaysShowWalkable; }
            set { allwaysShowWalkable = value; UpdateObjects(); }
        }

        // --- public events
        public event EventHandler ClickEntrance;
        public new event EventHandler Click;
        public event EventHandler RightClick;

        // --- methods
        public MapWindow()
        {
            InitializeComponent();

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        public void SetDocument(MapDocument doc)
        {
            this.doc = doc;

            if (doc != null)
            {
                mapImage = new Bitmap(doc.Size.Width * doc.TileSize, doc.Size.Height * doc.TileSize, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                AutoScrollMinSize = new Size(doc.Size.Width * doc.TileSize, doc.Size.Height * doc.TileSize);
            }
            else
            {
                mapImage = null;
                AutoScrollMinSize = Size.Empty;
            }

            entranceSelected = -1;

            UpdateMap();
        }

        public void UpdateMap()
        {
            if (MapImage != null)
            {
                if (MapImage.Width / doc.TileSize != doc.Size.Width ||
                    MapImage.Height / doc.TileSize != doc.Size.Height)
                {
                    mapImage = new Bitmap(doc.Size.Width * doc.TileSize, doc.Size.Height * doc.TileSize, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    AutoScrollMinSize = new Size(doc.Size.Width * doc.TileSize, doc.Size.Height * doc.TileSize);
                }

                Graphics g = Graphics.FromImage(MapImage);
                g.Clear(Color.Black);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;

                for (int y = 0; y < doc.Size.Height; y++)
                {
                    for (int x = 0; x < doc.Size.Width; x++)
                    {
                        if (doc.GetTile(x, y) != null)
                        {

                            int offsetx = 0;
                            int offsety = 0;

                            if (doc.TileSize > doc.GetTile(x, y).Size.Width)
                            {
                                offsetx++;
                                offsety++;
                            }

                            g.DrawImage(doc.GetTile(x, y).Image,
                                new Rectangle(x * doc.TileSize, y * doc.TileSize, doc.TileSize + offsetx, doc.TileSize + offsety),
                                new Rectangle(new Point(0, 0), doc.GetTile(x, y).Size),
                                GraphicsUnit.Pixel);
                        }
                    }
                }
            }

            Invalidate();
        }

        public void UpdateObjects()
        {
            Invalidate();
        }

        void DrawWalkable(Graphics g)
        {
            for (int y = 0; y < doc.Size.Height; y++)
            {
                for (int x = 0; x < doc.Size.Width; x++)
                {
                    Tile tile = doc.GetTile(x, y);
                    if (tile != null)
                    {
                        Brush brush = new SolidBrush(Color.FromArgb(128, 255, 0, 0));
                        for (int _y = 0; _y < 4; _y++)
                        {
                            for (int _x = 0; _x < 4; _x++)
                            {
                                if (!tile.Mask[_x + _y * 4])
                                    continue;

                                Rectangle rc = new Rectangle();
                                rc.X = x * doc.TileSize + _x * doc.TileSize / 4;
                                rc.Y = y * doc.TileSize + _y * doc.TileSize / 4;
                                rc.Width = doc.TileSize / 4;
                                rc.Height = doc.TileSize / 4;

                                g.FillRectangle(brush, rc);
                            }
                        }
                    }
                }
            }

            if (mouseInside)
            {
                Point pos = mouse;
                pos.X = (pos.X / (doc.TileSize / 4)) * doc.TileSize / 4;
                pos.Y = (pos.Y / (doc.TileSize / 4)) * doc.TileSize / 4;

                g.DrawRectangle(new Pen(Color.Yellow, 1), new Rectangle(pos.X, pos.Y, doc.TileSize / 4, doc.TileSize / 4));
            }
        }

        void DrawWayLines(Graphics g)
        {
            for (int i = 0; i < doc.WayCount; i++)
            {
                Way way = doc.GetWay(i);
                Pen pen = new Pen(waySelectIndex == i ? Color.Yellow : Color.FromArgb(wayHoverIndex == i ? 255 : 128, 192, 192, 192), wayHoverIndex == i ? 2 : 1);

                Rectangle rc0 = doc.GetEntrance(way.Entrance[0]).Rect;
                Rectangle rc1 = doc.GetEntrance(way.Entrance[1]).Rect;

                Point start = new Point(rc0.Left + rc0.Width / 2, rc0.Top + rc0.Height / 2);
                Point end = new Point(rc1.Left + rc1.Width / 2, rc1.Top + rc1.Height / 2);

                g.DrawLine(pen, start, end);
            }
        }

        void DrawWays(Graphics g)
        {
            for (int i = 0; i < doc.WayCount; i++)
            {
                Way way = doc.GetWay(i);
                Pen pen = new Pen(waySelectIndex == i ? Color.Yellow : Color.FromArgb(wayHoverIndex == i ? 255 : 128, 192, 192, 192), wayHoverIndex == i ? 2 : 1);

                Rectangle rc0 = doc.GetEntrance(way.Entrance[0]).Rect;
                Rectangle rc1 = doc.GetEntrance(way.Entrance[1]).Rect;

                Point start = new Point(rc0.Left + rc0.Width / 2, rc0.Top + rc0.Height / 2);
                Point end = new Point(rc1.Left + rc1.Width / 2, rc1.Top + rc1.Height / 2);

                Pen waypen = new Pen(Color.Red, 4);
                for (int j = 0; j < way.Points.Count - 1; j++)
                {
                    g.DrawLine(waypen, way.Points[j], way.Points[j + 1]);
                }

                Pen ptpen;
                Pen ptsel;
                if (wayPointHoverIndex == -1 || wayHoverIndex != i)
                {
                    ptpen = new Pen(waySelectIndex == i ? Color.Blue : Color.Gray, 1);
                    ptsel = new Pen(waySelectIndex == i ? Color.Blue : Color.Gray, 1);
                }
                else
                {
                    ptsel = new Pen(Color.Yellow, 2);
                    ptpen = new Pen(Color.Blue, 1);
                }

                for (int j = 0; j < way.Points.Count; j++)
                {
                    g.DrawRectangle(j == wayPointHoverIndex ? ptsel : ptpen, way.Points[j].X - 2, way.Points[j].Y - 2, 5, 5);
                }

                StringFormat fmt = new StringFormat();
                fmt.Alignment = StringAlignment.Center;
                fmt.LineAlignment = StringAlignment.Center;

                Rectangle rect = Rectangle.Union(rc0, rc1);
                Point center = new Point();
                if (rect.Width > rect.Height)
                {
                    center.X = rect.Left + rect.Width / 2;
                    center.Y = rect.Bottom;
                }
                else
                {
                    center.X = rect.Right;
                    center.Y = rect.Top + rect.Height / 2;
                    fmt.Alignment = StringAlignment.Near;
                }
                g.DrawString("[" + (i + 1).ToString() + "] " + way.Days + "d", this.Font, new SolidBrush(Color.White), center.X, center.Y, fmt);
            }
        }

        void DrawEntrances(Graphics g)
        {
            for (int i = 0; i < doc.EntranceCount; i++)
            {
                Entrance en = doc.GetEntrance(i);
                Pen pen = new Pen((i == entranceSelected && (Mode == EditMode.Entrance || Mode == EditMode.Way)) ? Color.Yellow : Color.Blue, 1);
                Rectangle rc = en.Rect;
                //rc.Offset(-AutoScrollPosition.X, -AutoScrollPosition.Y);

                int alpha = 50;
                if (i == entranceMoveIndex)
                    alpha = 150;
                g.FillRectangle(new SolidBrush(Color.FromArgb(alpha, 0, 0, 255)), rc);
                g.DrawRectangle(pen, rc);
            }
        }

        void DrawEntranceDesc(Graphics g)
        {
            for (int i = 0; i < doc.EntranceCount; i++)
            {
                Entrance en = doc.GetEntrance(i);
                Rectangle rc = en.Rect;

                StringFormat fmt = new StringFormat();
                fmt.Alignment = StringAlignment.Far;
                fmt.LineAlignment = StringAlignment.Far;
                g.DrawString((i + 1).ToString(), this.Font, new SolidBrush(Color.White), rc.Left, rc.Top, fmt);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            //base.OnPaintBackground(e);

            if (MapImage != null)
            {
                e.Graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);

                e.Graphics.SetClip(new Rectangle(0, 0, MapImage.Width, MapImage.Height));
                Point pt = new Point(0, 0);
                e.Graphics.DrawImage(MapImage, pt);

                if (mode == EditMode.Walkable || allwaysShowWalkable)
                    DrawWalkable(e.Graphics);
                if (mode == EditMode.Entrance || mode == EditMode.Way || allwaysShowEntrances)
                    DrawEntrances(e.Graphics);
                if (mode == EditMode.Entrance || mode == EditMode.Way)
                    DrawEntranceDesc(e.Graphics);
                if (mode == EditMode.Way || allwaysShowWayConnections)
                    DrawWayLines(e.Graphics);
                if (mode == EditMode.Way || allwaysShowWays)
                    DrawWays(e.Graphics);

                if (Tile != null && doc != null && mouseInside)
                {
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    ImageAttributes attrib = new ImageAttributes();
                    ColorMatrix mat = new ColorMatrix();
                    mat.Matrix00 = 1.0f;
                    mat.Matrix11 = 1.0f;
                    mat.Matrix22 = 1.0f;
                    mat.Matrix33 = 0.6f;
                    attrib.SetColorMatrix(mat);

                    Point pos = mouse;
                    pos.X = (pos.X / doc.TileSize) * doc.TileSize;
                    pos.Y = (pos.Y / doc.TileSize) * doc.TileSize;

                    Rectangle rc = new Rectangle(pos.X, pos.Y, doc.TileSize, doc.TileSize);

                    e.Graphics.DrawImage(Tile.Image,
                        rc,
                        0, 0, Tile.Size.Width, Tile.Size.Height,
                        GraphicsUnit.Pixel,
                        attrib);

                    e.Graphics.DrawRectangle(new Pen(Color.Yellow, 1), rc);
                }

                e.Graphics.ResetClip();
                e.Graphics.ExcludeClip(new Rectangle(0, 0, MapImage.Width, MapImage.Height));
            }

            base.OnPaintBackground(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            mouse.X = (e.Location.X - AutoScrollPosition.X);
            mouse.Y = (e.Location.Y - AutoScrollPosition.Y);
            base.OnMouseMove(e);

            if (doc == null)
                return;

            if (Mode != EditMode.Entrance)
            {
                entranceResizeIndex = -1;
                entranceMoveIndex = -1;
            }

            if (Mode != EditMode.Way)
            {
                waySelectIndex = -1;
                wayPointMoveIndex = -1;
                wayPointHoverIndex = -1;
            }

            switch (mode)
            {
                case EditMode.Tile:
                    if (e.Button == MouseButtons.Left && Tile != null && tilePainting)
                    {
                        Point pt = e.Location;
                        pt.X = (pt.X - AutoScrollPosition.X) / doc.TileSize;
                        pt.Y = (pt.Y - AutoScrollPosition.Y) / doc.TileSize;

                        if (pt.X >= 0 && pt.Y >= 0 && pt.X < doc.Size.Width && pt.Y < doc.Size.Height)
                        {
                            if (doc.GetTile(pt.X, pt.Y) != Tile)
                                doc.SetTile(pt.X, pt.Y, Tile);
                        }
                    }
                    break;

                case EditMode.Entrance:
                    if (!entranceMoving && !entranceResizing)
                    {
                        Cursor cursor = Cursors.Arrow;

                        entranceResizeCorner = ResizeCorner.None;
                        entranceResizeIndex = -1;
                        entranceMoveIndex = -1;

                        for (int i = 0; i < doc.EntranceCount && entranceResizeIndex == -1; i++)
                        {
                            Entrance en = doc.GetEntrance(i);

                            Rectangle rc = en.Rect;
                            rc.Inflate(2, 2);

                            if (rc.Contains(mouse))
                            {
                                rc.Inflate(-4, -4);
                                if (!rc.Contains(mouse))
                                {
                                    entranceResizeCorner |= (mouse.X <= rc.Left) ? ResizeCorner.Left : ResizeCorner.None;
                                    entranceResizeCorner |= (mouse.X >= rc.Right) ? ResizeCorner.Right : ResizeCorner.None;
                                    entranceResizeCorner |= (mouse.Y <= rc.Top) ? ResizeCorner.Top : ResizeCorner.None;
                                    entranceResizeCorner |= (mouse.Y >= rc.Bottom) ? ResizeCorner.Bottom : ResizeCorner.None;

                                    entranceResizeIndex = i;

                                    switch (entranceResizeCorner)
                                    {
                                        case ResizeCorner.Left:
                                        case ResizeCorner.Right:
                                            cursor = Cursors.SizeWE;
                                            break;
                                        case ResizeCorner.Top:
                                        case ResizeCorner.Bottom:
                                            cursor = Cursors.SizeNS;
                                            break;
                                        case ResizeCorner.TopRight:
                                        case ResizeCorner.BottomLeft:
                                            cursor = Cursors.SizeNESW;
                                            break;
                                        case ResizeCorner.TopLeft:
                                        case ResizeCorner.BottomRight:
                                            cursor = Cursors.SizeNWSE;
                                            break;

                                        default:
                                            entranceResizeIndex = -1;
                                            entranceResizeCorner = ResizeCorner.None;
                                            break;
                                    }
                                }
                                else
                                {
                                    cursor = Cursors.SizeAll;
                                    entranceMoveIndex = i;
                                }
                            }
                        }

                        Cursor = cursor;
                    }
                    if (entranceResizing)
                    {
                        Entrance en = doc.GetEntrance(entranceResizeIndex);

                        if ((entranceResizeCorner & ResizeCorner.Left) != ResizeCorner.None)
                        {
                            int change = (en.Rect.X - mouse.X) + en.Rect.Width;
                            if (change < 6)
                                change = 6;
                            en.Rect.X = en.Rect.Right - change;
                            en.Rect.Width = change;
                        }
                        if ((entranceResizeCorner & ResizeCorner.Right) != ResizeCorner.None)
                        {
                            en.Rect.Width = (mouse.X - en.Rect.X);
                            if (en.Rect.Width < 6)
                                en.Rect.Width = 6;
                        }
                        if ((entranceResizeCorner & ResizeCorner.Top) != ResizeCorner.None)
                        {
                            int change = (en.Rect.Y - mouse.Y) + en.Rect.Height;
                            if (change < 6)
                                change = 6;
                            en.Rect.Y = en.Rect.Bottom - change;
                            en.Rect.Height = change;
                        }
                        if ((entranceResizeCorner & ResizeCorner.Bottom) != ResizeCorner.None)
                        {
                            en.Rect.Height = (mouse.Y - en.Rect.Y);
                            if (en.Rect.Height < 6)
                                en.Rect.Height = 6;
                        }

                        doc.SetEntrance(entranceResizeIndex, en);
                    }

                    if (entranceMoving)
                    {
                        Entrance en = doc.GetEntrance(entranceMoveIndex);

                        if (en.Rect.X != mouse.X + entranceMoveOffset.X &&
                            en.Rect.Y != mouse.Y + entranceMoveOffset.Y)
                        {
                            en.Rect.X = mouse.X + entranceMoveOffset.X;
                            en.Rect.Y = mouse.Y + entranceMoveOffset.Y;

                            doc.SetEntrance(entranceMoveIndex, en);
                        }
                    }
                    break;

                case EditMode.Way:
                    entranceMoveIndex = -1;

                    for (int i = 0; i < doc.EntranceCount; i++)
                    {
                        Entrance en = doc.GetEntrance(i);

                        Rectangle rc = en.Rect;
                        rc.Inflate(2, 2);

                        if (rc.Contains(mouse))
                            entranceMoveIndex = i;
                    }

                    wayHoverIndex = -1;
                    if (entranceMoveIndex == -1 && entranceResizeIndex == -1)
                    {
                        for (int j = 0; j < doc.WayCount; j++)
                        {
                            Way way = doc.GetWay(j);

                            Rectangle rc0 = doc.GetEntrance(way.Entrance[0]).Rect;
                            Rectangle rc1 = doc.GetEntrance(way.Entrance[1]).Rect;

                            Rectangle area = Rectangle.Union(rc0, rc1);
                            if (area.Contains(mouse))
                            {
                                PointF start = new PointF(rc0.Left + rc0.Width / 2, rc0.Top + rc0.Height / 2);
                                PointF end = new PointF(rc1.Left + rc1.Width / 2, rc1.Top + rc1.Height / 2);
                                PointF dir = new PointF(end.X - start.X, end.Y - start.Y);
                                float len = (float)Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
                                dir.X /= len;
                                dir.Y /= len;

                                float lambda = (mouse.X - start.X) * dir.X + (mouse.Y - start.Y) * dir.Y;
                                PointF d = new PointF();
                                d.X = start.X + lambda * dir.X;
                                d.Y = start.Y + lambda * dir.Y;

                                float dist = (float)Math.Sqrt((d.X - mouse.X) * (d.X - mouse.X) + (d.Y - mouse.Y) * (d.Y - mouse.Y));
                                if (dist < 8)
                                    wayHoverIndex = j;
                            }
                        }
                    }

                    wayPointHoverIndex = -1;
                    if (entranceMoveIndex == -1 && entranceResizeIndex == -1)
                    {
                        for (int j = 0; j < doc.WayCount; j++)
                        {
                            Way way = doc.GetWay(j);

                            for (int k = 0; k < doc.Ways[j].Points.Count; k++)
                            {
                                Rectangle rect = new Rectangle(way.Points[k].X - 2, way.Points[k].Y - 2, 5, 5);
                                rect.Inflate(3, 3);
                                if (rect.Contains(mouse))
                                {
                                    wayPointHoverIndex = k;
                                    wayHoverIndex = j;
                                }
                            }
                        }
                    }

                    if (wayPointMoving)
                    {
                        Point pt = doc.Ways[wayMoveIndex].Points[wayPointMoveIndex];
                        if (pt.X != mouse.X + wayPointMoveOffset.X &&
                            pt.Y != mouse.Y + wayPointMoveOffset.Y)
                        {
                            pt.X = mouse.X + wayPointMoveOffset.X;
                            pt.Y = mouse.Y + wayPointMoveOffset.Y;
                            doc.Ways[wayMoveIndex].Points[wayPointMoveIndex] = pt;
                            doc.Saved = false;
                        }
                    }
                    break;

                case EditMode.Walkable:
                    if (walkablePainting)
                    {
                        Point pt = e.Location;
                        pt.X = (pt.X - AutoScrollPosition.X) / doc.TileSize;
                        pt.Y = (pt.Y - AutoScrollPosition.Y) / doc.TileSize;

                        if (pt.X >= 0 && pt.Y >= 0 && pt.X < doc.Size.Width && pt.Y < doc.Size.Height)
                        {
                            Tile tile = doc.GetTile(pt.X, pt.Y);
                            if (tile != null)
                            {
                                Point sub = e.Location;
                                sub.X = (sub.X - AutoScrollPosition.X) / (doc.TileSize / 4);
                                sub.Y = (sub.Y - AutoScrollPosition.Y) / (doc.TileSize / 4);

                                sub.X -= pt.X * 4;
                                sub.Y -= pt.Y * 4;

                                tile.Mask[sub.X + sub.Y * 4] = (e.Button == MouseButtons.Left);
                                TileManager.Change(tile);
                            }
                        }
                    }
                    break;
            }

            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {

            base.OnMouseClick(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (doc == null)
                return;

            switch (mode)
            {
                case EditMode.Tile:
                    if (e.Button == MouseButtons.Left)
                    {
                        if (doc != null && Mode == EditMode.Tile && Tile != null)
                        {
                            Point pt = e.Location;
                            pt.X = (pt.X - AutoScrollPosition.X) / doc.TileSize;
                            pt.Y = (pt.Y - AutoScrollPosition.Y) / doc.TileSize;

                            if (pt.X >= 0 && pt.Y >= 0 && pt.X < doc.Size.Width && pt.Y < doc.Size.Height)
                            {
                                doc.SetTile(pt.X, pt.Y, Tile);
                            }
                        }

                        tilePainting = true;
                    }
                    break;

                case EditMode.Entrance:
                    if (e.Button == MouseButtons.Right)
                    {
                        entranceSelected = -1;
                    }
                    else if (e.Button == MouseButtons.Left)
                    {
                        if (entranceResizeIndex != -1)
                        {
                            entranceResizing = true;
                            entranceSelected = entranceResizeIndex;
                            ClickEntrance.Invoke(this, new EntranceEventArgs(entranceSelected));

                            UpdateObjects();
                        }
                        if (entranceMoveIndex != -1)
                        {
                            Entrance en = doc.GetEntrance(entranceMoveIndex);

                            entranceMoving = true;
                            entranceMoveOffset.X = en.Rect.X - mouse.X;
                            entranceMoveOffset.Y = en.Rect.Y - mouse.Y;
                            entranceSelected = entranceMoveIndex;
                            ClickEntrance.Invoke(this, new EntranceEventArgs(entranceSelected));

                            UpdateObjects();
                        }
                    }
                    break;

                case EditMode.Way:
                    if (e.Button == MouseButtons.Right)
                    {
                        waySelectIndex = -1;
                    }
                    else if (e.Button == MouseButtons.Left)
                    {
                        if (entranceMoveIndex != -1)
                        {
                            waySelectIndex = -1;
                            Entrance en = doc.GetEntrance(entranceMoveIndex);

                            entranceMoving = true;
                            entranceMoveOffset.X = en.Rect.X - mouse.X;
                            entranceMoveOffset.Y = en.Rect.Y - mouse.Y;
                            entranceSelected = entranceMoveIndex;
                            ClickEntrance.Invoke(this, new EntranceEventArgs(entranceSelected));

                            UpdateObjects();
                        }
                        if (wayHoverIndex != -1)
                        {
                            entranceSelected = -1;

                            waySelectIndex = wayHoverIndex;

                            UpdateObjects();
                        }
                        if (wayPointHoverIndex != -1)
                        {
                            wayPointMoving = true;
                            wayPointMoveOffset.X = doc.Ways[wayHoverIndex].Points[wayPointHoverIndex].X - mouse.X;
                            wayPointMoveOffset.Y = doc.Ways[wayHoverIndex].Points[wayPointHoverIndex].Y - mouse.Y;
                            wayPointMoveIndex = wayPointHoverIndex;
                            wayMoveIndex = wayHoverIndex;
                        }
                    }
                    break;

                case EditMode.Walkable:
                    {
                        Point pt = e.Location;
                        pt.X = (pt.X - AutoScrollPosition.X) / doc.TileSize;
                        pt.Y = (pt.Y - AutoScrollPosition.Y) / doc.TileSize;

                        if (pt.X >= 0 && pt.Y >= 0 && pt.X < doc.Size.Width && pt.Y < doc.Size.Height)
                        {
                            Tile tile = doc.GetTile(pt.X, pt.Y);
                            if (tile != null)
                            {
                                Point sub = e.Location;
                                sub.X = (sub.X - AutoScrollPosition.X) / (doc.TileSize / 4);
                                sub.Y = (sub.Y - AutoScrollPosition.Y) / (doc.TileSize / 4);

                                sub.X -= pt.X * 4;
                                sub.Y -= pt.Y * 4;

                                tile.Mask[sub.X + sub.Y * 4] = (e.Button == MouseButtons.Left);
                                TileManager.Change(tile);
                            }
                        }

                        walkablePainting = true;
                    }
                    break;
            }
            
            if (e.Button == MouseButtons.Left && wayPointHoverIndex == -1)
            {
                Click.Invoke(this, new MapClickEventArgs(mouse));
            }
            else if (e.Button == MouseButtons.Right)
            {
                RightClick.Invoke(this, new MapClickEventArgs(mouse));
            }

            if (attachedView != null)
                attachedView.UpdateTitle();
            
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            entranceResizing = false;
            entranceMoving = false;
            tilePainting = false;
            wayPointMoving = false;
            walkablePainting = false;
            base.OnMouseUp(e);
        }

        public void RemoveSelectedEntrance()
        {
            if (entranceSelected != -1)
            {
                doc.RemoveEntrance(entranceSelected);
                entranceSelected = -1;
                if (attachedView != null)
                    attachedView.UpdateTitle();
            }
        }

        public void RemoveSelectedWay()
        {
            if (waySelectIndex != -1)
            {
                doc.RemoveWay(waySelectIndex);
                waySelectIndex = -1;
                if (attachedView != null)
                    attachedView.UpdateTitle();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            mouseInside = true;
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            mouseInside = false;
            base.OnMouseLeave(e);
        }
    }
}
