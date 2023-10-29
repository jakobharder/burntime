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

        static public int Min(int val1, int val2)
        {
            return val1 < val2 ? val1 : val2;
        }

        static public int Min(int val1, int val2, int val3)
        {
            int min = (val1 < val2 ? val1 : val2);
            return val3 < min ? val3 : min;
        }

        static public int Min(params int[] values)
        {
            int min = values[0];
            foreach (int val in values)
                if (val < min) min = val;
            return min;
        }

        static public int Max(int val1, int val2)
        {
            return val1 > val2 ? val1 : val2;
        }

        static public int Max(int val1, int val2, int val3)
        {
            int max = (val1 > val2 ? val1 : val2);
            return val3 > max ? val3 : max;
        }

        static public int Max(params int[] values)
        {
            int max = values[0];
            foreach (int val in values)
                if (val > max) max = val;
            return max;
        }
    }
}
