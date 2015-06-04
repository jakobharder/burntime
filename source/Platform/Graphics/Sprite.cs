
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
using System.Collections.Generic;
using System.Text;

using Burntime.Platform.Resource;

namespace Burntime.Platform.Graphics
{
    // TODO: use gameTime
    [Serializable]
    public class SpriteAnimation
    {
        float frame;
        internal int frameCount;
        float intervalMargin = 0;
        float pause = 0;
        bool endless = true;
        float delay = 0;

        public int Frame
        {
            get { return (int)System.Math.Floor(frame); }
            set { frame = value; }
        }

        public int FrameCount
        {
            get { return frameCount; }
        }

        public float IntervalMargin
        {
            get { return intervalMargin; }
            set { intervalMargin = value; }
        }

        public float Delay
        {
            get { return delay; }
            set { pause = value; delay = value; }
        }

        public float Speed = 5;

        public bool Endless
        {
            get { return endless; }
            set { endless = value; }
        }
        public bool Progressive = true;

        protected bool reverse;
        public bool ReverseAnimation
        {
            set { reverse = value; }
            get { return reverse; }
        }

        //public bool ColorKey
        //{
        //    set
        //    {
        //        for (int i = 0; i < frameCount; i++)
        //        {

        //        }
        //    }
        //    get { if (frameCount <= 0) return false; return frames[0].ColorKey; }
        //}

        protected bool running;
        public void Stop()
        {
            running = false;
        }

        public void Start()
        {
            ended = false;
            running = true;
            pause = delay;
        }

        public void GoLastFrame()
        {
            frame = frameCount - 0.0001f;
        }

        public void GoFirstFrame()
        {
            frame = 0;
        }

        public SpriteAnimation(int FrameCount)
        {
            frameCount = FrameCount;
            frame = 0;
            endless = true;
            running = true;
        }

        bool ended ;
        public bool End
        {
            //get { if (!reverse) return (frame == frameCount - 1); else return (frame == 0); }
            get { return ended; }
        }

        public void Update(float elapsed)
        {
            if (ended || frameCount == 0 || !running)
                return;

            if (pause > 0)
            {
                pause -= elapsed;
                return;
            }

            if (!endless)
            {
                if (!reverse)
                {
                    frame += elapsed * Speed;
                    if (frame > frameCount - 0.0001f)
                    {
                        frame = frameCount - 0.0001f;
                        ended = true;
                        running = false;
                    }
                }
                else
                {
                    frame -= elapsed * Speed;
                    if (frame < 0)
                    {
                        frame = 0;
                        ended = true;
                        running = false;
                    }
                }
            }
            else
            {
                if (!reverse)
                {
                    frame += elapsed * Speed;
                    while (frame >= frameCount)
                    {
                        pause = intervalMargin;
                        frame -= frameCount;
                    }
                }
                else
                {
                    frame -= elapsed * Speed;
                    while (frame < 0)
                    {
                        pause = intervalMargin;
                        frame += frameCount;
                    }
                }
            }
        }
    }

    public class Sprite : DataObject
    {
        public ResourceLoadType LoadType = ResourceLoadType.Now;
        protected ResourceManager resMan;
        protected bool colorKey = true;
        internal SpriteFrame[] internalFrames;
        internal ResourceID id;
        internal SpriteAnimation ani;

        // sprite resolution relative to global resolution
        protected float resolution = 1;
        public float Resolution
        {
            get { return resolution; }
            set { resolution = value; }
        }

        protected Sprite()
        {
        }

        public bool IsLoaded
        {
            get { return (internalFrames != null && internalFrames[0].loaded); }
        }

        public bool IsLoading
        {
            get { return (internalFrames != null && internalFrames[0].loading); }
        }

        public int Width
        {
            get { /*Load();*/ if (internalFrames[0] == null) return 0; else return (int)(Frame.Width * resolution); }
        }
        public int Height
        {
            get { /*Load();*/ if (internalFrames[0] == null) return 0; else return (int)(Frame.Height * resolution); }
        }

        public Vector2 Size
        {
            get { return new Vector2(Width, Height); }
        }

        public Vector2 OriginalSize
        {
            get { if (internalFrames[0] == null) return new Vector2(); return new Vector2(Frame.Width, Frame.Height); }
        }

        public int CurrentFrame
        {
            get { if (ani != null) return ani.Frame; return 0; }
        }

        public bool ColorKey
        {
            get { return colorKey; }
            set { colorKey = value; }
        }

        public ResourceID ID
        {
            get { return id; }
        }

        public SpriteAnimation Animation
        {
            get { return ani; }
        }

        public void CopyTo(Sprite Sprite)
        {
            Sprite.resMan = resMan;
            Sprite.id = id;
            Sprite.internalFrames = internalFrames;
            Sprite.LoadType = LoadType;
            if (ani != null)
                Sprite.ani = new SpriteAnimation(ani.FrameCount);
            Sprite.Resolution = Resolution;
        }

        public Sprite Clone()
        {
            Sprite sprite = new Sprite(resMan, id, internalFrames, ani);
            sprite.resolution = resolution;
            return sprite;
        }

        public void Update(float Elapsed)
        {
            if (ani != null && internalFrames != null)
                ani.Update(Elapsed);
        }

        public void Touch()
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

        internal Sprite(ResourceManager ResMan, String ID, SpriteFrame Frame)
        {
            id = ID;
            internalFrames = new SpriteFrame[1];
            internalFrames[0] = Frame;
            resMan = ResMan;
        }

        internal Sprite(ResourceManager resMan, String id, SpriteFrame[] frames, SpriteAnimation animation)
        {
            this.id = id;
            internalFrames = frames;
            this.resMan = resMan;
            ani = animation;
        }

        internal void Unload()
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
