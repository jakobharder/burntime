using Burntime.Data.BurnGfx;
using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Interaction;
using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Remaster
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
        // One-based original record slot; zero means an older save/unassigned item.
        [System.Runtime.Serialization.OptionalField]
        internal int OriginalRecordSlot;

        public string ID
        {
            get { return Type.ID; }
        }

        public string Sprite
        {
            get { return ammo == 1 && !string.IsNullOrEmpty(Type.LastRoundSprite) ? Type.LastRoundSprite : Type.Sprite; }
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
        [System.Runtime.Serialization.OptionalField]
        int ammunitionFormat;
        public string TooltipText => Type.AmmoValue > 0 ? $"{Title} ({ammo}/{Type.AmmoValue})" : Title;
        public int AmmoValue => ammo;

        /// <summary>
        /// Weapon that requires ammo.
        /// </summary>
        public bool ConsumesAmmo => ammo != 0;

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
            ammunitionFormat = 1;
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

        internal void MigrateAmmunition(Func<string, ItemType> resolve)
        {
            // The old Extended first stage had one stored round plus a second
            // one-round item type. Read the saved link before refreshing links.
            if (ammunitionFormat == 0 && ID == "item_loaded_rifle" &&
                Type.Empty?.ID == "item_loaded_rifle_1")
                ammo++;
            if (ID == "item_loaded_rifle_1")
                Type = resolve("item_loaded_rifle");
            ammunitionFormat = 1;
        }

        internal void Reload(ItemType loadedType)
        {
            Type = loadedType;
            ammo = loadedType.AmmoValue;
        }

        // for debug
        public override string ToString()
        {
            return this.Title;
        }
    }
}
