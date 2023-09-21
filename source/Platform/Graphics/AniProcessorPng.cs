using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;

namespace Burntime.Platform.Graphics
{
    public class AniProcessorPng : ISpriteProcessor, ISpriteAnimationProcessor, IDataProcessor
    {
        Vector2 size;
        int frameOffset;
        int frameCount;
        string format;
        SpriteProcessorPng png;

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
            png = new SpriteProcessorPng();
            png.Process(String.Format(format, frame + frameOffset));
            size = png.Size;
            return true;
        }

        public void Process(ResourceID ID)
        {
            if (ID.EndIndex == -1)
                frameCount = 1;
            else
                frameCount = ID.EndIndex - ID.Index + 1;

            frameOffset = ID.Index;
            format = ID.File;
            size = new Vector2();
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
            png.Render(s, stride);
        }

        string[] IDataProcessor.Names
        {
            get { return new string[] { "pngani" }; }
        }
    }
}
