using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Platform
{
    public class FadingHelper
    {
        public FadingHelper()
        {
        }

        public FadingHelper(float Speed)
        {
            speed = Speed;
        }

        float speed;
        public float Speed
        {
            get { return speed; }
            set { speed = value; }
        }

        float state = 0;
        public float State
        {
            get { return state; }
            set { state = value; }
        }

        public bool IsIn
        {
            get { return state == fade && fade != 0; }
        }

        public bool IsOut
        {
            get { return state == 0; }
        }

        public bool IsFadingIn
        {
            get { return fade > state; }
        }

        public bool IsFadingOut
        {
            get { return fade < state; }
        }

        float fade;
        public float FadeTo
        {
            get { return fade; }
            set { fade = value; }
        }

        public void FadeIn()
        {
            fade = 1;
        }

        public void FadeOut()
        {
            fade = 0;
        }

        public void Stop()
        {
            fade = state;
        }

        public void Update(float Elapsed)
        {
            if (IsFadingOut)
            {
                state -= Elapsed * speed;
                if (state < fade)
                    state = fade;
            }
            else if (IsFadingIn)
            {
                state += Elapsed * speed;
                if (state > fade)
                    state = fade;
            }
        }
    }

}
