using System;
using System.Linq;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Interaction;
using Burntime.Framework.States;

namespace Burntime.Remaster.AI
{
    /// <summary>
    /// item pool for AI
    /// </summary>
    [Serializable]
    class AiItemPool : StateObject, Constructions.IConstructionMaterialReserve
    {
        static readonly string[] ItemTypeFilter = new string[] { 
            "item_knife", "item_axe", "item_pitchfork", "item_loaded_rifle", 
            "item_loaded_pistol", "item_gas_mask", "item_protection_suit", 
            "item_paper_helmet", "item_rat_trap", "item_snake_trap", "item_trap" };

        // Inputs for recipes the strategic AI currently knows how to use. Construction
        // reserves are capped at one per type; additional copies remain physical goods.
        static readonly string[] ConstructionMaterialFilter = new string[] {
            "item_wire", "item_woodpile", "item_screws", "item_spring", "item_tin",
            "item_broken_pump", "item_spare_parts", "item_rags", "item_hose", "item_iron_pipe",
            "item_unloaded_rifle", "item_unloaded_pistol", "item_ammunition",
            "item_gas_mask", "item_gloves", "item_protective_overall", "item_boots" };

        internal static System.Collections.Generic.IEnumerable<string> ConstructionMaterialIds =>
            ConstructionMaterialFilter;

        internal static bool Accepts(ItemType type)
        {
            return Array.Exists(ItemTypeFilter, id => id == type.ID) || IsWaterContainer(type);
        }

        internal static bool IsWaterContainer(ItemType type) =>
            (type.Empty != null && type.WaterValue > 0) ||
            (type.Full != null && type.Full.WaterValue > 0);

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
            if (!Accepts(item.Type))
                return;

            InsertUnchecked(item.Type);
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
            if (!Accepts(type))
                return;

            InsertUnchecked(type);
        }

        /// <summary>
        /// Move the first copy of a supported construction component into the
        /// empire-wide reserve. Further copies must remain in real inventories.
        /// </summary>
        public bool TryReserveConstructionMaterial(Item item)
        {
            if (!IsConstructionMaterial(item.Type.ID) || GetConstructionMaterialCount(item.Type.ID) >= 1)
                return false;

            InsertUnchecked(item.Type);
            return true;
        }

        private void InsertUnchecked(ItemType type)
        {
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
        public Item GetBestWeapon(int minimumDamage = 0, bool allowProductionTool = true)
        {
            PoolItem item = FindBestWeaponPoolItem(minimumDamage, allowProductionTool);
            if (item != null)
                return Take(item);
            return null;
        }

        /// <summary>
        /// Get the largest available water container, preferring a full variant.
        /// </summary>
        public Item GetBestWaterContainer()
        {
            PoolItem item = FindPoolItem(
                "item_full_wineskin", "item_empty_wineskin",
                "item_full_canteen", "item_empty_canteen",
                "item_water_bottle", "item_bottle");
            return item != null ? Take(item) : null;
        }

        public bool HasWaterContainer()
        {
            return FindPoolItem(
                "item_full_wineskin", "item_empty_wineskin",
                "item_full_canteen", "item_empty_canteen",
                "item_water_bottle", "item_bottle") != null;
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
                            return Take(item);
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
                return Take(item);
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
                return Take(item);
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

        public bool HasBetterWeapon(int damage, bool allowProductionTool = true)
        {
            return FindBestWeaponPoolItem(damage, allowProductionTool) != null;
        }

        public int ProductionToolCount => items
            .Where(item => item.Count > 0 && item.Type.Production != null)
            .Sum(item => item.Count);

        public int ProtectionCount => items
            .Where(item => item.Count > 0 && IsHazardProtection(item.Type))
            .Sum(item => item.Count);

        public int WaterContainerCount => items
            .Where(item => item.Count > 0 && IsWaterContainer(item.Type))
            .Sum(item => item.Count);

        public int BestWaterContainerCapacity => items
            .Where(item => item.Count > 0 && IsWaterContainer(item.Type))
            .Select(item => WaterContainerCapacity(item.Type))
            .DefaultIfEmpty(0)
            .Max();

        internal System.Collections.Generic.IEnumerable<(ItemType Type, int Count)> GetContents() => items
            .Where(item => item.Count > 0)
            .Select(item => (item.Type, item.Count));

        internal static bool IsConstructionMaterial(string itemId) =>
            Array.Exists(ConstructionMaterialFilter, id => id == itemId);

        public int GetConstructionMaterialCount(string itemId) =>
            FindPoolItem(itemId)?.Count ?? 0;

        public bool TryConsumeConstructionMaterial(string itemId)
        {
            PoolItem item = FindPoolItem(itemId);
            if (item == null || !IsConstructionMaterial(itemId))
                return false;
            item.Count--;
            return true;
        }

        public bool HasHigherValueTrap(float productionValue, params string[] availableProducts) => items
            .Any(item => item.Count > 0 && item.Type.Production != null &&
                availableProducts.Contains(item.Type.Production.Produce.ID) &&
                item.Type.Production.Produce.TradeValue > productionValue);

        public Item GetBestGeneralProtection()
        {
            PoolItem item = FindPoolItem("item_protection_suit", "item_gas_mask", "item_paper_helmet");
            return item != null ? Take(item) : null;
        }

        public Item TakeLeastProtection()
        {
            PoolItem item = FindPoolItem("item_paper_helmet", "item_gas_mask", "item_protection_suit");
            return item != null ? Take(item) : null;
        }

        public Item TakeLeastProductionTool()
        {
            PoolItem item = items
                .Where(candidate => candidate.Count > 0 && candidate.Type.Production != null)
                .OrderBy(candidate => candidate.Type.Production.Produce.TradeValue)
                .ThenBy(candidate => candidate.Type.DamageValue)
                .FirstOrDefault();
            return item != null ? Take(item) : null;
        }

        internal static bool IsHazardProtection(ItemType type) =>
            type.GetProtection("gas") != null || type.GetProtection("radiation") != null;

        public Item TakeLeastWaterContainer()
        {
            PoolItem item = items
                .Where(candidate => candidate.Count > 0 && IsWaterContainer(candidate.Type))
                .OrderBy(candidate => WaterContainerCapacity(candidate.Type))
                .ThenBy(candidate => candidate.Type.WaterValue)
                .FirstOrDefault();
            return item != null ? Take(item) : null;
        }

        internal static int WaterContainerCapacity(ItemType type) =>
            type.WaterValue > 0 ? type.WaterValue : type.Full?.WaterValue ?? 0;

        PoolItem FindBestWeaponPoolItem(int minimumDamage, bool allowProductionTool)
        {
            string[] weaponOrder = { "item_loaded_rifle", "item_loaded_pistol", "item_pitchfork", "item_axe", "item_knife" };
            return weaponOrder
                .Select(id => items.FirstOrDefault(item => item.Type.ID == id && item.Count > 0))
                .FirstOrDefault(item => item != null && item.Type.DamageValue > minimumDamage &&
                    (allowProductionTool || item.Type.Production == null));
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
                    if (item.Type == type && item.Count > 0)
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
                    if (item.Type.ID == type && item.Count > 0)
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        private static Item Take(PoolItem item)
        {
            item.Count--;
            return item.Type.Generate();
        }
        #endregion
    }
}
