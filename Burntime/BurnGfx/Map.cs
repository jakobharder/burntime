using System;
using System.Collections.Generic;

using Burntime.Platform;
using Burntime.Platform.IO;

namespace Burntime.Data.BurnGfx
{
    public struct Mask
    {
        public int Value;
        public int x;
        public int y;
    };

    public class Map
    {
        protected int DoorInfoAddress;
        protected byte[] UnknownBlock1; // filesize, filesize ??
        protected byte[] UnknownBlock1b; // zeros
        protected byte[] UnknownBlock2; // 33% color map?
        //protected String UnknownBlock3;

        protected List<Door> doors;
        protected  List<Mask> masks;
        protected ColorTable colors;

        public List<Door> Doors { get { return doors; } }
        public ColorTable ColorTable { get { return colors; } }

        public int TileWidth
        {
            get
            {
                return Tile.WIDTH;
            }
        }

        public int TileHeight
        {
            get
            {
                return Tile.HEIGHT;
            }
        }

        public int width;
        public int height;

        public Vector2 Size
        {
            get
            {
                return new Vector2(width * Tile.WIDTH, height * Tile.HEIGHT);
            }
        }

        public int MapId;

        public ushort[] data;

        public List<int> TileSetList = new List<int>();

        public Map()
        {
        }

        public Map(String file)
        {
            Load(FileSystem.GetFile(file));
        }

        public Map(File file)
        {
            Load(file);
        }

        void Load(File file)
        {
            //if (System.IO.File.Exists(BurnGfxSetting.Singleton.DataPath + "\\" + File))
            {
                File reader = file;//FileSystem.GetFile("burngfx:" + File);

                width = reader.ReadUShort() / 2;
                height = reader.ReadUShort();

                UnknownBlock1 = reader.ReadBytes(4);
                DoorInfoAddress = reader.ReadUShort();
                UnknownBlock1b = reader.ReadBytes(6);

                // skip color table
                reader.Seek(256 * 3, SeekPosition.Current);

                UnknownBlock2 = reader.ReadBytes(256);

                data = new ushort[width * height];

                masks = new List<Mask>();

                for (int i = 0; i < width * height; i++)
                {
                    data[i] = reader.ReadUShort();

                    int x = i % width;
                    int y = (i - x) / width;

                    if (!TileSetList.Contains(data[i] & 0xff))
                        TileSetList.Add(data[i] & 0xff);
                    //Tile tile = TileDB.Singleton.GetTile(data[i] & 0xff, data[i] >> 8);

                    //if (tile != null && tile.Mask != 0)
                    //{
                    //    Mask m = new Mask();
                    //    m.x = x * tile.Width;
                    //    m.y = y * tile.Height;
                    //    m.Value = tile.Mask;
                    //    masks.Add(m);
                    //}
                }

                doors = new List<Door>();

                Door d = Door.Read(reader);
                while (d != null)
                {
                    doors.Add(d);
                    d = Door.Read(reader);
                }
            }
            //else
            //{
            //    data = new ushort[1];
            //    width = 1;
            //    height = 1;
            //    data[0] = 22;
            //    colors = ColorTable.Last;
            //    masks = new List<Mask>();
            //    doors = new List<Door>();
            //}
        }

        public void SaveToRaw(File writer)
        {
            writer.WriteUShort((ushort)(width * 2));
            writer.WriteUShort((ushort)height);

            writer.Write(UnknownBlock1, 4);
            int DoorInfoPos = writer.Position;
            writer.WriteUShort(0);
            writer.Write(UnknownBlock1b, 6);

            // skip color table
            writer.Seek(256 * 3, SeekPosition.Current);

            writer.Write(UnknownBlock2, 256);

            for (int i = 0; i < width * height; i++)
            {
                writer.WriteUShort(data[i]);

                int x = i % width;
                int y = (i - x) / width;

                if (!TileSetList.Contains(data[i] & 0xff))
                    TileSetList.Add(data[i] & 0xff);
                //Tile tile = TileDB.Singleton.GetTile(data[i] & 0xff, data[i] >> 8);

                //if (tile != null && tile.Mask != 0)
                //{
                //    Mask m = new Mask();
                //    m.x = x * tile.Width;
                //    m.y = y * tile.Height;
                //    m.Value = tile.Mask;
                //    masks.Add(m);
                //}
            }

            DoorInfoAddress = writer.Position;
            writer.Seek(DoorInfoPos, SeekPosition.Begin);
            writer.WriteUShort((ushort)DoorInfoAddress);
            writer.Seek(DoorInfoAddress, SeekPosition.Begin);

            for (int i = 0; i < doors.Count; i++)
            {
                doors[i].Write(writer);
            }
        }

