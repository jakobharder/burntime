
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
