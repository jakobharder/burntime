using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Framework.States;
using Burntime.Data.BurnGfx;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.Logic
{
    [Serializable]
    public class Ways : StateObject
    {
        protected DataID<WayData> wayData;

        public WayData WayData
        {
            get { return wayData; }
        }

        protected override void InitInstance(object[] parameter)
        {
            if (parameter.Length != 1)
                throw new Burntime.Framework.BurntimeLogicException();

            wayData = ResourceManager.GetData((string)parameter[0]);
        }
    }
}
