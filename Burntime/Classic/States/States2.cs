using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Framework.States;
using Burntime.Data.BurnGfx;
using Burntime.Classic.Logic;

namespace Burntime.Classic
{
    public enum CharClass
    {
        Mercenary,
        Technician,
        Doctor,
        Boss,
        Mutant,
        Trader,
        Dog,

        Count
    }

    [Serializable]
    public class Fog : StateObject
    {
    }
}
