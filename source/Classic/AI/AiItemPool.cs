
#region GNU General Public License
/*
 *  Burntime Classic
 *  Copyright (C) 2009
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 *  authors: 
 *    Juernjakob Harder (yn.harada@gmail.com)
 * 
*/
#endregion

using System;
using Burntime.Classic.Logic;
using Burntime.Framework.States;

namespace Burntime.Classic.AI
{
    /// <summary>
    /// item pool for AI
    /// </summary>
    [Serializable]
    class AiItemPool : StateObject
    {
        static readonly string[] ItemTypeFilter = new string[] { 
            "item_knife", "item_axe", "item_pitchfork", "item_loaded_rifle", 
            "item_loaded_pistol", "item_gas_mask", "item_protection_suit", 
            "item_paper_helmet", "item_rat_trap", "item_snake_trap", "item_trap" };

        #region protected class PoolItem
        /// <summary>
        /// item type + count structure
        /// </summary>
        [Serializable]
        protected class PoolItem : StateObject
        {
            protected StateLink<ItemType> type;
            protected int count;

            public ItemType Type
            {
                get { return type; }
            }

            public int Count
            {
                get { return count; }
                set { count = value; }
            }

            protected override void InitInstance(object[] parameter)
            {
                this.type = parameter[0] as ItemType;
                count = 0;
            }
        }
        #endregion

        #region protected attributes
        protected StateLinkList<PoolItem> items;
        #endregion

        #region protected initialize
        /// <summary>
        /// StateObject initialization
        /// </summary>
        /// <param name="parameter">none</param>
        protected override void InitInstance(object[] parameter)
        {
            base.InitInstance(parameter);

            items = container.CreateLinkList<PoolItem>();
        }
        #endregion

        #region public insert item methods
        /// <summary>
        /// Insert item into pool.
        /// </summary>
        /// <param name="item">item to be inserted</param>
        public void Insert(Item item)
        {
            // filter item type
            bool found = false;
            foreach (string str in ItemTypeFilter)
            {
                if (str == item.Type.ID)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
                return;

            PoolItem poolItem = FindPoolItem(item.Type);

            // if this type is not yet in pool, create it
            if (poolItem == null)
            {
                poolItem = container.Create<PoolItem>(item.Type);
                items.Add(poolItem);
            }

            // increase count
            poolItem.Count++;
        }

        /// <summary>
        /// Insert items into pool.
        /// </summary>
        /// <param name="items">collection of items to be inserted</param>
        public void Insert(IItemCollection items)
        {
            foreach (Item item in items)
            {
                Insert(item);
            }
        }

        /// <summary>
        /// Insert item by item type into pool.
        /// </summary>
        /// <param name="type">item type to be inserted</param>
        public void Insert(ItemType type)
        {
            // filter item type
            bool found = false;
            foreach (string str in ItemTypeFilter)
            {
                if (str == type.ID)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
                return;

            PoolItem poolItem = FindPoolItem(type);

            // if this type is not yet in pool, create it
            if (poolItem == null)
            {
                poolItem = container.Create<PoolItem>(type);
                items.Add(poolItem);
            }

            // increase count
            poolItem.Count++;
        }
        #endregion

        #region public get item methods
        /// <summary>
        /// Get best available weapon.
        /// </summary>
        /// <returns>weapon item or null</returns>
        public Item GetBestWeapon()
        {
            PoolItem item = FindPoolItem("item_loaded_rifle", "item_loaded_pistol", "item_pitchfork", "item_axe", "item_knife");
            if (item != null)
                return item.Type.Generate();
            return null;
        }

        /// <summary>
        /// Get best available food production item.
        /// </summary>
        /// <param name="availableProducts">filter productions with list of products</param>
        /// <returns>production item or null</returns>
        public Item GetBestTrap(params string[] availableProducts)
        {
            string[] foodProductions = new string[] { "item_trap", "item_snake_trap", "item_rat_trap", "item_knife" };

            // look in order trap, snake trap, rat trap, knife
            foreach (string foodProduction in foodProductions)
            {
                PoolItem item = FindPoolItem(foodProduction);
                if (item != null)
                {
                    // check if production is available
                    foreach (string product in availableProducts)
                    {
                        // if available, then return current trap
                        if (item.Type.Production.Produce.ID == product)
                            return item.Type.Generate();
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get least necessary gas protection.
        /// </summary>
        /// <returns>gas protection item or null</returns>
        public Item GetGasMask()
        {
            PoolItem item = FindPoolItem("item_gas_mask", "item_paper_helmet", "item_protection_suit");
            if (item != null)
                return item.Type.Generate();
            return null;
        }

        // 
        /// <summary>
        /// Get least necessary radiation protection.
        /// </summary>
        /// <returns>radiation protection item or null</returns>
        public Item GetProtectionSuit()
        {
            PoolItem item = FindPoolItem("item_protection_suit", "item_paper_helmet");
            if (item != null)
                return item.Type.Generate();
            return null;
        }

        /// <summary>
        /// Check if weapon is available.
        /// </summary>
        /// <returns>true if weapon is available</returns>
        public bool HasWeapon()
        {
            PoolItem item = FindPoolItem("item_knife", "item_axe", "item_pitchfork", "item_loaded_pistol", "item_loaded_rifle");
            if (item != null)
                return true;
            return false;
        }

        /// <summary>
        /// Check if food production item is available.
        /// </summary>
        /// <param name="availableProducts">filter productions with list of products</param>
        /// <returns>true if food production item is available</returns>
        public bool HasTrap(params string[] availableProducts)
        {
            string[] foodProductions = new string[] { "item_trap", "item_snake_trap", "item_rat_trap", "item_knife" };

            // look in order trap, snake trap, rat trap, knife
            foreach (string foodProduction in foodProductions)
            {
                PoolItem item = FindPoolItem(foodProduction);
                if (item != null)
                {
                    // check if production is available
                    foreach (string product in availableProducts)
                    {
                        // if available, then return current trap
                        if (item.Type.Production.Produce.ID == product)
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Check if gas mask is available. 
        /// </summary>
        /// <returns>true if gas mask is available</returns>
        public bool HasGasMask()
        {
            PoolItem item = FindPoolItem("item_gas_mask", "item_protection_suit", "item_paper_helmet");
            if (item != null)
                return true;
            return false;
        }

        /// <summary>
        /// Check if radiation protection is available.
        /// </summary>
        /// <returns>true if radiation protection is available</returns>
        public bool HasProtectionSuit()
        {
            PoolItem item = FindPoolItem("item_protection_suit", "item_paper_helmet");
            if (item != null)
                return true;
            return false;
        }
        #endregion

        #region protected helper
        /// <summary>
        /// Find first PoolItem with item type.
        /// </summary>
        /// <param name="types">list of types</param>
        /// <returns>first found PoolItem</returns>
        protected PoolItem FindPoolItem(params ItemType[] types)
        {
            foreach (ItemType type in types)
            {
                // look for pool items with the same ItemType
                foreach (PoolItem item in items)
                {
                    if (item.Type == type)
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Find first PoolItem with item type.
        /// </summary>
        /// <param name="types">list of types</param>
        /// <returns>first found PoolItem</returns>
        protected PoolItem FindPoolItem(params string[] types)
        {
            foreach (string type in types)
            {
                // look for pool items with the same ItemType.ID
                foreach (PoolItem item in items)
                {
                    if (item.Type.ID == type)
                    {
                        return item;
                    }
                }
            }

            return null;
        }
        #endregion
    }
}