        public bool CheckTile(int x, int y)
        {
            int tx = (x - x % 4) / 4;
            int ty = (y - y % 4) / 4;

            if (x == 0 || y == 0 || (tx == width - 1 && x % 4 == 3) || (ty == height - 1 && y % 4 == 3))
                return true;

            foreach (Mask m in masks)
            {
                if (m.x == tx * 32 && m.y == ty * 32)
                {
                    bool[] matrix = new bool[16];

                    int value = m.Value;
                    for (int i = 0; i < 16; i++, value >>= 1)
                        matrix[15 - i] = (value & 1) != 0;

                    int n = (y % 4) * 4 + x % 4;
                    return matrix[n];
                }
            }

            return false;
        }

        public void Read(File reader)
        {
            int width = reader.ReadUShort() / 2;
            int height = reader.ReadUShort();

            UnknownBlock1 = reader.ReadBytes(4);
            DoorInfoAddress = reader.ReadUShort();
            UnknownBlock1b = reader.ReadBytes(6);

            colors = new ColorTable();
            colors.Read(reader);

            UnknownBlock2 = reader.ReadBytes(256);

            //bmp = new Bitmap(width * Tile.WIDTH, height * Tile.HEIGHT, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            //Graphics g = Graphics.FromImage(bmp);
            //g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

            masks = new List<Mask>();

            byte[] data = new byte[2];
            for (int i = 0; i < width * height; i++)
            {
                int read = reader.Read(data, 0, 2);

                if (read != 2 || data[0] > 50)
                    break;

                int x = i % width;
                int y = (i - i % width) / width;

                //FileStream file = File.OpenRead(BurnGfxSetting.Singleton.DataPath + "\\ZEI_0" + data[0].ToString("D2") + ".RAW");
                //file.Seek(0x410 * data[1], SeekOrigin.Begin);

                //Tile tile = new Tile();
                //tile.Read(file, colors);

                Tile tile = null;// TileDB.Singleton.GetTile(data[0], data[1]);

                if (tile.Mask != 0)
                {
                    Mask m = new Mask();
                    m.x = x * tile.Width;
                    m.y = y * tile.Height;
                    m.Value = tile.Mask;
                    masks.Add(m);
                }
            }

            doors = new List<Door>();

            Door d = Door.Read(reader);
            while (d != null)
            {
                doors.Add(d);
                d = Door.Read(reader);
            }

        }

        public void Convert()
        {
            //bmp.Save("c:\\burntime\\input.bmp", System.Drawing.Imaging.ImageFormat.Bmp);

            //Size sz = bmp.Size;

            //bmp = null;

            //System.Diagnostics.Process proc = new System.Diagnostics.Process();
            //proc.EnableRaisingEvents = false;
            //proc.StartInfo.FileName = "c:\\burntime\\hq3x.exe";
            //proc.StartInfo.Arguments = "c:\\burntime\\input.bmp c:\\burntime\\output.bmp";
            //proc.Start();
            //proc.WaitForExit();

            //Image img = Image.FromFile("c:\\burntime\\output.bmp");

            //int zoom = 3;
            //bmp = new Bitmap(zoom * sz.Width, zoom * sz.Height);
            //Graphics g = Graphics.FromImage(bmp);

            //g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            //g.DrawImage(img, new Rectangle(0, 0, zoom * sz.Width, zoom * sz.Height), new Rectangle(0, 0, sz.Width * 3, sz.Height * 3), GraphicsUnit.Pixel);

            //img.Dispose();
        }

