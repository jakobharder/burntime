using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Remaster.Maps;

namespace Burntime.Remaster.Logic.Interaction
{
    public interface IInteractionHandler
    {
        bool HandleInteraction(IMapObject mapObject, Character actor);
    }
}
