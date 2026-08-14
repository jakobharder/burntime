using System;
using System.IO;

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
            DecodedImage decoded = ImageLoader.LoadBgra(file.Stream);
            buffer = decoded.BgraData;
            size = new Vector2(decoded.Width, decoded.Height);
            file.Close();
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
