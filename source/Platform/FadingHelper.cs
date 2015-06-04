
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
