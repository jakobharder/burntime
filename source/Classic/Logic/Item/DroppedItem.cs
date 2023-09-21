using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Framework.States;

namespace Burntime.Classic
{
    [Serializable]
    public class DroppedItem : StateObject, Maps.IMapObject
    {
        public StateLink<Item> Item;
        public Vector2 Position;
        public int Icon;

        public String GetTitle(ResourceManager ResourceManager)
        {
            return ResourceManager.GetString("burn?355");
        }

        protected override void InitInstance(object[] parameter)
        {
            base.InitInstance(parameter);

            Icon = Burntime.Platform.Math.Random.Next() % 2;
        }

        public Vector2 MapPosition
        {
            get { return Position; }
        }

        public Rect MapArea
        {
            get { return new Rect(Position.x - 4, Position.y - 4, 8, 8); }
        }
    }
}