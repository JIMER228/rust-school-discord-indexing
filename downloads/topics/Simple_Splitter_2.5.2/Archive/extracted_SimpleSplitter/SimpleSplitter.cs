using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VLB;

//SimpleSplitter created with PluginMerge v(1.0.6.0) by MJSU @ https://github.com/dassjosh/Plugin.Merge
namespace Oxide.Plugins
{
    [Info("Simple Splitter", "Shady14u", "2.5.2")]
    [Description("Splits up resources in furnaces automatically")]
    public partial class SimpleSplitter : RustPlugin
    {
        #region 1. SimpleSplitter.cs
        private readonly Dictionary<ulong, SmeltingController> _smeltingControllers = new();
        [PluginReference] private Plugin XRadiationOre;
        
        private void AutoAddFuel(PlayerInventory playerInventory, BaseOven oven)
        {
            var neededFuel = (int) Math.Ceiling(GetFuelNeeded(oven));
            var existingFuel = oven.inventory.GetAmount(oven.fuelType.itemid, false);
            neededFuel -= existingFuel;
            var playerFuel = playerInventory.FindItemsByItemID(oven.fuelType.itemid);
            if (neededFuel <= 0 || !playerFuel.Any()) return;
            
            foreach (var fuelItem in playerFuel)
            {
                if (oven.fuelType != fuelItem.info) continue;
                
                var toTake = Math.Min(neededFuel, oven.fuelType.stackable);
                
                if (toTake > fuelItem.amount) toTake = fuelItem.amount;
                
                if (toTake <= 0) break;
                
                neededFuel -= toTake;
                
                if (toTake >= fuelItem.amount)
                {
                    fuelItem.MoveToContainer(oven.inventory);
                }
                else
                {
                    var splitItem = fuelItem.SplitItem(toTake);
                    if (!splitItem.MoveToContainer(oven.inventory)) // Break if oven is full
                    break;
                }
                
                if (neededFuel <= 0)
                break;
            }
            
            if (oven.fuelSlots <= 1) return;
            
            var newAmt = oven.inventory.GetAmount(oven.fuelType.itemid, false);
            var slot0 = oven.inventory.GetSlot(0);
            var slot1 = oven.inventory.GetSlot(1);
            
            if (slot0 != null)
            {
                slot0.amount = newAmt / 2;
            }
            else
            {
                var item = ItemManager.CreateByItemID(oven.fuelType.itemid, newAmt / 2);
                item.MoveToContainer(oven.inventory, 0);
                slot0 = item;
            }
            
            if (slot1 != null)
            {
                slot1.amount = newAmt - slot0.amount;
            }
            else
            {
                var item = ItemManager.CreateByItemID(oven.fuelType.itemid, newAmt - slot0.amount);
                item.MoveToContainer(oven.inventory, 1);
            }
        }
        
        public float GetFuelNeeded(BaseOven oven)
        {
            var maxStack = 0;
            for (var i = 0; i < oven.inputSlots; i++)
            {
                if (oven.inventory.GetSlot(oven.fuelSlots + i)?.amount > maxStack)
                {
                    maxStack = oven.inventory.GetSlot(oven.fuelSlots + i).amount;
                }
            }
            
            var component = GetSmeltingController(oven);
            if (component == null || component.maxSmeltPerTick <= 0)
            {
                return 0;
            }
            
            var numTicks = (maxStack / component.maxSmeltPerTick) + 1;
            var fuel = numTicks * component.fuelPerTick;
            return fuel;
        }
        
        
        private int GetMaxSmelt(BaseOven oven)
        {
            var maxSmelt = _config.MaxSmeltPerTick;
            if (_config.OvenSmeltingOverrides != null &&
            _config.OvenSmeltingOverrides.TryGetValue(oven.ShortPrefabName, out var smeltingOverride))
            {
                maxSmelt = smeltingOverride;
            }
            
            return maxSmelt;
        }
        
        private SmeltingController GetSmeltingController(BaseNetworkable oven)
        {
            return _smeltingControllers.TryGetValue(oven.net.ID.Value, out var smeltingController)
            ? smeltingController
            : oven.GetComponent<SmeltingController>();
        }
        
