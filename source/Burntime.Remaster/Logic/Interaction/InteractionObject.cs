using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Remaster.Maps;

namespace Burntime.Remaster.Logic.Interaction
{
    public class InteractionObject
    {
        IMapObject mapObject;
        IInteractionHandler handler;
        Condition condition;

        public Vector2 Position
        {
            get { return mapObject.MapPosition; }
        }

        public float MaximumRange
        {
            get { if (condition != null) return condition.MaxDistanceOnMap; else return 0; }
        }

        public bool IsInRange(Vector2 position)
        {
            if (condition != null)
            {
                if (condition.MaxDistanceOnMap > 0 && condition.HasRegionOnMap)
                {
                    if (condition.MaxDistanceOnMap < condition.RegionOnMap.Distance(position))
                        return false;
                }
                else
                {
                    return condition.MaxDistanceOnMap >= (Position - position).Length;
                }
            }
            else return 15 >= (Position - position).Length; 

            return true;
        }

        public InteractionObject(IMapObject mapObject, IInteractionHandler handler)
        {
            this.mapObject = mapObject;
            this.handler = handler;
        }

        public InteractionObject(IMapObject mapObject, Condition condition, IInteractionHandler handler)
        {
            this.mapObject = mapObject;
            this.handler = handler;
            this.condition = condition;
        }

        public void Interact(Character actor)
        {
            handler.HandleInteraction(mapObject, actor);
        }
    }
}
