using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Classic.Maps;

namespace Burntime.Classic.Logic.Interaction
{
    public interface IInteractionHandler
    {
        bool HandleInteraction(IMapObject mapObject, Character actor);
    }
}