        public void DrawGrid()
        {
            //Graphics g = Graphics.FromImage(bmp);

            //g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

            //Pen p = new Pen(new SolidBrush(Color.Black));

            //for (int i = 0; i < Size.Width; i++)
            //{
            //    Point pt1 = new Point(i * TileWidth, 0);
            //    Point pt2 = new Point(pt1.X, Size.Height * TileHeight - 1);
            //    g.DrawLine(p, pt1, pt2);
            //}

            //for (int i = 0; i < Size.Height; i++)
            //{
            //    Point pt1 = new Point(0, i * TileHeight);
            //    Point pt2 = new Point(Size.Width * TileWidth - 1, pt1.Y);
            //    g.DrawLine(p, pt1, pt2);
            //}

            //p = new Pen(new SolidBrush(Color.Red));
            //foreach (Mask m in masks)
            //{
            //    bool[] matrix = new bool[16];

            //    int value = m.Value;
            //    for (int i = 0; i < 16; i++, value >>= 1)
            //        matrix[15 - i] = (value & 1) != 0;


            //    for (int i = 0; i < 16; i++)
            //    {
            //        if (matrix[i])
            //        {
            //            int x = i % 4;
            //            int y = (i - x) / 4;

            //            Rectangle rc = new Rectangle(m.x + x * TileWidth / 4, m.y + y * TileHeight / 4, TileWidth / 4, TileHeight / 4);

            //            // left
            //            if (x == 0 || (x > 0 && !matrix[y * 4 + x - 1]))
            //            {
            //                g.DrawLine(p, new Point(rc.Left, rc.Top), new Point(rc.Left, rc.Bottom));
            //            }
            //            // right
            //            if (x == 3 || (x < 3 && !matrix[y * 4 + x + 1]))
            //            {
            //                g.DrawLine(p, new Point(rc.Right, rc.Top), new Point(rc.Right, rc.Bottom));
            //            }
            //            // top
            //            if (y == 0 || (y > 0 && !matrix[(y - 1) * 4 + x]))
            //            {
            //                g.DrawLine(p, new Point(rc.Left, rc.Top), new Point(rc.Right, rc.Top));
            //            }
            //            // bottom
            //            if (y == 3 || (y < 3 && !matrix[(y + 1) * 4 + x]))
            //            {
            //                g.DrawLine(p, new Point(rc.Left, rc.Bottom), new Point(rc.Right, rc.Bottom));
            //            }

            //            //g.DrawRectangle(p, rc);
            //        }
            //    }
            //}
            //p = new Pen(new SolidBrush(Color.Blue));

            //foreach (Door d in doors)
            //{
            //    g.DrawRectangle(p, d.Area);
            //}
        }

        public int GetDoor(Vector2 pos)
        {
            Vector2 pt = new Vector2(pos);
            foreach (Door d in doors)
            {
                if (d.Area.PointInside(pt))
                {
                    return d.ID;
                }
            }

            return 0;
        }

        public Door GetDoor(int id)
        {
            if (doors[id - 1].ID != id)
                return null;
            return doors[id - 1];
        }

        public void save(String file)
        {
            //Bitmap bmp = new Bitmap(PixelSize.Width, PixelSize.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);


            //System.Drawing.Imaging.BitmapData bdata = bmp.LockBits(new Rectangle(0, 0, PixelSize.Width, PixelSize.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly,
            //    System.Drawing.Imaging.PixelFormat.Format24bppRgb);


            //ByteBuffer target = new ByteBuffer(PixelSize.Width, PixelSize.Height, new byte[3 * PixelSize.Width * PixelSize.Height]);


            //for (int y = 0; y < height; y++)
            //{
            //    for (int x = 0; x < width; x++)
            //    {
            //        int tileset = data[x + y * width] & 0xff;
            //        int tileid = data[x + y * width] >> 8;

            //        Tile tile = TileDB.Singleton.GetTile(tileset, tileid);
            //        tile.Draw(ref target, x * Tile.WIDTH, y * Tile.HEIGHT, ColorTable.Last);

            //        ByteBuffer tt = new ByteBuffer(Tile.WIDTH, Tile.HEIGHT, new byte[3 * Tile.WIDTH * Tile.HEIGHT]);
            //        tile.Draw(ref tt, 0, 0, ColorTable.Last);
            //        Bitmap tbmp = new Bitmap(Tile.WIDTH, Tile.HEIGHT, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            //        System.Drawing.Imaging.BitmapData bdata2 = tbmp.LockBits(new Rectangle(0, 0, Tile.WIDTH, Tile.HEIGHT), System.Drawing.Imaging.ImageLockMode.ReadOnly,
            //            System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            //        System.Runtime.InteropServices.Marshal.Copy(tt.Data, 0, bdata2.Scan0, tt.Height * tt.Stride);
            //        tbmp.UnlockBits(bdata2);

            //        tbmp.Save(BurnGfxSetting.Singleton.DataPath + "\\..\\output\\tiles\\" + tileset.ToString("D2") + "_" + tileid.ToString("D2") + ".png");
            //    }
            //}

            //System.Runtime.InteropServices.Marshal.Copy(target.Data, 0, bdata.Scan0, target.Height * target.Stride);
            //bmp.UnlockBits(bdata);

            //bmp.Save(BurnGfxSetting.Singleton.DataPath + "\\..\\output\\maps\\" + file + ".png");
        }
    }
}
