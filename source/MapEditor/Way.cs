using System;
using System.Collections.Generic;
using System.Drawing;

namespace MapEditor
{
    public class Way
    {
        public int[] Entrance = new int[2];
        public List<Point> Points = new List<Point>();
        public int Days = 1;
    }
}
