using System;

using SlimDX.Direct3D9;
using Burntime.Platform.Resource;

namespace Burntime.Platform.Graphics
{
    internal class SpriteFrame
    {
        internal Texture texture;
        readonly Resource.ResourceManager resourceManager;
        readonly byte[] systemCopy;

        public Vector2 Size { get; set; }
        public float Resolution { get; set; } = -1;

        public bool HasSystemCopy
        {
            get { return systemCopy != null; }
        }

        public SpriteFrame(Resource.ResourceManager ResourceManager, Texture Texture, Vector2 size, byte[] systemCopy)
        {
            resourceManager = ResourceManager;
            texture = Texture;
            Size = size;
            loaded = Texture != null;
            this.systemCopy = systemCopy;
        }

        public SpriteFrame(Resource.ResourceManager ResourceManager)
        {
            resourceManager = ResourceManager;
            texture = null;
            Size = new(1, 1);
            loaded = false;
        }

        internal void RestoreFromSystemCopy()
        {
            SlimDX.DataRectangle data = texture.LockRectangle(0, SlimDX.Direct3D9.LockFlags.Discard);
            data.Data.Write(systemCopy, 0, systemCopy.Length);
            texture.UnlockRectangle(0);
        }

        internal Texture Texture
        {
            get 
            {
                if (texture != null && texture.Disposed)
                    texture = null;
                return texture ?? resourceManager.EmptyTexture; 
            }
            set
            {
                texture = value;
            }
        }

        public long TimeStamp;

        internal bool loaded;
        internal bool loading;
    }

    //internal class AniSpriteInternal : SpriteFrame
    //{
    //    //internal Sprite[] frames;

    //    internal Sprite current;

    //    public AniSpriteInternal(AnimationLoader loader)
    //    {
    //        current = new Sprite(loader.Frames[0]);
    //        frameCount = loader.FrameCount;

    //        //frames = new Sprite[loader.FrameCount];
    //        //frames[0] = new Sprite(loader.Frames[0]);

    //        //for (int i = 1; i < loader.FrameCount; i++)
    //        //    frames[i] = new Sprite(loader.Frames[i], loader.Frames[0]);
    //    }

    //    public Texture Texture
    //    {
    //        get { return SpriteManager.GetImage(current); }
    //    }

    //    public int Width
    //    {
    //        get { return current.Width; }
    //    }

    //    public int Height
    //    {
    //        get { return current.Height; }
    //    }

    //    int frameCount;
    //    public int FrameCount
    //    {
    //        get { return frameCount; }
    //    }
    //}
}
