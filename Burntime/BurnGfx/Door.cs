using System;

using Burntime.Platform;
using Burntime.Platform.IO;

namespace Burntime.Data.BurnGfx
{
    public class Door
    {
        protected Rect rc;
        public Rect Area
        {
            get
            {
                return rc;
            }
            set
            {
                rc = value;
            }
        }

        protected int id;
        public int ID
        {
            get
            {
                return id;
            }
            set
            {
                id = value;
            }
        }

        protected String info;
        public String Info
        {
            get
            {
                return info;
            }
        }

        public int RoomID;
        byte[] infob;

        static public Door Read(File reader)
        {
            int id = reader.ReadUShort();
            int x = reader.ReadUShort();
            int y = reader.ReadUShort();
            int w = reader.ReadUShort();
            int h = reader.ReadUShort();

            byte[] infob = new byte[10];
            reader.Read(infob, 0, 10);
            String info = "";
            foreach (byte b in infob)
                info += b.ToString("X2");

            if (x != 0)
            {
                Door d = new Door();
                d.id = id;
                d.rc = new Rect(x, y, w, h);
                d.info = info;
                d.infob = infob;

                d.RoomID = infob[2];

                return d;
            }

            return null;
        }

        public void Write(File writer)
        {
            writer.WriteUShort((ushort)id);
            writer.WriteUShort((ushort)rc.Left);
            writer.WriteUShort((ushort)rc.Top);
            writer.WriteUShort((ushort)rc.Width);
            writer.WriteUShort((ushort)rc.Height);
            writer.Write(infob, 10);
        }
    }
}
