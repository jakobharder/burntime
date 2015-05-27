using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Framework.States;
using Burntime.Data.BurnGfx;
using Burntime.Classic.Logic;
using Burntime.Classic.Logic.Interaction;

namespace Burntime.Classic.Logic.Data
{
    public class ItemTypeData : DataObject
    {
        public string ID
        {
            get { return DataName; }
        }

        public string[] Class;
        public bool IsSelectable;

        public string Sprite;
        public string Title;
        public string Text;

        public int FoodValue;
        public int WaterValue;
        public int HealthValue;
        public float EatValue;
        public float DrinkValue;
        public float TradeValue;
        public float HealValue;
        public int ExperienceValue;
        public int DamageValue;
        public int AmmoValue;

        public string Empty;
        public string Full;
        public string Production;
        public string[] Protection;
    }
}
