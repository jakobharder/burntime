
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform.Resource;
using Burntime.Classic.Logic.Data;
using Burntime.Classic.Logic.Interaction;

namespace Burntime.Classic.Logic
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

        public bool IsSelectable
        {
            get { return data.Object.IsSelectable; }
        }

        public string Sprite
        {
            get { return data.Object.Sprite; }
        }

        public string Title
        {
            get
            {
                if (dummy != null)
                    return dummy;
                if (data.Object.Title == "")
                    return data.Object.ID;
                return ResourceManager.GetString(data.Object.Title);
            }
        }

        public string Text
        {
            get
            {
                if (dummy != null)
                    return dummy;
                if (data.Object.Text == "")
                    return data.Object.ID;
                return ResourceManager.GetString(data.Object.Text);
            }
        }

        public int FoodValue
        {
            get { return data.Object.FoodValue; }
        }

        public int WaterValue
        {
            get { return data.Object.WaterValue; }
        }

        public int HealthValue
        {
            get { return data.Object.HealthValue; }
        }

        public float EatValue
        {
            get { return data.Object.EatValue; }
        }

        public float DrinkValue
        {
            get { return data.Object.DrinkValue; }
        }

        public float TradeValue
        {
            get { return data.Object.TradeValue; }
        }

        public float HealValue
        {
            get { return data.Object.HealValue; }
        }

        public int ExperienceValue
        {
            get { return data.Object.ExperienceValue; }
        }

        public int DamageValue
        {
            get { return data.Object.DamageValue; }
        }

        public int AmmoValue
        {
            get { return data.Object.AmmoValue; }
        }

        public ItemType Empty
        {
            get { return empty; }
        }

        public ItemType Full
        {
            get { return full; }
        }

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