
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

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Framework.States;
using Burntime.Data.BurnGfx;
using Burntime.Classic.Logic;
using Burntime.Classic.Logic.Interaction;

namespace Burntime.Classic
{
    public enum ItemClass
    {
        Default,
        Weapon,
        Protection
    }

    [Serializable]
    public class Item : StateObject
    {
        protected int ammo;

        public string ID
        {
            get { return Type.ID; }
        }

        public string Sprite
        {
            get { return Type.Sprite; }
        }

        public string Title
        {
            get { return Type.Title; }
        }

        public string Text
        {
            get { return Type.Text; }
        }

        protected StateLink<ItemType> type;
        public ItemType Type
        {
            get { return type; }
            set { type = value; }
        }

        // logic
        // food
        public int FoodValue
        {
            get { return Type.FoodValue; }
        }

        // water
        public int WaterValue
        {
            get { return Type.WaterValue; }
        }

        // medpacks, ...
        public int HealthValue
        {
            get { return Type.HealthValue; }
        }

        // drugs, books, maybe quest items...
        public int ExperienceValue
        {
            get { return Type.ExperienceValue; }
        }

        // restaurant
        public float EatValue
        {
            get { return Type.EatValue; }
        }

        // pub
        public float DrinkValue
        {
            get { return Type.DrinkValue; }
        }

        // trader
        public float TradeValue
        {
            get { return Type.TradeValue; }
        }

        // doctor
        public float HealValue
        {
            get { return Type.HealValue; }
        }

        // damage
        public int DamageValue
        {
            get { return Type.DamageValue; }
        }

        // remaining bullets
        public int AmmoValue
        {
            get { return ammo; }
        }

        public bool IsSelectable
        {
            get { return Type.IsSelectable; }
        }

        //public ItemClass ItemClass
        //{
        //    get { return Type.Class; }
        //}

        protected override void InitInstance(object[] parameter)
        {
            if (parameter.Length != 1)
                throw new Burntime.Framework.BurntimeLogicException();

            Type = (ItemType)parameter[0];
            ammo = Type.AmmoValue;
        }

        public void Use()
        {
            ammo--;
            if (ammo == 0)
            {
                Type = Type.Empty;
                ammo = Type.AmmoValue;
            }
        }

        public void MakeEmpty()
        {
            Type = Type.Empty;
        }

        public void MakeFull()
        {
            Type = Type.Full;
        }

        // for debug
        public override string ToString()
        {
            return this.Title;
        }
    }
}