        private int GetStackSize(Item item)
        {
            return Interface.CallHook("OnMaxStackable", item) is int stackSize ? stackSize : item.info.stackable;
        }
        
        private MoveResult MoveSplitItem(Item item, Composter composter)
        {
            if (item.info.GetComponent<ItemModCompostable>() == null)
            {
                return MoveResult.SlotsFilled;
            }
            
            var container = composter.inventory;
            var totalMoved = 0;
            var lastSlotIdx = 0;
            var existingAmount = container.itemList.Where(x=>x.info.itemid == item.info.itemid && x.skin == item.skin).Sum(x=>x.amount);
            var remaining = existingAmount + item.amount;
            var numUsedSlots = 0;
            var openSlot = -1;
            
            for (var i = composter.inventorySlots - 1; i > -1; --i)
            {
                var inputItem = composter.inventory.GetSlot(i);
                if (inputItem != null && inputItem.info == item.info && inputItem.skin == item.skin)
                {
                    composter.inventory.Remove(inputItem);
                    inputItem = null;
                }
                
                if ((inputItem == null || inputItem.info.shortname == "fertilizer") && openSlot == -1)
                {
                    openSlot = i;
                }
                else
                {
                    if (inputItem == null)
                    {
                        numUsedSlots += 1;
                    }
                }
            }
            
            if (numUsedSlots <= 0)
            {
                return MoveResult.SlotsFilled;
            }
            
            var stackedMax = Math.Min(remaining, numUsedSlots * GetStackSize(item));
            var totalStackSize = stackedMax / numUsedSlots ;
            
            if(totalStackSize < remaining) totalStackSize++;
            totalStackSize = Math.Min(totalStackSize, GetStackSize(item));
            
            if (totalStackSize <= 0) totalStackSize = 0;
            
            for (var i = composter.inventorySlots - 1; i > -1; --i)
            {
                if (remaining <= 0 || i == openSlot)
                {
                    continue;
                }
                
                var existingItem = composter.inventory.GetSlot(i);
                if (existingItem != null && (existingItem.info != item.info || existingItem.skin == item.skin))
                {
                    continue;
                }
                
                var amtInSlot = Math.Min(remaining, totalStackSize);
                
                if (remaining < totalStackSize)
                {
                    amtInSlot = remaining;
                    remaining = 0;
                }
                
                if (amtInSlot > 0)
                {
                    var newItem = ItemManager.Create(item.info, amtInSlot, item.skin);
                    newItem.MoveToContainer(container, i);
                }
                
                lastSlotIdx = i;
                remaining -= amtInSlot;
                totalMoved += amtInSlot;
                composter.inventory.MarkDirty();
            }
            
            if (remaining > 0)
            {
                //Add to Oven if it can fit otherwise add to Inventory
                var lastItem = composter.inventory.GetSlot(lastSlotIdx);
                if (lastItem != null && lastItem.info == item.info && lastItem.skin == item.skin)
                {
                    var maxToAdd = Math.Min(remaining, GetStackSize(item) - lastItem.amount);
                    
                    remaining -= maxToAdd;
                    lastItem.amount += maxToAdd;
                    lastItem.GetRootContainer()?.MarkDirty();
                    totalMoved += maxToAdd;
                }
            }
            
            if (totalMoved >= item.amount && remaining <= 0)
            {
                item.Remove();
                item.GetRootContainer()?.MarkDirty();
                return MoveResult.Ok;
            }
            
            if (remaining > 0)
            {
                item.amount = remaining;
            }
            
            item.GetRootContainer()?.MarkDirty();
            return MoveResult.SlotsFilled;
        }
        
