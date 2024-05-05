using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform.Resource;
using Burntime.Remaster.Logic.Data;

namespace Burntime.Remaster.Logic
{
    [Serializable]
    public class ItemTypes : StateObject
    {
        protected DataID<ItemTypesData> data;
        protected StateLinkList<ItemType> types;

        [NonSerialized]
        protected Dictionary<string, ItemType>? typeMap;

        public ItemType this[string id]
        {
            get
            {
                typeMap ??= GenerateMap();

                if (!typeMap.ContainsKey(id))
                {
                    ItemType dummy = container.Create<ItemType>(new object[] { data.Object.Items[0], id });
                    typeMap.Add(id, dummy);

                    Burntime.Platform.Log.Warning("item type " + id + " not found.");
                    return typeMap[id];
                }

                return typeMap[id];
            }
        }

        public ItemType this[int id]
        {
            get
            {
                return this[data.Object.BurnGfxIDs[id]];
            }
        }

        public bool Contains(string id)
        {
            typeMap ??= GenerateMap();
            return typeMap.ContainsKey(id);
        }

        /// <summary>
        /// Generate several items
        /// </summary>
        public Item? GenerateClass(string[] include, string[] exclude, float chance = 1.0f)
        {
            var types = GetTypesWithClass(include, exclude);
            if (types.Length == 0)
                return null;

            bool win = Platform.Math.Random.NextSingle() <= chance;
            if (!win)
                return null;

            var index = Platform.Math.Random.Next(types.Length);
            return Generate(types[index]);
        }

        /// <summary>
        /// Generate several items
        /// </summary>
        public IList<Item> GenerateClass(string[] include, string[] exclude, int min, int max)
        {
            if (min <= 0 || max <= 0)
                return Array.Empty<Item>();

            var list = new List<Item>();

            var types = GetTypesWithClass(include, exclude);
            if (types.Length == 0)
                return Array.Empty<Item>();

            int count = Platform.Math.Random.Next(min, max);

            for (int i = 0; i < count; i++)
            {
                var index = Platform.Math.Random.Next(types.Length - 1);
                list.Add(Generate(types[index]));
            }

            return list;
        }

        /// <summary>
        /// Generate item. Produces dummy if id is not found.
        /// </summary>
        public Item Generate(string id)
        {
            return this[id].Generate();
        }

        public string[] GetTypesWithClass(string[] include, string[] exclude)
        {
            var list = new List<string>();

            foreach (ItemType type in types)
            {
                // first look for excluded class flags
                bool excluded = false;
                foreach (string e in exclude)
                {
                    if (type.IsClass(e) || type.ID == e)
                    {
                        excluded = true;
                        break;
                    }
                }

                // if excluded then continue
                if (excluded)
                    continue;

                // now look for included flags
                foreach (string i in include)
                {
                    if (type.IsClass(i) || type.ID == i)
                    {
                        // if included then add to list
                        list.Add(type.ID);
                        break;
                    }
                }
            }

            return list.ToArray();
        }

        protected override void InitInstance(object[] parameter)
        {
            if (parameter.Length != 1)
                throw new BurntimeLogicException();

            types = container.CreateLinkList<ItemType>();

            // load item type data
            data = ResourceManager.GetData((string)parameter[0]);
            ItemTypeData[] typesData = data.Object.Items;

            // add state object links
            foreach (ItemTypeData typeData in typesData)
                types.Add(container.Create<ItemType>(new object[] { typeData }));

            foreach (ItemType type in types)
                type.InitializeLinks(this);
        }

        protected override void AfterDeserialization()
        {
            // make sure item type data is loaded
            data.Touch();

            base.AfterDeserialization();
        }

        private Dictionary<string, ItemType> GenerateMap()
        {
            var typeMap = new Dictionary<string, ItemType>();

            foreach (ItemType type in types)
                typeMap.Add(type.ID, type);

            return typeMap;
        }
    }
}
