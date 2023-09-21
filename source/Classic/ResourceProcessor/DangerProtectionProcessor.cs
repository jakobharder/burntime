﻿using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform.Resource;
using Burntime.Classic.Logic.Interaction;

namespace Burntime.Classic.ResourceProcessor
{
    class DangerProtectionProcessor : IDataProcessor
    {
        public DataObject Process(ResourceID id, ResourceManager resourceManager)
        {
            int value = 0;
            if (!int.TryParse(id.Custom, out value))
                Burntime.Platform.Log.Warning("DangerProtectionProcessor: custom parameter is not an integer.");

            return new DangerProtection(id.File.ToLower(), value * 0.01f);
        }

        string[] IDataProcessor.Names
        {
            get { return new string[] { "protection" }; }
        }
    }
}
