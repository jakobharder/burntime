using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform.Resource;
using Burntime.Remaster.Logic.Data;
using Burntime.Remaster.Logic.Interaction;
using System;
using System.Collections.Generic;

namespace Burntime.Remaster.Logic
{
    [Serializable]
    public class ItemType : StateObject
    {
        protected string dummy;
        protected DataID<ItemTypeData> data;

        protected StateLink<ItemType> empty;
        protected StateLink<ItemType> full;
        protected StateLink<Production> production;
        protected DataID<DangerProtection>[] protection;

        protected override void InitInstance(object[] parameter)
        {
            if (parameter.Length < 1 || parameter.Length > 2)
                throw new BurntimeLogicException();

            data = (DataObject)parameter[0];
            if (parameter.Length == 2)
                dummy = (string)parameter[1];

            List<DataID<DangerProtection>> list = new List<DataID<DangerProtection>>();
            foreach (string str in data.Object.Protection)
                list.Add(ResourceManager.GetData(str));

            protection = list.ToArray();
        }

        public void InitializeLinks(ItemTypes types)
        {
            if (data.Object.Empty != "")
                empty = types[data.Object.Empty];

            if (data.Object.Full != "")
                full = types[data.Object.Full];
        }

        public string ID
        {
            get
            {
                if (dummy != null)
                    return dummy;
                return data.Object.ID;
            }
        }

        public static implicit operator string(ItemType right)
        {
            return right.ID;
        }

        public bool IsClass(string name)
        {
            // check item for specified class tag
            foreach (string str in data.Object.Class)
            {
                if (str.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }

            return false;
        }

        public bool IsSelectable => data.Object.IsSelectable;
        public string Sprite => data.Object.Sprite;

        public string Title => dummy ??
            (string.IsNullOrEmpty(data.Object.Title) ? data.Object.ID : ResourceManager.GetString(data.Object.Title));

        public string Text => dummy ??
            (string.IsNullOrEmpty(data.Object.Text) ? data.Object.ID : ResourceManager.GetString(data.Object.Text));

        public string? FluffText => string.IsNullOrEmpty(data.Object.Fluff) ? null : ResourceManager.GetString(data.Object.Fluff);

        public int FoodValue => data.Object.FoodValue;
        public int WaterValue => data.Object.WaterValue;
        public int HealthValue => data.Object.HealthValue;
        public float EatValue => data.Object.EatValue;
        public float DrinkValue => data.Object.DrinkValue;
        public float TradeValue => data.Object.TradeValue;
        public float HealValue => data.Object.HealValue;
        public int ExperienceValue => data.Object.ExperienceValue;
        public int DamageValue => data.Object.DamageValue;
        public int DefenseValue => data.Object.DefenseValue;
        public int AmmoValue => data.Object.AmmoValue;

        public ItemType Empty => empty;
        public ItemType Full => full;

        public Production Production
        {
            get { return production; }
            set { production = value; }
        }

        public DataID<DangerProtection>[] Protection
        {
            get { return protection; }
        }

        public DangerProtection GetProtection(string type)
        {
            foreach (DataID<DangerProtection> p in protection)
            {
                if (type == p.Object.Type)
                    return p.Object;
            }

            return null;
        }

        public Item Generate()
        {
            return container.Create<Item>(new object[] { this });
        }
    }
}