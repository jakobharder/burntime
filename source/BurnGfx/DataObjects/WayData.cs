using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Platform.Graphics;

namespace Burntime.Data.BurnGfx
{
    public struct Way
    {
        public Sprite[] Images;
        public int Start;
        public int End;
        public Vector2 Position;
    }

    public struct CrossWay
    {
        public int[] Ways;
    }

    public class WayData : DataObject
    {
        public Way[] Ways;
        public CrossWay[] Cross;
    }
}
