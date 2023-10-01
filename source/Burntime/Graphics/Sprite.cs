using System;
using System.Diagnostics;

using Burntime.Platform.Resource;
using SlimDX.Direct3D9;

namespace Burntime.Platform.Graphics
{
    [DebuggerDisplay("Sprite = {id.ToString()}")]
    public class Sprite : ISprite
    {
        public ResourceLoadType LoadType = ResourceLoadType.Now;
        protected Resource.ResourceManager resMan;
        protected bool colorKey = true;
        internal SpriteFrame[] internalFrames;
        internal ResourceID id;
        internal SpriteAnimation ani;

        protected Sprite()
        {
        }

        public bool IsNew { get; set; } = true;

        public override bool IsLoaded => (internalFrames != null && internalFrames[0].loaded);

        public bool IsLoading
        {
            get { return (internalFrames != null && internalFrames[0].loading); }
        }

        public override Vector2 Size => Frame.Size * Frame.Resolution;
        public Vector2 OriginalSize => Frame.Size;

        public int CurrentFrame
        {
            get { if (ani != null) return ani.Frame; return 0; }
        }

        public bool ColorKey
        {
            get { return colorKey; }
            set { colorKey = value; }
        }

        public override ResourceID ID
        {
            get { return id; }
        }

        public override SpriteAnimation Animation => ani;
        public override float Resolution
        {
            get => internalFrames[0].Resolution;
            set => internalFrames[0].Resolution = value;
        }

        public Sprite Clone()
        {
            return new (resMan, id, internalFrames, ani);
        }

        public override void Update(float elapsed)
        {
            if (ani != null && internalFrames != null)
                ani.Update(elapsed);
        }

        public override void Touch()
        {
            Load();
        }

        // internal access
        internal SpriteFrame Frame
        {
            get 
            { 
                Load();
                if (CurrentFrame >= internalFrames.Length)
                    return internalFrames[0];
                return internalFrames[CurrentFrame]; 
            }
        }

        internal SpriteFrame[] Frames
        {
            get { Load(); return internalFrames; }
        }

        internal Sprite(Resource.ResourceManager ResMan, String ID, SpriteFrame Frame)
        {
            id = ID;
            internalFrames = new SpriteFrame[1];
            internalFrames[0] = Frame;
            resMan = ResMan;
        }

        internal Sprite(Resource.ResourceManager resMan, String id, SpriteFrame[] frames, SpriteAnimation animation)
        {
            this.id = id;
            internalFrames = frames;
            this.resMan = resMan;
            ani = animation;
        }

        public override void Unload()
        {
            if (internalFrames != null)
            {
                for (int i = 0; i < internalFrames.Length; i++)
                {
                    if (internalFrames[i].Texture != null)
                    {
                        internalFrames[i].Texture.Dispose();
                        internalFrames[i].Texture = null;
                    }
                }

                internalFrames[0].loaded = false;
            }
        }

        internal void Load()
        {
            if (IsLoaded || IsLoading)
                return;

            resMan.Reload(this, ResourceLoadType.Delayed);
        }
    }
}
