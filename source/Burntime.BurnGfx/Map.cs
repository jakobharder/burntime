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

        [Obsolete]
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
    }
}
