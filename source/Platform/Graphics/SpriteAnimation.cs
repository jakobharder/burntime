using System;

namespace Burntime.Platform.Graphics;

// TODO: use gameTime
[Serializable]
public class SpriteAnimation
{
    float frame;
#warning slimdx todo
    public int frameCount;
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

    public SpriteAnimation Clone()
    {
        return new SpriteAnimation(frameCount) { Speed = Speed };
    }

    bool ended;
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

