using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;

namespace Burntime.Data.BurnGfx
{
    public enum TileMaskType
    {
        None,
        Simple,
        Complex
    }

    public class TileMaskData : DataObject
    {
        public TileMaskType Type;
        public bool[] Mask;

        public bool this[int x, int y]
        {
            get { return Mask[x + y * 4]; }
        }

        public bool this[Vector2 pos]
        {
            get { return Mask[pos.x + pos.y * 4]; }
        }
    }
}
