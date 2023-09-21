﻿using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform.Resource;
using Burntime.Classic.Logic.Data;

namespace Burntime.Classic.Logic
{
    [Serializable]
    public class ItemTypes : StateObject
    {
        protected DataID<ItemTypesData> data;
        protected StateLinkList<ItemType> types;

        [NonSerialized]
        protected Dictionary<string, ItemType> typeMap;

        public ItemType this[string id]
        {
            get
            {
                if (typeMap == null)
                    GenerateMap();

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
            if (typeMap == null)
                GenerateMap();

            return typeMap.ContainsKey(id);
        }

        public Item Generate(string id)
        {
            return this[id].Generate();
        }

        public string[] GetTypesWithClass(string[] include, string[] exclude)
        {
            List<string> list = new List<string>();

            foreach (ItemType type in types)
            {
                // first look for excluded class flags
                bool excluded = false;
                foreach (string e in exclude)
                {
                    if (type.IsClass(e))
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
                    if (type.IsClass(i))
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

        private void GenerateMap()
        {
            // recreate id - object map
            typeMap = new Dictionary<string, ItemType>();

            foreach (ItemType type in types)
                typeMap.Add(type.ID, type);
        }
    }
}
