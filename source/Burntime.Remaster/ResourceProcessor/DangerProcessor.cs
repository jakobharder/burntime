using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform.Resource;
using Burntime.Remaster.Logic.Interaction;

namespace Burntime.Remaster.ResourceProcessor
{
    class DangerProcessor : IDataProcessor
    {
        public DataObject Process(ResourceID id, IResourceManager resourceManager)
        {
            Danger danger;

            int value = 0;
            if (!int.TryParse(id.Custom, out value))
                Burntime.Platform.Log.Warning("DangerProcessor: custom parameter is not an integer.");

            switch (id.File)
            {
                case "radiation":
                    danger = new Danger(id.File, value, resourceManager.GetString("burn?408"), resourceManager.GetImage("inf.ani?3"));
                    break;
                case "gas":
                    danger = new Danger(id.File, value, resourceManager.GetString("burn?413"), resourceManager.GetImage("inf.ani?7"));
                    break;
                default:
                    throw new Burntime.Framework.BurntimeLogicException();
            }

            return danger;
        }

        string[] IDataProcessor.Names
        {
            get { return new string[] { "danger" }; }
        }
    }
}
