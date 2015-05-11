using System;
using Burntime.Data.BurnGfx;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform.Resource;

namespace Burntime.Classic
{
    [Serializable]
    public class Map : StateObject
    {
        protected DataID<MapData> mapData;
        public MapData MapData
        {
            get { return mapData; }
        }

        public MapEntrance[] Entrances
        {
            get { return MapData.Entrances; }
        }

        public PathMask Mask
        {
            get { return MapData.Mask; }
        }

        protected override void InitInstance(object[] parameter)
        {
            if (parameter.Length != 1)
                throw new BurntimeLogicException();

            mapData = ResourceManager.GetData((string)parameter[0], ResourceLoadType.LinkOnly);
        }
    }
}