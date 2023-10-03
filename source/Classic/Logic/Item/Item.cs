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

        public int DamageValue => Type.DamageValue;
        public int DefenseValue => Type.DefenseValue;

        // remaining bullets
        protected int ammo;
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
