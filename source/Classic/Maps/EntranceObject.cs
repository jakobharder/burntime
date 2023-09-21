using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Data.BurnGfx;

namespace Burntime.Classic.Maps
{
    public class EntranceObject : IMapObject
    {
        MapEntrance entrance;
        int number;

        public string GetTitle(Burntime.Platform.Resource.ResourceManager resourceManager)
        {
            return "";
        }

        public MapEntrance Data
        {
            get { return entrance; }
        }

        public int Number
        {
            get { return number; }
        }

        public EntranceObject(MapEntrance entrance, int number)
        {
            this.entrance = entrance;
            this.number = number;
        }

        Vector2 IMapObject.MapPosition
        {
            get { return entrance.Area.Center; }
        }

        Rect IMapObject.MapArea
        {
            get { return entrance.Area; }
        }
    }
}