        private MoveResult MoveSplitItem(Item item, BaseOven oven)
        {
            var container = oven.inventory;
            var totalMoved = 0;
            var lastSlotIdx = 0;
            var existingAmount = container.itemList.Where(slotItem => slotItem.info.shortname == item.info.shortname &&
            slotItem.skin == item.skin)
            .Sum(slotItem => slotItem.amount);
            
            var numOreSlots = 0;
            for (var i = 0; i < oven.inputSlots; i++)
            {
                var inputItem = oven.inventory.GetSlot(oven.fuelSlots + i);
                if (inputItem != null && (inputItem.info.shortname != item.info.shortname || inputItem.skin != item.skin)) continue;
                
                numOreSlots += 1;
            }
            
            var maxCanHold = numOreSlots * GetStackSize(item);
            if (numOreSlots <= 0 || existingAmount >= maxCanHold)
            {
                return MoveResult.SlotsFilled;
            }
            
            var remaining = existingAmount + item.amount;
            var stackedMax = Math.Min(remaining, numOreSlots * GetStackSize(item));
            var totalStackSize = stackedMax / numOreSlots;
            
            if (totalStackSize <= 0) totalStackSize = 1;
            
            for (var i = 0; i < oven.inputSlots; ++i)
            {
                if (remaining <= 0)
                {
                    continue;
                }
                
                var existingItem = oven.inventory.GetSlot(oven.fuelSlots + i);
                if (existingItem != null && (existingItem.info.shortname != item.info.shortname || existingItem.skin != item.skin))
                {
                    continue;
                }
                var amtInSlot = Math.Min(remaining, totalStackSize);
                
                if (remaining <= totalStackSize)
                {
                    amtInSlot = remaining;
                    remaining = 0;
                }
                
                if (existingItem != null)
                {
                    existingItem.amount = amtInSlot;
                }
                else
                {
                    var newItem = ItemManager.Create(item.info, amtInSlot, item.skin);
                    newItem.MoveToContainer(container, oven.fuelSlots + i);
                }
                
                lastSlotIdx = i;
                remaining -= amtInSlot;
                totalMoved += amtInSlot;
                oven.inventory.MarkDirty();
            }
            
            if (remaining > 0)
            {
                //Add to Oven if it can fit otherwise add to Inventory
                var lastItem = oven.inventory.GetSlot(oven.fuelSlots + lastSlotIdx);
                if (lastItem != null && lastItem.info == item.info)
                {
                    var maxToAdd = Math.Min(remaining, GetStackSize(item) - lastItem.amount);
                    
                    remaining -= maxToAdd;
                    lastItem.amount += maxToAdd;
                    lastItem.GetRootContainer()?.MarkDirty();
                    totalMoved += maxToAdd;
                }
            }
            
            if (totalMoved >= item.amount && remaining <= 0)
            {
                item.Remove();
                item.GetRootContainer()?.MarkDirty();
                return MoveResult.Ok;
            }
            
            if (remaining > 0)
            {
                item.amount = remaining;
            }
            
            item.GetRootContainer()?.MarkDirty();
            return MoveResult.SlotsFilled;
        }
        #endregion

