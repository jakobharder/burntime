using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using Burntime.Platform.IO;
using Burntime.Platform.Resource;

namespace Burntime.Platform.Graphics
{
    public class SpriteProcessorPng : ISpriteProcessor
    {
        Vector2 size;
        byte[] buffer;

        public Vector2 Size { get { return size; } }
        public byte[] Buffer { get { return buffer; } }

        public void Process(ResourceID id)
        {
            Burntime.Platform.IO.File file = FileSystem.GetFile(string.Format(id.File, id.Index));
            Bitmap bmp = new Bitmap(Bitmap.FromStream(file));

            buffer = new byte[bmp.Width * bmp.Height * 4];

            BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            unsafe
            {
                for (int y = 0; y < bmp.Height; y++)
                    Marshal.Copy((IntPtr)((byte*)data.Scan0 + y * data.Stride), buffer, y * bmp.Width * 4, bmp.Width * 4);
            }

            bmp.UnlockBits(data);

            size = new Vector2(bmp.Width, bmp.Height);
        }

        public void Render(Stream s, int stride)
        {
            for (int y = 0; y < size.y; y++)
            {
                s.Write(buffer, y * size.x * 4, size.x * 4);
                s.Seek(stride - size.x * 4, SeekOrigin.Current);
            }
        }
    }
}
