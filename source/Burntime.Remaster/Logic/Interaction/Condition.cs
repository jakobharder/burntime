using System;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform;

namespace Burntime.Remaster.Logic
{
    [Serializable]
    public class Condition : StateObject
    {
        StateLink<ItemType> requiredItem = null;
        float maxDistanceOnMap = 0;
        Rect regionOnMap;
        bool hasRegionOnMap = false;
        bool removeItem = false;

        public ItemType RequiredItem
        {
            get { return requiredItem == null ? null : requiredItem; }
            set { requiredItem = value; }
        }

        public float MaxDistanceOnMap
        {
            get { return maxDistanceOnMap; }
            set { maxDistanceOnMap = value; }
        }

        public Rect RegionOnMap
        {
            get { return regionOnMap; }
            set { regionOnMap = value; }
        }

        public bool HasRegionOnMap
        {
            get { return hasRegionOnMap; }
            set { hasRegionOnMap = value; }
        }

        public bool RemoveItem
        {
            get { return removeItem; }
            set { removeItem = value; }
        }

        public bool Process(Character character, out Conversation? hint)
        {
            hint = null;

            if (maxDistanceOnMap > 0 && hasRegionOnMap)
            {
                if (maxDistanceOnMap < regionOnMap.Distance(character.Position))
                    return false;
            }

            if (RequiredItem != null)
            {
                Item item = character.Items.Find(RequiredItem);
                if (item != null)
                {
                    if (removeItem)
                        character.Items.Remove(item);

                    return true;
                }
                else
                {
                    hint = GetHint();
                    return false;
                }
            }

            return true;
        }

        private Conversation? GetHint()
        {
            if (requiredItem is not null)
            {
                TextHelper txt = new(ResourceManager, "newburn");
                txt.AddArgument("|F", requiredItem.Object.Text);
                return Conversation.Simple(txt, 49);
            }

            return null;
        }
    }
}
