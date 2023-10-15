using Burntime.Remaster.Logic.Interaction;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;

namespace Burntime.Remaster.ResourceProcessor
{
    class ConstructionsProcessor : IDataProcessor
    {
        public DataObject Process(ResourceID id, IResourceManager resourceManager)
        {
            ConfigFile file = new ConfigFile();
            file.Open(id.File);
            return new Constructions(file);
        }

        string[] IDataProcessor.Names
        {
            get { return new string[] { "constructions" }; }
        }
    }
}
