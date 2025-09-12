using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Furnace Splitter Ex", "Menevt", "2.2.0")]
    [Description("Splits up resources in furnaces automatically")]
    public class FurnaceSplitterEx : RustPlugin
    {
        private class OvenSlot
        {
            /// <summary>The item in this slot. May be null.</summary>
            public Item Item;

            /// <summary>The slot position</summary>
            public int? Position;

            /// <summary>The slot's index in the itemList list.</summary>
            public int Index;

            /// <summary>How much should be added/removed from stack</summary>
            public int DeltaAmount;
        }

        public class OvenInfo
        {
            public float ETA;
            public float FuelNeeded;
        }

        public enum MoveResult
        {
            Ok,
            SlotsFilled,
            NotEnoughSlots
        }

        private const string permUse = "furnacesplitterex.use";
        private readonly string[] compatibleOvens =
        {
            "bbq.deployed",
            "campfire",
            "fireplace.deployed",
            "furnace",
            "furnace.large",
            "hobobarrel_static",
            "refinery_small_deployed",
            "skull_fire_pit"
        };

        private Dictionary<string, int> initialStackOptions = new Dictionary<string, int>()
            {
                {"furnace", 3},
                {"bbq.deployed", 9},
                {"campfire", 2},
                {"fireplace.deployed", 2},
                {"furnace.large", 15},
                {"hobobarrel_static", 2},
                {"refinery_small_deployed", 3},
                {"skull_fire_pit", 2}
            };

        private void Loaded()
        {
            permission.RegisterPermission(permUse, this);
        }

        private bool IsSlotCompatible(Item item, BaseOven oven, ItemDefinition itemDefinition)
        {
            ItemModCookable cookable = item.info.GetComponent<ItemModCookable>();

            if (item.amount < item.info.stackable && item.info == itemDefinition)
                return true;

            if (oven.allowByproductCreation && oven.fuelType.GetComponent<ItemModBurnable>().byproductItem == item.info)
                return true;

            if (cookable == null || cookable.becomeOnCooked == itemDefinition)
                return true;

            if (CanCook(cookable, oven))
                return true;

            return false;
        }

        private object CanMoveItem(Item item, PlayerInventory inventory, uint targetContainer, int targetSlot)
        {
            BasePlayer player = inventory.GetComponent<BasePlayer>();
            if (player == null)
                return null;

            ItemContainer container = inventory.FindContainer(targetContainer);
            ItemContainer originalContainer = item.GetRootContainer();

            Func<object> splitFunc = () =>
            {
                if (player == null || !HasPermission(player))
                    return null;


                if (container == null || container == item.GetRootContainer())
                    return null;

                BaseOven oven = container.entityOwner as BaseOven;
                ItemModCookable cookable = item.info.GetComponent<ItemModCookable>();

                if (oven == null || cookable == null)
                    return null;

                int totalSlots = 2 + (oven.allowByproductCreation ? 1 : 0);

                if (initialStackOptions.ContainsKey(oven.ShortPrefabName))
                {
                    totalSlots = initialStackOptions[oven.ShortPrefabName];
                }

                if (cookable.lowTemp > oven.cookingTemperature || cookable.highTemp < oven.cookingTemperature)
                    return null;

                MoveSplitItem(item, oven, totalSlots);
                return true;
            };

            object returnValue = splitFunc();

            if (HasPermission(player))
            {
                BaseOven oven = container?.entityOwner as BaseOven ?? item.GetRootContainer().entityOwner as BaseOven;

                if (oven != null && compatibleOvens.Contains(oven.ShortPrefabName))
                {
                    if (returnValue is bool && (bool)returnValue)
                        AutoAddFuel(inventory, oven);
                }
            }

            return returnValue;
        }

        private MoveResult MoveSplitItem(Item item, BaseOven oven, int totalSlots)
        {
            ItemContainer container = oven.inventory;
            int invalidItemsCount = container.itemList.Count(slotItem => !IsSlotCompatible(slotItem, oven, item.info));
            int numOreSlots = Math.Min(container.capacity - invalidItemsCount, totalSlots);
            int totalMoved = 0;
            int totalAmount = Math.Min(item.amount + container.itemList.Where(slotItem => slotItem.info == item.info).Take(numOreSlots).Sum(slotItem => slotItem.amount), item.info.stackable * numOreSlots);

            if (numOreSlots <= 0)
            {
                return MoveResult.NotEnoughSlots;
            }

            //Puts("---------------------------");

            int totalStackSize = Math.Min(totalAmount / numOreSlots, item.info.stackable);
            int remaining = totalAmount - totalAmount / numOreSlots * numOreSlots;

            List<int> addedSlots = new List<int>();

            //Puts("total: {0}, remaining: {1}, totalStackSize: {2}", totalAmount, remaining, totalStackSize);

            List<OvenSlot> ovenSlots = new List<OvenSlot>();

            for (int i = 0; i < numOreSlots; ++i)
            {
                Item existingItem;
                int slot = FindMatchingSlotIndex(container, out existingItem, item.info, addedSlots);

                if (slot == -1) // full
                {
                    return MoveResult.NotEnoughSlots;
                }

                addedSlots.Add(slot);

                OvenSlot ovenSlot = new OvenSlot
                {
                    Position = existingItem?.position,
                    Index = slot,
                    Item = existingItem
                };

                int currentAmount = existingItem?.amount ?? 0;
                int missingAmount = totalStackSize - currentAmount + (i < remaining ? 1 : 0);
                ovenSlot.DeltaAmount = missingAmount;

                //Puts("[{0}] current: {1}, delta: {2}, total: {3}", slot, currentAmount, ovenSlot.DeltaAmount, currentAmount + missingAmount);

                if (currentAmount + missingAmount <= 0)
                    continue;

                ovenSlots.Add(ovenSlot);
            }

            foreach (OvenSlot slot in ovenSlots)
            {
                if (slot.Item == null)
                {
                    Item newItem = ItemManager.Create(item.info, slot.DeltaAmount, item.skin);
                    slot.Item = newItem;
                    newItem.MoveToContainer(container, slot.Position ?? slot.Index);
                }
                else
                {
                    slot.Item.amount += slot.DeltaAmount;
                }

                totalMoved += slot.DeltaAmount;
            }

            container.MarkDirty();

            if (totalMoved >= item.amount)
            {
                item.Remove();
                item.GetRootContainer()?.MarkDirty();
                return MoveResult.Ok;
            }
            else
            {
                item.amount -= totalMoved;
                item.GetRootContainer()?.MarkDirty();
                return MoveResult.SlotsFilled;
            }
        }

        private void AutoAddFuel(PlayerInventory playerInventory, BaseOven oven)
        {
            int neededFuel = (int)Math.Ceiling(GetOvenInfo(oven).FuelNeeded);
            neededFuel -= oven.inventory.GetAmount(oven.fuelType.itemid, false);
            var playerFuel = playerInventory.FindItemIDs(oven.fuelType.itemid);

            if (neededFuel <= 0 || playerFuel.Count <= 0)
                return;

            foreach (Item fuelItem in playerFuel)
            {
                if (oven.inventory.CanAcceptItem(fuelItem, -1) != ItemContainer.CanAcceptResult.CanAccept)
                    break;

                Item largestFuelStack = oven.inventory.itemList.Where(item => item.info == oven.fuelType).OrderByDescending(item => item.amount).FirstOrDefault();
                int toTake = Math.Min(neededFuel, oven.fuelType.stackable - (largestFuelStack?.amount ?? 0));

                if (toTake > fuelItem.amount)
                    toTake = fuelItem.amount;

                if (toTake <= 0)
                    break;

                neededFuel -= toTake;

                if (toTake >= fuelItem.amount)
                {
                    fuelItem.MoveToContainer(oven.inventory);
                }
                else
                {
                    Item splitItem = fuelItem.SplitItem(toTake);
                    if (!splitItem.MoveToContainer(oven.inventory)) // Break if oven is full
                        break;
                }

                if (neededFuel <= 0)
                    break;
            }
        }

        private int FindMatchingSlotIndex(ItemContainer container, out Item existingItem, ItemDefinition itemType, List<int> indexBlacklist)
        {
            existingItem = null;
            int firstIndex = -1;
            Dictionary<int, Item> existingItems = new Dictionary<int, Item>();

            for (int i = 0; i < container.capacity; ++i)
            {
                if (indexBlacklist.Contains(i))
                    continue;

                Item itemSlot = container.GetSlot(i);
                if (itemSlot == null || itemType != null && itemSlot.info == itemType)
                {
                    if (itemSlot != null)
                        existingItems.Add(i, itemSlot);

                    if (firstIndex == -1)
                    {
                        existingItem = itemSlot;
                        firstIndex = i;
                    }
                }
            }

            if (existingItems.Count <= 0 && firstIndex != -1)
            {
                return firstIndex;
            }
            else if (existingItems.Count > 0)
            {
                var largestStackItem = existingItems.OrderByDescending(kv => kv.Value.amount).First();
                existingItem = largestStackItem.Value;
                return largestStackItem.Key;
            }

            existingItem = null;
            return -1;
        }

        public OvenInfo GetOvenInfo(BaseOven oven)
        {
            OvenInfo result = new OvenInfo();
            var smeltTimes = GetSmeltTimes(oven);

            if (smeltTimes.Count > 0)
            {
                var longestStack = smeltTimes.OrderByDescending(kv => kv.Value).First();
                float fuelUnits = oven.fuelType.GetComponent<ItemModBurnable>().fuelAmount;
                float neededFuel = (float)Math.Ceiling(longestStack.Value * (oven.cookingTemperature / 200.0f) / fuelUnits);

                result.FuelNeeded = neededFuel;
                result.ETA = longestStack.Value;
            }

            return result;
        }

        private Dictionary<ItemDefinition, float> GetSmeltTimes(BaseOven oven)
        {
            ItemContainer container = oven.inventory;
            var cookables = container.itemList.Where(item =>
            {
                ItemModCookable cookable = item.info.GetComponent<ItemModCookable>();
                return cookable != null && CanCook(cookable, oven);
            }).ToList();

            if (cookables.Count == 0)
                return new Dictionary<ItemDefinition, float>();

            var distinctCookables = cookables.GroupBy(item => item.info, item => item).ToList();
            Dictionary<ItemDefinition, int> amounts = new Dictionary<ItemDefinition, int>();

            foreach (var group in distinctCookables)
            {
                int biggestAmount = group.Max(item => item.amount);
                amounts.Add(group.Key, biggestAmount);
            }

            var smeltTimes = amounts.ToDictionary(kv => kv.Key, kv => GetSmeltTime(kv.Key.GetComponent<ItemModCookable>(), kv.Value));
            return smeltTimes;
        }

        private float GetSmeltTime(ItemModCookable cookable, int amount)
        {
            float smeltTime = cookable.cookTime * amount;
            return smeltTime;
        }

        private bool CanCook(ItemModCookable cookable, BaseOven oven)
        {
            return oven.cookingTemperature >= cookable.lowTemp && oven.cookingTemperature <= cookable.highTemp;
        }

        private bool HasPermission(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, permUse);
        }

        #region Exposed plugin methods

        [HookMethod("MoveSplitItem")]
        public string Hook_MoveSplitItem(Item item, BaseOven oven, int totalSlots)
        {
            MoveResult result = MoveSplitItem(item, oven, totalSlots);
            return result.ToString();
        }

        [HookMethod("GetOvenInfo")]
        public JObject Hook_GetOvenInfo(BaseOven oven)
        {
            OvenInfo ovenInfo = GetOvenInfo(oven);
            return JObject.FromObject(ovenInfo);
        }

        #endregion Exposed plugin methods
    }
}