using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Platform
{
    static public class Math
    {
        static Random random = new Random();
        static public Random Random
        {
            get { return random; }
        }
    }
}
