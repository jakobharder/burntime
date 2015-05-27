using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Platform.Graphics;
using Burntime.Platform.Resource;

namespace Burntime.Data.BurnGfx
{
    public class SpriteLoaderAni : ISpriteProcessor, ISpriteAnimationProcessor, IDataProcessor
    {
        Vector2 size;
        RawImageFileReader raw;
        int frameCount;
        int frameOffset;
        bool progressive;
        int frame;

        System.IO.MemoryStream lastFrame;

        public Vector2 Size
        {
            get { return size; }
        }

        public int FrameCount
        {
            get { return frameCount; }
        }

        public Vector2 FrameSize
        {
            get { return size; }
        }

        public bool SetFrame(int frame)
        {
            this.frame = frame;
            if (!raw.ReadImage(frame + frameOffset))
                return false;
            
            if (raw.Size > size || frame == 0 || !progressive)
                size = raw.Size;
            return true;
        }

        public void Process(ResourceID ID)
        {
            if (ID.EndIndex == -1)
                frameCount = 1;
            else
                frameCount = ID.EndIndex - ID.Index + 1;

            frameOffset = ID.Index;
            progressive = ID.Custom == "p";

            File file = FileSystem.GetFile(ID.File);
            raw = new RawImageFileReader(file);
            raw.ReadHeader();

            if (!ID.IndexProvided)
                frameCount = raw.ImageCount;

            size = new Vector2();

            lastFrame = null;
        }

        public DataObject Process(ResourceID ID, ResourceManager ResourceManager)
        {
            return ResourceManager.GetImage(ID);
        }

        public void Render(IntPtr ptr)
        {
            throw new NotSupportedException();
        }

        public void Render(System.IO.Stream s, int stride)
        {
            if (progressive && lastFrame != null)
            {
                lastFrame.Seek(0, System.IO.SeekOrigin.Begin);
                Vector2 sz = raw.Size;

                byte[] buf = new byte[sz.x * 4];

                System.IO.MemoryStream currentFrame = new System.IO.MemoryStream();

                for (int y = 0; y < size.y; y++)
                {
                    lastFrame.Read(buf, 0, size.x * 4);

                    if (y < sz.y)
                    {
                        for (int x = 0; x < sz.x && x < size.x; x++)
                        {
                            if (raw.Data[y * size.x * 4 + x * 4 + 3] != 0)
                            {
                                for (int i = 0; i < 4; i++)
                                    buf[x * 4 + i] = raw.Data[y * size.x * 4 + x * 4 + i];
                            }
                        }
                    }

                    s.Write(buf, 0, size.x * 4);
                    currentFrame.Write(buf, 0, size.x * 4);
                    s.Seek(stride - size.x * 4, System.IO.SeekOrigin.Current);
                }

                lastFrame = currentFrame;
            }
            else
            {
                for (int y = 0; y < size.y; y++)
                {
                    s.Write(raw.Data, y * Size.x * 4, Size.x * 4);
                    s.Seek(stride - Size.x * 4, System.IO.SeekOrigin.Current);
                }

                if (progressive)
                {
                    lastFrame = new System.IO.MemoryStream();
                    for (int y = 0; y < size.y; y++)
                        lastFrame.Write(raw.Data, y * Size.x * 4, Size.x * 4);
                }
            }
        }

        string[] IDataProcessor.Names
        {
            get { return new string[] { "ani", "burngfxani" }; }
        }
    }
}
