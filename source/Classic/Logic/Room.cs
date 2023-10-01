using System;
using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Framework.States;
using Burntime.Classic.Maps;

namespace Burntime.Classic.Logic
{
    [Serializable]
    public class Room : StateObject, IMapObject
    {
        StateLink<Condition> entryCondition;
        StateLink<ItemList> items;
        bool isWaterSource;
        string titleId;

        public string TitleId
        {
            get { return titleId; }
            set { titleId = value; }
        }

        public ItemList Items
        {
            get { return items; }
            set { items = value; }
        }

        public bool IsWaterSource
        {
            get { return isWaterSource; }
            set { isWaterSource = value; }
        }

        public Condition EntryCondition
        {
            get { return entryCondition == null ? null : entryCondition; }
            set { entryCondition = value; }
        }

        protected override void InitInstance(object[] parameter)
        {
            items = container.Create<ItemList>();
            entryCondition = container.Create<Condition>();

            base.InitInstance(parameter);
        }

        public Vector2 MapPosition
        {
            get { return EntryCondition.RegionOnMap.Center; }
        }

        public Rect MapArea
        {
            get { return EntryCondition.RegionOnMap; }
        }

        public String GetTitle(IResourceManager resourceManager)
        {
            return resourceManager.GetString(titleId);
        }
    }
}
