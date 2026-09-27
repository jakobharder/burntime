using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Burntime.Framework.States;
using Burntime.Framework;
using Burntime.Remaster.Logic;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;
using Burntime.Data.BurnGfx.Save;

namespace Burntime.Remaster.Logic.Data
{
    public class ItemTypesData : DataObject
    {
        public sealed class DataProcessor : IDataProcessor
        {
            public DataObject Process(ResourceID id, IResourceManager resourceManager)
            {
                ConfigFile file = new ConfigFile();
                file.Open(resourceManager.ResolveFileReplacement(id.File));

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

                type.LastRoundSprite = section.GetString("image_last_round");
                type.Class = section.GetStrings("class");
                foreach (string function in section.GetStrings("function"))
                {
                    string name = string.Concat(function.Split(new[] { '_', '-' },
                        StringSplitOptions.RemoveEmptyEntries).Select(part =>
                            char.ToUpperInvariant(part[0]) + part[1..]));
                    if (Enum.TryParse(name, out ItemFunction value) && Enum.IsDefined(value))
                        type.Functions |= value;
                    else
                        Burntime.Platform.Log.Warning(
                            $"Unknown item function '{function}' on {section.Name}.");
                }

                type.FoodValue = section.GetInt("food");
                type.WaterValue = section.GetInt("water");
                type.HealValue = section.GetInt("heal");
                type.DamageValues = section.GetInts("damage");
                if (type.DamageValues.Length is not (0 or 1 or 16) || type.DamageValues.Any(value => value < 0))
                    throw new InvalidOperationException($"Invalid damage vector for {section.Name}: expected one value or four tiers of four rolls.");
                type.DamageValue = section.Name == "" || type.DamageValues.Length == 0 ? 0 : (int)type.DamageValues.Average();
                type.WeaponPriority = section.ContainsKey("weapon_priority") ? section.GetInt("weapon_priority") : null;
                type.AttackRange = section.GetInt("attack_range");
                type.Protection = section.GetStrings("protection");
                type.Production = "";
                type.Full = section.GetString("full");
                type.Empty = section.GetString("empty");
                type.AmmoValue = section.GetInt("ammo");
                type.Loads = section.GetStrings("loads");
                type.LoadName = section.GetString("load_name");
                type.DefenseValue = section.GetInt("defense");
                type.Fluff = section.Get("fluff");
                type.TraderWorldGroup = section.GetString("trader_world_group");
                type.TraderWorldLimit = Math.Max(0, section.GetInt("trader_world_limit"));

                if (section.Name != "" && (type.Protection.Length > 0 || type.DamageValue > 0 || type.DefenseValue > 0))
                {
                    type.IsSelectable = true;
                }

                list.Add(type);

                if (section.Name == "")
                    resourceManager.RegisterDataObject("item_dummy", type);
                else
                    resourceManager.RegisterDataObject(section.Name, type);
            }

            // Old saves reference this DataID. Resolve it from canonical data,
            // without retaining a second weapon definition or sprite mapping.
            ItemTypeData? rifle = list.FirstOrDefault(type => type.ID == "item_loaded_rifle");
            if (rifle != null)
                resourceManager.RegisterDataObject("item_loaded_rifle_1", rifle.CreateAlias());
        }
    }
}
