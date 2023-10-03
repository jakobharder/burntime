using System;
using System.Runtime.InteropServices;
using System.IO;

using Burntime.Platform;
using Burntime.Platform.Resource;

namespace Burntime.Data.BurnGfx
{
    public class SpriteLoaderRaw : ISpriteProcessor, IDataProcessor
    {
        Vector2 size;
        ItemImage image;

        public Vector2 Size
        {
            get { return size; }
        }

        public void Process(ResourceID ID)
        {
            image = new ItemImage(ID.File, ID.Index);
            image.Load();
            size = new Vector2(image.Width, image.Height);
        }

        public DataObject Process(ResourceID ID, IResourceManager ResourceManager)
        {
            return ResourceManager.GetImage(ID);
        }

        public void Render(IntPtr ptr)
        {
            int len = size.x * size.y;
            unsafe
            {
                int* data = (int*)ptr;
                for (int i = 0; i < len; i++)
                    data[i] = image.Data[i].ToInt();
            }
        }

        public void Render(Stream s, int stride)
        {
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                    s.Write(System.BitConverter.GetBytes(image.Data[x + y * size.x].ToInt()), 0, 4);

                s.Seek(stride - size.x * 4, SeekOrigin.Current);
            }
        }

        string[] IDataProcessor.Names
        {
            get { return new string[] { "raw" }; }
        }
    }
}
