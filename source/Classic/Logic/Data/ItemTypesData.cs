using System;
using System.Collections.Generic;
using System.Text;
using Burntime.Framework.States;
using Burntime.Framework;
using Burntime.Classic.Logic;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;
using Burntime.Data.BurnGfx.Save;

namespace Burntime.Classic.Logic.Data
{
    public class ItemTypesData : DataObject
    {
        public sealed class DataProcessor : IDataProcessor
        {
            public DataObject Process(ResourceID id, IResourceManager resourceManager)
            {
                ConfigFile file = new ConfigFile();
                file.Open(id.File);

                return new ItemTypesData(file, resourceManager);
            }

            string[] IDataProcessor.Names
            {
                get { return new string[] { "items" }; }
            }
        }

        List<ItemTypeData> list;
        string[] burnGfxIDs;

        public ItemTypeData[] Items
        {
            get { return list.ToArray(); }
        }

        public string[] BurnGfxIDs
        {
            get { return burnGfxIDs; }
        }

        protected ItemTypesData(ConfigFile file, IResourceManager resourceManager)
        {
            list = new List<ItemTypeData>();

            // create burngfx id to string convertion array
            burnGfxIDs = new string[58];

            // load item types
            ConfigSection[] sections = file.GetAllSections();
            foreach (ConfigSection section in sections)
            {
                ItemTypeData type = new ItemTypeData();

                if (section.ContainsKey("burngfx"))
                {
                    // use burngfx attributes
                    int burngfx = section.GetInt("burngfx");
                    type.Sprite = "gst.raw?" + burngfx;
                    type.Title = "@burn?" + (50 + burngfx);
                    type.Text = "@burn?" + (110 + burngfx);
                    type.TradeValue = Burntime.Data.BurnGfx.ConstValues.GetValue(burngfx) / 4.0f;
                    type.EatValue = Burntime.Data.BurnGfx.ConstValues.GetValue(burngfx) / 4.0f;
                    type.DrinkValue = Burntime.Data.BurnGfx.ConstValues.GetValue(burngfx) / 4.0f;

                    burnGfxIDs[burngfx] = section.Name;

                    // use attributes from file
                    if (section.ContainsKey("image"))
                        type.Sprite = section.Get("image");
                    if (section.ContainsKey("title"))
                        type.Title = section.Get("title");
                    if (section.ContainsKey("text"))
                        type.Text = section.Get("text");
                    if (section.ContainsKey("value"))
                        type.TradeValue = section.GetInt("value") / 4.0f;
                    if (section.ContainsKey("value"))
                        type.EatValue = section.GetInt("value") / 4.0f;
                    if (section.ContainsKey("value"))
                        type.DrinkValue = section.GetInt("value") / 4.0f;
                }
                else
                {
                    // use attributes from file
                    type.Sprite = section.Get("image");
                    type.Title = section.Get("title");
                    type.Text = section.Get("text");
                    type.TradeValue = section.GetInt("value") / 4.0f;
                    type.EatValue = section.GetInt("value") / 4.0f;
                    type.DrinkValue = section.GetInt("value") / 4.0f;
                }

                type.Class = section.GetStrings("class");

                type.FoodValue = section.GetInt("food");
                type.WaterValue = section.GetInt("water");
                type.HealValue = section.GetInt("heal");
                type.DamageValue = section.GetInt("damage");
                type.Protection = section.GetStrings("protection");
                type.Production = "";
                type.Full = section.GetString("full");
                type.Empty = section.GetString("empty");
                type.AmmoValue = section.GetInt("ammo");
                type.DefenseValue = section.GetInt("defense");

                if (type.Protection.Length > 0 || type.DamageValue > 0 || type.DefenseValue > 0)
                {
                    type.IsSelectable = true;
                }

                list.Add(type);

                if (section.Name == "")
                    resourceManager.RegisterDataObject("item_dummy", type);
                else
                    resourceManager.RegisterDataObject(section.Name, type);
            }
        }
    }
}