        #region 2. SimpleSplitter.Config.cs
        private PluginConfig _config;
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null) LoadDefaultConfig();
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                LoadDefaultConfig();
            }
        }
        
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating default config for SimpleSplitter.");
            _config = new PluginConfig();
        }
        
        protected override void SaveConfig() => Config.WriteObject(_config);
        
        private class PluginConfig
        {
            [JsonProperty("Compatible Ovens")] public string[] CompatibleOvens =
            {
                "bbq.deployed",
                "bbq.static",
                "campfire",
                "campfire.static",
                "cursedcauldron.deployed",
                "fireplace.deployed",
                "furnace",
                "furnace.large",
                "furnace.static",
                "furnace_static",
                "hobobarrel_static",
                "hobobarrel.deployed",
                "refinery_small_deployed",
                "small_refinery_static",
                "skull_fire_pit",
                "electricfurnace.deployed",
                "legacy_furnace"
            };
            
            [JsonProperty("Oven charcoal Overrides - Higher the number the less often charcoal is made")]
            public Dictionary<string, int> OvenCharcoalOverrides = new Dictionary<string, int>
            {
                {"campfire", 4},
                {"bbq.deployed", 2}
            };
            
            [JsonProperty("Oven Smelting Overrides")]
            public Dictionary<string, int> OvenSmeltingOverrides = new Dictionary<string, int>
            {
                {"furnace", 1},
                {"refinery_small_deployed", 2}
            };
            
            [JsonProperty("Auto Start Ovens on Deploy or restarts")]
            public bool AutoStartOvens { get; set; } = false;
            
            [JsonProperty("Fuel * This = Additional Charcoal per Tick")]
            public float CharcoalMultiplier { get; set; } = 1;
            
            [JsonProperty("Delays Smelting by a Factor 0-25 (Used to reduce speeds below vanilla)")]
            public int DelayFactor { get; set; } = 2;
            
            [JsonProperty("Disable Multi Fuel Speed Bonus")]
            public bool DisableMultiFuelBonus { get; set; } = false;
            
            [JsonProperty("Enable Charcoal Creation in Electric Furnace")]
            public bool EnableCharcoalInElectricFurnace { get; set; } = false;
            
            [JsonProperty("Enable Composters - Splitting items into Composters")]
            public bool EnableComposters { get; set; } = true;
            
            [JsonProperty("Enable QuickSmelt - Smelting of multiple slots at once")]
            public bool EnableQuickSmelt { get; set; } = true;
            
            [JsonProperty("Fuel Burned Per Tick")] public int FuelPerTick { get; set; } = 1;
            
            [JsonProperty("Ignore Stack Limit In Smelted Output Slots")]
            public bool IgnoreStackLimitInSmeltedSlots { get; set; } = false;
            
            [JsonProperty("Max Smelt slots at once")]
            public int MaxSmeltingSlots { get; set; } = 5;
            
            [JsonProperty("Max Ore Smelted Per Tick")]
            public int MaxSmeltPerTick { get; set; } = 3;
            
            [JsonProperty("Require Ore In Electric Oven For Charcoal")]
            public bool RequireOreInElectricForCharcoal { get; set; }
            
            [JsonProperty("Number of seconds to delay after reload (5 default)")]
            public float RestartDelay { get; set; } = 5f;
            
            [JsonProperty("Smelting Speed (Time Between Ticks 0.5 Default)")]
            public float SmeltingSpeed { get; set; } = 0.5f;
        }
        #endregion

        #region 3. SimpleSplitter.Classes.cs
        public enum MoveResult
        {
            Ok,
            SlotsFilled,
            NotEnoughSlots
        }
        
        public class BaseItem
        {
            [JsonProperty("Display Name")] public string DisplayName;
            
            [JsonProperty("Short Name")] public string ShortName;
            
            [JsonProperty("Skin ID")] public ulong SkinId;
            
            public Item ToItem(int amount = 1)
            {
                var item = ItemManager.CreateByName(ShortName, amount, SkinId);
                if (item != null && string.IsNullOrEmpty(DisplayName) == false)
                item.name = DisplayName;
                
                return item;
            }
        }
        #endregion

        #region 4. SimpleSplitter.Controllers.cs
        public partial class SmeltingController : FacepunchBehaviour
        {
            public int delayFactor;
            public int maxSmeltPerTick;
            public int fuelPerTick;
            public float smeltingSpeed;
            public BaseOven oven;
            public int maxSmeltingSlots;
            public float charcoalMultiplier;
            public bool disableMultiFuelBonus;
            public WaterPurifier waterPurifier;
            private int _charcoalLoop;
            private int _currentLoop;
            public SimpleSplitter Instance;
            
            
            public void StartCooking()
            {
                if(oven == null) return;
                _charcoalLoop = 0;
                _currentLoop = 0;
                oven.StopCooking();
                if(HasFuel(oven)) oven.SetFlag(BaseEntity.Flags.On, true);
                if (oven.PrefabName.Contains("campfire"))
                {
                    waterPurifier = oven.GetComponentInChildren<WaterPurifier>();
                    waterPurifier?.InvokeRepeating(Purify, smeltingSpeed, smeltingSpeed);
                }
                
                oven.InvokeRepeating(Cook, smeltingSpeed, smeltingSpeed);
            }
            
            private void Purify()
            {
                if (waterPurifier == null) return;
                waterPurifier.Cook(smeltingSpeed);
            }
            
            private void Cook()
            {
                if (oven == null) return;
                
                if (!oven.HasFlag(BaseEntity.Flags.On))
                {
                    oven.StopCooking();
                    return;
                }
                
                try
                {
                    if (delayFactor > 0 && _currentLoop < delayFactor)
                    {
                        _currentLoop += 1;
                        return;
                    }
                    
                    _currentLoop = 0;
                    var fastCook = true;
                    var hasFuel = HasFuel(oven);
                    var canCook = false;
                    
                    for (var i = 0; i < oven.fuelSlots; i++)
                    {
                        var fuel = oven.inventory.GetSlot(i);
                        if (fuel == null || fuel.amount <= 0)
                        {
                            fastCook = false;
                        }
                    }
                    
                    if (oven.fuelSlots <= 1 || oven.PrefabName.Contains("electric")) fastCook = false;
                    if (disableMultiFuelBonus) fastCook = false;
                    
                    if (!hasFuel)
                    {
                        StopCooking();
                        return;
                    }
                    
                    for (var i = 0; i < oven.inputSlots; i++)
                    {
                        var needsOverflow = false;
                        var slotItem = oven.inventory.GetSlot(oven.fuelSlots + i);
                        if (slotItem == null) continue;
                        
                        var cookedItem = slotItem.info.GetComponent<ItemModCookable>();
                        if (cookedItem == null) continue;
                        var outputItem = oven.inventory.GetSlot(oven.fuelSlots + oven.inputSlots + i + 1);
                        var overFlowSlot = 0;
                        Item newCookedItem = null;
                        if (Instance.XRadiationOre != null && Instance.XRadiationOre.IsLoaded)
                        {
                            
                            var args = Instance.XRadiationOre.Call<string[]>("SpecialSmeltingItem", slotItem,oven);
                            if (args != null && args.Length > 2)
                            {
                                newCookedItem = ItemManager.CreateByName(args[0], 1, ulong.Parse(args[1]));
                                if (newCookedItem != null && args.Length > 2)
                                newCookedItem.name = args[2];
                            }
                        }
                        
                        newCookedItem ??= ItemManager.Create(cookedItem.becomeOnCooked);
                        
                        if (outputItem != null && (outputItem.info != newCookedItem.info ||
                        (outputItem.amount >=
                        (outputItem.info.stackable) &&
                        !Instance._config.IgnoreStackLimitInSmeltedSlots))
                        || i >= maxSmeltingSlots)
                        {
                            if (oven.outputSlots == 10)
                            {
                                outputItem = oven.inventory.GetSlot(13 + i);
                                if (outputItem != null && (outputItem.info != newCookedItem.info ||
                                (outputItem.amount >= outputItem.info.stackable &&
                                !Instance._config.IgnoreStackLimitInSmeltedSlots))
                                || i >= maxSmeltingSlots - 1)
                                {
                                    slotItem.SetFlag(global::Item.Flag.OnFire, false);
                                    slotItem.MarkDirty();
                                    continue;
                                }
                                
                                overFlowSlot = 5;
                            }
                            else if (oven.inputSlots == 1 && oven.outputSlots == 3)
                            {
                                outputItem = oven.inventory.GetSlot(4);
                                if (outputItem != null && (outputItem.info != newCookedItem.info ||
                                (outputItem.amount >= outputItem.info.stackable &&
                                !Instance._config.IgnoreStackLimitInSmeltedSlots))
                                || i >= maxSmeltingSlots - 1)
                                {
                                    slotItem.SetFlag(global::Item.Flag.OnFire, false);
                                    slotItem.MarkDirty();
                                    continue;
                                }
                                
                                overFlowSlot = 1;
                            }
                            else if (oven.inputSlots == 2 && oven.outputSlots == 3)
                            {
                                outputItem = oven.inventory.GetSlot(2);
                                if (outputItem != null && (outputItem.info != newCookedItem.info ||
                                (outputItem.amount >= outputItem.info.stackable &&
                                !Instance._config.IgnoreStackLimitInSmeltedSlots))
                                || i >= maxSmeltingSlots - 1)
                                {
                                    slotItem.SetFlag(global::Item.Flag.OnFire, false);
                                    slotItem.MarkDirty();
                                    continue;
                                }
                                needsOverflow = true;
                                
                            }
                            else
                            {
                                slotItem.SetFlag(global::Item.Flag.OnFire, false);
                                slotItem.MarkDirty();
                                continue;
                            }
                        }
                        
                        
                        var limit = maxSmeltPerTick;
                        if (outputItem != null && !Instance._config.IgnoreStackLimitInSmeltedSlots)
                        {
                            limit = outputItem.info.stackable - outputItem.amount;
                        }
                        
                        canCook = true;
                        slotItem.SetFlag(global::Item.Flag.OnFire, true);
                        slotItem.MarkDirty();
                        
                        var amtToSmelt = Math.Min(slotItem.amount, maxSmeltPerTick);
                        amtToSmelt = Math.Min(amtToSmelt, limit);
                        if (fastCook) amtToSmelt *= 2;
                        amtToSmelt = Math.Min(slotItem.amount, amtToSmelt);
                        if (amtToSmelt <= 0) continue;
                        
                        var outputSlot = oven.fuelSlots + oven.inputSlots + i + 1 + overFlowSlot;
                        if (needsOverflow)
                        {
                            outputSlot = 2;
                        }
                        
                        var outItem = oven.inventory.GetSlot(outputSlot);
                        var crudeMultiple = slotItem.info.shortname == "crude.oil"
                        ? amtToSmelt * 3
                        : amtToSmelt;
                        
                        newCookedItem.amount = crudeMultiple;
                        
                        if (outItem == null)
                        {
                            newCookedItem.MoveToContainer(oven.inventory, outputSlot);
                            slotItem.amount -= amtToSmelt;
                        }
                        else
                        {
                            if (outItem.skin == newCookedItem.skin && outItem.info.shortname == newCookedItem.info.shortname)
                            {
                                outItem.amount += crudeMultiple;
                                slotItem.amount -= amtToSmelt;
                            }
                        }
                        
                        if (slotItem.amount <= 0)
                        {
                            slotItem.Remove();
                        }
                    }
                    
                    //Return Here if Electric Oven and No Charcoal to be made
                    if (oven.PrefabName.Contains("electric"))
                    {
                        if (!Instance._config.EnableCharcoalInElectricFurnace) return;
                        if (!canCook && Instance._config.RequireOreInElectricForCharcoal) return;
                    }
                    
                    var charcoalAmt = int.Parse($"{charcoalMultiplier * fuelPerTick}");
                    if (fastCook) charcoalAmt *= 2;
                    if (charcoalAmt <= 0) return;
                    
                    var makeCharcoal = true;
                    var maxCharcoal = false;
                    if (Instance._config.OvenCharcoalOverrides.TryGetValue(oven.ShortPrefabName, out var charcoalDelay))
                    {
                        if (_charcoalLoop < charcoalDelay)
                        {
                            _charcoalLoop += 1;
                            makeCharcoal = false;
                        }
                    }
                    
                    if (oven.allowByproductCreation == false)
                    {
                        makeCharcoal = false;
                    }
                    
                    if (makeCharcoal)
                    {
                        if (oven.outputSlots > 1)
                        {
                            var outputItem = oven.inventory.GetSlot(oven.fuelSlots + oven.inputSlots);
                            
                            if (outputItem != null &&
                            oven.fuelType.GetComponent<ItemModBurnable>().byproductItem == outputItem.info)
                            {
                                if (outputItem.amount >= outputItem.info.stackable &&
                                !Instance._config.IgnoreStackLimitInSmeltedSlots)
                                {
                                    maxCharcoal = true;
                                }
                                else
                                {
                                    if (!Instance._config.IgnoreStackLimitInSmeltedSlots)
                                    {
                                        charcoalAmt = Math.Min(charcoalAmt,
                                        outputItem.info.stackable - outputItem.amount);
                                    }
                                }
                                
                                if (!maxCharcoal)
                                {
                                    outputItem.amount += charcoalAmt;
                                    canCook = true;
                                    _charcoalLoop = 0;
                                }
                            }
                            
                            if (outputItem == null)
                            {
                                var newItem = ItemManager.Create(
                                oven.fuelType.GetComponent<ItemModBurnable>().byproductItem,
                                charcoalAmt);
                                if (newItem != null)
                                {
                                    if (newItem.MoveToContainer(oven.inventory, oven.fuelSlots + oven.inputSlots))
                                    {
                                        canCook = true;
                                    }
                                }
                            }
                        }
                    }
                    
                    var fuelTaken = false;
                    
                    for (var i = 0; i < oven.fuelSlots; i++)
                    {
                        var item = oven.inventory.GetSlot(i);
                        if (item == null) continue;
                        
                        if (fuelTaken && !fastCook) continue;
                        item.amount -= fuelPerTick;
                        item.SetFlag(global::Item.Flag.OnFire, true);
                        item.MarkDirty();
                        fuelTaken = true;
                        if (item.amount <= 0) item.Remove();
                    }
                    
                    
                    if (makeCharcoal && !canCook || maxCharcoal)
                    {
                        StopCooking();
                    }
                }
                catch (Exception ex)
                {
                    Instance.Puts(ex.Message);
                }
            }
            
            private bool HasFuel(BaseOven baseOven)
            {
                for (var i = 0; i < baseOven.fuelSlots; i++)
                {
                    var fuel = baseOven.inventory.GetSlot(i);
                    if (fuel != null && fuel.amount > 0)
                    {
                        return true;
                    }
                }
                
                if (!baseOven.PrefabName.Contains("electricfurnace.deployed")) return false;
                
                var electricOven = baseOven.GetOrAddComponent<ElectricOven>();
                if (electricOven == null) return false;
                var hasFuel = electricOven.GetComponentsInChildren<IOEntity>()
                .FirstOrDefault(x => x.ioType == IOEntity.IOType.Electric)?.IsPowered() ?? false;
                var parent = electricOven.GetParentEntity();
                if (parent != null && parent.ShortPrefabName == "tugboat")
                {
                    hasFuel = parent.HasFlag(BaseEntity.Flags.On);
                }
                return hasFuel;
            }
            
            public void StopCooking()
            {
                _charcoalLoop = 0;
                _currentLoop = 0;
                oven.CancelInvoke(Cook);
                oven.SetFlag(BaseEntity.Flags.Reserved1, false);
                oven.StopCooking();
                waterPurifier?.CancelInvoke(Purify);
            }
        }
        #endregion

        #region 5. SimpleSplitter.Hooks.cs
        [HookMethod("AutoAddFuel")]
        public void AutoAddFuel(BasePlayer player, BaseOven oven)
        {
            AutoAddFuel(player.inventory, oven);
        }
        
        private object CanMoveItem(Item item, PlayerInventory inventory, ItemContainerId targetContainer, int targetSlot)
        {
            if (item == null || inventory == null || item.info==null)
            {
                return null;
            }
            
            if (item.info.shortname.Contains("cooked")) return null;
            
            var player = inventory.GetComponent<BasePlayer>();
            if (player == null)
            {
                return null;
            }
            
            var entityOwner = item.GetEntityOwner();
            if (!targetContainer.IsValid)
            {
                if (entityOwner == inventory.baseEntity)
                {
                    if (inventory.loot?.containers?.Count > 0)
                    {
                        targetContainer = inventory.loot.containers[0].uid;
                    }
                }
            }
            
            var container = inventory.FindContainer(targetContainer);
            var originalContainer = item.GetRootContainer();
            
            if (container == null || originalContainer == null)
            {
                return null;
            }
            
            Func<object> splitFunc = () =>
            {
                if (!HasPermission(player))
                return null;
                
                if (container == item.GetRootContainer())
                return null;
                
                if (container.entityOwner as Composter != null && _config.EnableComposters)
                {
                    return MoveSplitItem(item, (Composter) container.entityOwner) == MoveResult.Ok;
                }
                
                var oven = container.entityOwner as BaseOven;
                var cookable = item.info.GetComponent<ItemModCookable>();
                
                if (oven == null || cookable == null || !_config.CompatibleOvens.Contains(oven.ShortPrefabName))
                return null;
                if (cookable.lowTemp > oven.cookingTemperature || cookable.highTemp < oven.cookingTemperature)
                return null;
                
                MoveSplitItem(item, oven);
                return true;
            };
            
            var returnValue = splitFunc();
            
            if (HasPermission(player))
            {
                var oven = container.entityOwner as BaseOven ?? item.GetRootContainer().entityOwner as BaseOven;
                
                if (oven == null || !_config.CompatibleOvens.Contains(oven.ShortPrefabName) ||
                oven.ShortPrefabName.Contains("electric")) return returnValue;
                
                AutoAddFuel(inventory, oven);
            }
            
            return returnValue;
        }
        
        object MoveSplitItemApi(Item item, BaseOven oven)
        {
            return MoveSplitItem(item, oven).ToString();
        }
        
        private void OnEntitySpawned(BaseOven oven)
        {
            if (!_config.EnableQuickSmelt) return;
            if (oven == null || !_config.CompatibleOvens.Contains(oven.ShortPrefabName)) return;
            
            var component = oven.GetOrAddComponent<SmeltingController>();
            if (component == null) return;
            
            _smeltingControllers[oven.net.ID.Value] = component;
            component.maxSmeltPerTick = GetMaxSmelt(oven);
            component.delayFactor = _config.DelayFactor;
            component.charcoalMultiplier = _config.CharcoalMultiplier;
            component.disableMultiFuelBonus = _config.DisableMultiFuelBonus;
            component.Instance = this;
            component.smeltingSpeed = _config.SmeltingSpeed;
            component.fuelPerTick = _config.FuelPerTick;
            component.maxSmeltingSlots = _config.MaxSmeltingSlots;
            component.oven = oven;
            if (oven.IsOn() || _config.AutoStartOvens)
            {
                timer.Once(1f, () => { component.StartCooking(); });
            }
            else
            {
                oven.StopCooking();
            }
        }
        
        object OnOvenCook(BaseOven oven, Item item)
        {
            if (!_config.EnableQuickSmelt || !oven.allowByproductCreation ||
            !_config.CompatibleOvens.Contains(oven.ShortPrefabName)) return null;
            var component = GetSmeltingController(oven);
            if (component == null)
            {
                OnEntitySpawned(oven);
                return null;
            }
            
            component.StartCooking();
            return false;
        }
        
        private void OnOvenStarted(BaseOven oven)
        {
            if (oven == null || !oven.allowByproductCreation || !_config.CompatibleOvens.Contains(oven.ShortPrefabName)) return;
            var component = GetSmeltingController(oven);
            if (component == null) return ;
            component.StartCooking();
            
        }
        
        private object OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (oven == null || !oven.allowByproductCreation ||
            !_config.CompatibleOvens.Contains(oven.ShortPrefabName)) return null;
            var component = GetSmeltingController(oven);
            if (component == null)
            {
                return null;
            }
            
            if (oven.IsOn())
            {
                component.StopCooking();
            }
            else
            {
                component.StartCooking();
            }
            
            return false;
        }
        
        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermUse, this);
            
            if (!_config.EnableQuickSmelt) return;
            
            var ovens = BaseNetworkable.serverEntities.OfType<BaseOven>();
            
            timer.In(_config.RestartDelay, () =>
            {
                foreach (var oven in ovens)
                {
                    OnEntitySpawned(oven);
                }
            });
        }
        
        private void Unload()
        {
            var ovens = BaseNetworkable.serverEntities.OfType<BaseOven>();
            
            foreach (var oven in ovens)
            {
                foreach (var component in oven.GetComponents<Component>())
                {
                    var comp = component.GetType().ToString();
                    if (!comp.Contains("SmeltingController"))
                    {
                        continue;
                    }
                    
                    var smelter = (SmeltingController) component;
                    
                    if (oven.IsOn())
                    {
                        smelter.StopCooking();
                        oven.StartCooking();
                    }
                    
                    UnityEngine.Object.Destroy(smelter);
                }
            }
        }
        #endregion

        #region 6. SimpleSplitter.Permissions.cs
        private const string PermUse = "simplesplitter.use";
        
        #region
        
        private bool HasPermission(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, PermUse);
        }
        
        #endregion
        #endregion

    }

}
