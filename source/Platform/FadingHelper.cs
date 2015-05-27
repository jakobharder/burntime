/*
 *  Burntime Platform
 *  Copyright (C) 2009
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 *  authors: 
 *    Juernjakob Harder (yn.harada@gmail.com)
 * 
*/

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
