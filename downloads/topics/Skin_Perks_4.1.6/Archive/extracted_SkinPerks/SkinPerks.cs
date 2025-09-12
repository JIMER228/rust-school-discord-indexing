using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Skin Perks", "supreme", "4.1.6")]
    [Description("Allows players to gain perks based on equipped items")]
    public class SkinPerks : RustPlugin
    {
        #region Class Fields
        
        [PluginReference]
        Plugin RaidableBases, AbandonedBases;

        private static SkinPerks _pluginInstance;
        private PluginData _pluginData;
        private PluginConfig _pluginConfig;

        private readonly Hash<ulong, PlayerPerks> _playerPerks = new Hash<ulong, PlayerPerks>();
        private readonly Hash<ulong, ItemContainerId> _cachedPlayerBox = new Hash<ulong, ItemContainerId>();
        private readonly Hash<ulong, SkinPerksEditor> _skinPerksEditors = new Hash<ulong, SkinPerksEditor>();
        private readonly Hash<ulong, string> _cachedWeapon = new Hash<ulong, string>();

        private const string UsePermission = "skinperks.use";
        private const string WoodBoxPrefab = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
        private const string WhiteHex = "#ffffff";
        private readonly object _true = true;
        
        private readonly UiPosition _uiPositionBackground = new UiPosition(0.65f, 0.322f, 0.946f, 0.74f);
        private readonly UiPosition _uiPositionTitle = new UiPosition(0f, 0.8f, 1f, 1f);
        private readonly UiPosition _uiPositionSave = new UiPosition(0.4f, 0.02f, 0.6f, 0.11f);

        private enum GatherMode
        {
            All,
            Tree,
            Ore,
            Pickup,
            Crop,
            Null
        }

        private enum DamageMode
        {
            All,
            Construction,
            Player,
            Npc,
            Animal,
            Resource,
            Trap,
            Vehicle,
            Null
        }

        private enum DurabilityMode
        {
            All,
            Clothing,
            Weapon,
            Tool,
            Null
        }

        private enum Perks
        {
            Gather,
            Durability,
            DamageOut,
            DamageIn,
            Dodge,
            Upgrade,
            Build,
            Repair,
            Magazine
        }

        #endregion

        #region Hooks
        
        private void Init()
        {
            _pluginData = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            permission.RegisterPermission(UsePermission, this);
        }
        
        private void OnServerInitialized()
        {
            _pluginInstance = this;
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }

        private void Unload()
        {
            SaveData();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DestroyAllUis(player);
                OnPlayerLootEnd(player.inventory.loot);
            }

            _pluginInstance = null;
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            RecalculatePlayerPerks(player);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            _playerPerks.Remove(player.userID);
        }

        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (oldItem != null && _pluginData.Skins.ContainsKey(oldItem.skin) || newItem != null && _pluginData.Skins.ContainsKey(newItem.skin))
            {
                RecalculatePlayerPerks(player);
            }
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container.entityOwner && container.entityOwner is BasePlayer)
            {
                BasePlayer player = container.entityOwner as BasePlayer;
                if (_cachedPlayerBox[player.userID] == container.uid && !container.IsEmpty())
                {
                    DisplayUi(player, item);
                }
            }
            
            BasePlayer ownerPlayer = container.GetOwnerPlayer();
            if (!ownerPlayer)
            {
                return;
            }

            if (ownerPlayer.inventory.containerBelt.uid != container.uid && ownerPlayer.inventory.containerWear.uid != container.uid)
            {
                return;
            }

            if (!_pluginData.Skins.ContainsKey(item.skin))
            {
                return;
            }

            RecalculatePlayerPerks(ownerPlayer);
        }

        private void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (container.entityOwner && container.entityOwner is BasePlayer)
            {
                BasePlayer player = container.entityOwner as BasePlayer;
                if (_cachedPlayerBox[player.userID] == container.uid)
                {
                    DestroyAllUis(player);
                }
            }
            
            OnItemAddedToContainer(container, item);
        }
        
        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            PlayerPerks perks = _playerPerks[player.userID];
            if (perks == null)
            {
                return;
            }

            ResourceEntity resource = dispenser.GetComponent<ResourceEntity>();
            if (!resource)
            {
                return;
            }

            double multiplier = 1;
            if (resource is OreResourceEntity)
            {
                multiplier = GetGatherPerk(perks.Gather, GatherMode.Ore);
            }
            else if (resource is TreeEntity)
            {
                multiplier = GetGatherPerk(perks.Gather, GatherMode.Tree);
            }

            if (multiplier != 1)
            {
                UpdateItemAmount(item, multiplier);
            }
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            OnDispenserGather(dispenser, player, item);
        }

        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            PlayerPerks perks = _playerPerks[player.userID];
            if (perks == null)
            {
                return;
            }

            double multiplier = GetGatherPerk(perks.Gather, GatherMode.Pickup);
            if (multiplier != 1)
            {
                UpdateItemAmount(item, multiplier);
            }
        }

        private void OnCropGather(GrowableEntity plant, Item item, BasePlayer player)
        {
            PlayerPerks perks = _playerPerks[player.userID];
            if (perks == null)
            {
                return;
            }

            double multiplier = GetGatherPerk(perks.Gather, GatherMode.Crop);
            if (multiplier != 1)
            {
                UpdateItemAmount(item, multiplier);
            }
        }
        
        private void OnLoseCondition(Item item, ref float amount)
        {
            BasePlayer player = item.GetOwnerPlayer();
            if (!player)
            {
                return;
            }

            PlayerPerks perks = _playerPerks[player.userID];
            if (perks == null)
            {
                return;
            }

            double multiplier = 1;
            switch (item.info.category)
            {
                case ItemCategory.Attire:
                    multiplier = GetDurabilityPerk(perks.Durability, DurabilityMode.Clothing);
                    break;

                case ItemCategory.Tool:
                    multiplier = GetDurabilityPerk(perks.Durability, DurabilityMode.Tool);
                    break;

                case ItemCategory.Weapon:
                    multiplier = GetDurabilityPerk(perks.Durability, DurabilityMode.Weapon);
                    break;
            }

            if (multiplier == 1)
            {
                return;
            }

            amount *= (float)multiplier;
        }

        private object OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (info == null)
            {
                return null;
            }

            DamageMode? attackerDamage = GetDamageMode(info.Initiator);
            DamageMode? victimDamage = GetDamageMode(entity);
            if (attackerDamage == null || victimDamage == null)
            {
                return null;
            }

            double outputDamage = 1;
            if (attackerDamage == DamageMode.Player)
            {
                if (_pluginConfig.BlockRaidableBases && RaidableTerritory(entity))
                {
                    return null;
                }

                if (_pluginConfig.BlockAbandonedBases && AbandonedTerritory(entity))
                {
                    return null;
                }

                BasePlayer attacker = info.InitiatorPlayer;
                PlayerPerks attackerPerks = _playerPerks[attacker.userID];
                if (attackerPerks != null)
                {
                    outputDamage = GetDamagePerk(attackerPerks.DamageOut, victimDamage.Value);
                }
            }

            double inputDamage = 1;
            if (victimDamage == DamageMode.Player && entity is BasePlayer victim)
            {
                PlayerPerks victimPerks = _playerPerks[victim.userID];
                if (victimPerks != null)
                {
                    double dodgeChance = (float)GetDodgePerk(victimPerks.Dodge, attackerDamage.Value);
                    float dodge = Random.Range(0.0f, 1.0f);
                    if (dodge < dodgeChance)
                    {
                        return _true;
                    }
                    
                    inputDamage = GetProtectionPerk(victimPerks.DamageIn, attackerDamage.Value);
                }
            }

            float damageMultiplier = (float)(outputDamage * inputDamage);
            if (damageMultiplier != 1)
            {
                if (victimDamage == DamageMode.Resource)
                {
                    info.gatherScale *= damageMultiplier;
                }
                else
                {
                    BasePlayer player = info.InitiatorPlayer;
                    for (int index = 0; index < info.damageTypes.types.Length; ++index)
                    {
                        DamageType damageType = (DamageType) index;
                        if (damageType == DamageType.Explosion)
                        {
                            string cachedWeapon = _cachedWeapon[player.userID];
                            if (string.IsNullOrEmpty(cachedWeapon) || !cachedWeapon.Contains(player.GetActiveItem().info.shortname))
                            {
                                return null;
                            }
                        }
                        
                        info.damageTypes.Scale(damageType, damageMultiplier);
                    }
                }
            }

            return null;
        }
        
        private void OnEntitySpawned(TimedExplosive explosive)
        {
            BasePlayer player = explosive.creatorEntity as BasePlayer;
            if (!player)
            {
                return;
            }

            Item activeItem = player.GetActiveItem();
            if (activeItem == null)
            {
                return;
            }
            
            _cachedWeapon[player.userID] = activeItem.info.shortname;
        }

        private bool? CanAffordUpgrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
        {
            PlayerPerks perks = _playerPerks[player.userID];
            if (perks == null || perks.Upgrade == 1)
            {
                return null;
            }

            double upgrade = GetUpgradePerk(perks.Upgrade);

            foreach (ItemAmount amount in block.blockDefinition.grades[(int)grade].CostToBuild())
            {
                if (player.inventory.GetAmount(amount.itemid) >= amount.amount * upgrade)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private object OnPayForUpgrade(BasePlayer player, BuildingBlock block, ConstructionGrade gradeTarget)
        {
            PlayerPerks perks = _playerPerks[player.userID];
            if (perks == null || perks.Upgrade == 1)
            {
                return null;
            }

            double upgrade = GetUpgradePerk(perks.Upgrade);

            List<Item> items = new List<Item>();
            foreach (ItemAmount itemAmount in gradeTarget.CostToBuild())
            {
                player.inventory.Take(items, itemAmount.itemid, (int) (itemAmount.amount * upgrade));
                player.Command(string.Concat("note.inv ", itemAmount.itemid, " ", itemAmount.amount * upgrade * -1f), Array.Empty<object>());
            }

            foreach (Item item in items)
            {
                item.Remove();
            }

            return _true;
        }
        
        private object CanAffordToPlace(BasePlayer player, Planner planner, Construction construction)
        {
            PlayerPerks perks = _playerPerks[player.userID];
            if (perks == null || perks.Build == 1)
            {
                return null;
            }

            double build = GetBuildPerk(perks.Build);
            foreach (ItemAmount amount in construction.defaultGrade.CostToBuild())
            {
                if (player.inventory.GetAmount(amount.itemDef.itemid) >= amount.amount * build)
                {
                    continue;
                }

                return false;
            }

            return _true;
        }


        private object OnPayForPlacement(BasePlayer player, Planner planner, Construction construction)
        {
            if (planner.isTypeDeployable)
            {
                return null;
            }

            PlayerPerks perks = _playerPerks[player.userID];
            if (perks == null || perks.Build == 1)
            {
                return null;
            }

            double build = GetBuildPerk(perks.Build);

            List<Item> items = Pool.GetList<Item>();
            foreach (ItemAmount itemAmount in construction.defaultGrade.CostToBuild())
            {
                player.inventory.Take(items, itemAmount.itemDef.itemid, (int) (itemAmount.amount * build));
                player.Command("note.inv", itemAmount.itemDef.itemid, itemAmount.amount * build * -1f);
            }

            foreach (Item item in items)
            {
                item.Remove();
            }

            Pool.FreeList(ref items);
            return _true;
        }
        
        private void OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            PlayerPerks perks = _playerPerks[player.userID];
            if (perks == null || perks.Repair == 1)
            {
                return;
            }

            double repair = GetRepairPerk(perks.Repair);
            float beforeRepair = entity.health;

            NextTick(() =>
            {
                float afterRepair = entity.health;
                float repairedAmount = afterRepair - beforeRepair;
                if (repairedAmount == 0)
                {
                    return;
                }

                float repairFaction = repairedAmount / entity.MaxHealth();

                foreach (ItemAmount repairAmount in entity.RepairCost(repairFaction))
                {
                    int amount = (int) Math.Round(repairAmount.amount * repairFaction * repair,
                        MidpointRounding.AwayFromZero);
                    if (amount == 0)
                    {
                        continue;
                    }

                    player.GiveItem(ItemManager.CreateByItemID(repairAmount.itemid, amount));
                }
            });
        }

        private void OnItemRepair(BasePlayer player, Item item)
        {
            PlayerPerks perks = _playerPerks[player.userID];
            if (perks == null || perks.Repair == 1)
            {
                return;
            }

            ItemBlueprint bp = item.info.GetComponent<ItemBlueprint>();
            if (!bp)
            {
                return;
            }

            double repair = GetRepairPerk(perks.Repair);
            float repairFraction = RepairBench.RepairCostFraction(item);
            List<ItemAmount> list = Pool.GetList<ItemAmount>();
            RepairBench.GetRepairCostList(bp, list);

            foreach (ItemAmount repairAmount in list)
            {
                if (repairAmount.itemDef.category == ItemCategory.Component)
                {
                    continue;
                }

                int amount = (int) Math.Round(repairAmount.amount * repairFraction * repair, MidpointRounding.AwayFromZero);
                if (amount <= 0)
                {
                    continue;
                }

                player.GiveItem(ItemManager.CreateByItemID(repairAmount.itemid, amount));
            }

            Pool.FreeList(ref list);
        }

        private void OnPlayerLootEnd(PlayerLoot playerLoot)
        {
            BasePlayer player = playerLoot.GetComponent<BasePlayer>();
            if (!player)
            {
                return;
            }
            
            if (_cachedPlayerBox.ContainsKey(player.userID))
            {
                ItemContainer itemContainer = playerLoot.containers[0];
                if (itemContainer == null)
                {
                    return;
                }
                
                if (itemContainer.itemList.Count > 0)
                {
                    player.GiveItem(itemContainer.itemList[0]);
                }
                    
                playerLoot.entitySource.Kill();
                _cachedPlayerBox.Remove(player.userID);
            }
        }
        
        #endregion

        #region Perk Helper Methods

        private double GetGatherPerk(Hash<GatherMode, double> perks, GatherMode mode)
        {
            if (perks.ContainsKey(GatherMode.All) || perks.ContainsKey(mode))
            {
                return 1 + perks[mode] + perks[GatherMode.All];
            }

            return 1;
        }

        private double GetDurabilityPerk(Hash<DurabilityMode, double> perk, DurabilityMode mode)
        {
            bool containsMode = perk.ContainsKey(mode);
            bool containsAll = perk.ContainsKey(DurabilityMode.All);
            if (containsAll && containsMode)
            {
                double modeDur = perk[mode];
                double allDur = perk[DurabilityMode.All];
                return 1 - Math.Max(modeDur, allDur) - Math.Min(modeDur, allDur);
            }

            if (containsMode)
            {
                return 1 - perk[mode];
            }

            if (containsAll)
            {
                return 1 - perk[DurabilityMode.All];
            }

            return 1;
        }

        private double GetDamagePerk(Hash<DamageMode, double> perk, DamageMode mode)
        {
            if (perk.ContainsKey(mode) || perk.ContainsKey(DamageMode.All))
            {
                return 1 + perk[mode] + perk[DamageMode.All];
            }

            return 1;
        }

        private double GetProtectionPerk(Hash<DamageMode, double> perk, DamageMode mode)
        {
            if (perk.ContainsKey(mode) || perk.ContainsKey(DamageMode.All))
            {
                return 1 - perk[mode] - perk[DamageMode.All];
            }

            return 1;
        }

        private double GetDodgePerk(Hash<DamageMode, double> perk, DamageMode mode)
        {
            if (perk.ContainsKey(mode) || perk.ContainsKey(DamageMode.All))
            {
                return perk[mode] + perk[DamageMode.All];
            }
            
            return 0;
        }

        private double GetUpgradePerk(double perk)
        {
            return 1 - perk;
        }

        private double GetBuildPerk(double perk)
        {
            return 1 - perk;
        }

        private double GetRepairPerk(double perk)
        {
            return 1 - perk;
        }

        private double GetMagazinePerk(double perk)
        {
            return perk;
        }

        private void RecalculatePlayerPerks(BasePlayer player)
        {
            PlayerPerks perks = _playerPerks[player.userID];
            if (perks == null)
            {
                perks = new PlayerPerks();
                _playerPerks[player.userID] = perks;
            }
            else
            {
                perks.Reset();
            }

            Item held = player.GetActiveItem();
            if (held != null)
            {
                perks.AddPerks(_pluginData.Skins[held.skin]);
                int magazinePerk = (int) GetMagazinePerk(perks.Magazine);
                if (magazinePerk == 0)
                {
                    return;
                }
                
                BaseProjectile.Magazine magazine = held.GetHeldEntity().GetComponent<BaseProjectile>()?.primaryMagazine;
                if (magazine == null || magazine.capacity == magazinePerk)
                {
                    return;
                }
                
                magazine.capacity = magazinePerk;
            }

            for (int index = 0; index < player.inventory.containerWear.itemList.Count; index++)
            {
                Item item = player.inventory.containerWear.itemList[index];
                perks.AddPerks(_pluginData.Skins[item.skin]);
            }
        }
        
        private DamageMode? GetDamageMode(BaseEntity entity)
        {
            if (!entity)
            {
                return null;
            }
            
            switch (entity)
            {
                case BasePlayer player:
                {
                    return player.userID.IsSteamId() ? DamageMode.Player : DamageMode.Npc;
                }
                case BaseNpc:
                {
                    return DamageMode.Animal;
                }
                case ResourceEntity:
                {
                    return DamageMode.Resource;
                }
                case AutoTurret:
                case FlameTurret:
                case GunTrap:
                case BaseTrap:
                {
                    return DamageMode.Trap;
                }
                case Barricade:
                case BuildingBlock:
                {
                    return DamageMode.Construction;
                }
                case PatrolHelicopter:
                case CH47Helicopter:
                case BradleyAPC:
                {
                    return DamageMode.Npc;
                }
                case BaseVehicle:
                case HotAirBalloon:
                {
                    return DamageMode.Vehicle;
                }
            }

            return null;
        }

        #endregion

        #region Helper Methods
        
        private void UpdateItemAmount(Item item, double multiplier)
        {
            item.amount = (int) Math.Round(item.amount * multiplier, MidpointRounding.AwayFromZero);
        }

        private bool RaidableTerritory(BaseEntity entity) => IsLoaded(RaidableBases) && entity.OwnerID == 0 && RaidableBases.Call<bool>("EventTerritory", entity.transform.position);
        private bool AbandonedTerritory(BaseEntity entity) => IsLoaded(AbandonedBases) && AbandonedBases.Call<bool>("EventTerritory", entity.transform.position);

        private bool IsLoaded(Plugin plugin) => plugin != null && plugin.IsLoaded;

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _pluginData);

        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        private void DestroyAllUis(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "SkinPerksUi");
        }

        #endregion

        #region Editor Handler

        private void CreateEditor(BasePlayer player)
        {
            BoxStorage boxStorage = GameManager.server.CreateEntity(WoodBoxPrefab) as BoxStorage;
            if (!boxStorage)
            {
                return;
            }
            
            boxStorage.enableSaving = false;
            boxStorage.inventorySlots = 1;
            boxStorage.Spawn();
            boxStorage.inventory.entityOwner = player;
            
            player.inventory.loot.Clear();
            player.inventory.loot.PositionChecks = false;
            player.inventory.loot.entitySource = boxStorage;
            player.inventory.loot.itemSource = null;
            player.inventory.loot.AddContainer(boxStorage.inventory);
            player.inventory.loot.SendImmediate();
            
            _cachedPlayerBox.Add(player.userID, boxStorage.inventory.uid);
            
            timer.Once(0.1f, () =>
            {
                player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", boxStorage.panelName);
            });
        }

        #endregion

        #region Classes

        private class PluginConfig
        {
            [DefaultValue(true)]
            [JsonProperty(PropertyName = "Block Perks In Raidable Bases")]
            public bool BlockRaidableBases { get; set; }

            [DefaultValue(true)]
            [JsonProperty(PropertyName = "Block Perks In Abandoned Bases")]
            public bool BlockAbandonedBases { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig pluginConfig)
        {
            return pluginConfig;
        }

        private class PluginData
        {
            public Hash<ulong, SkinSettings> Skins { get; set; } = new Hash<ulong, SkinSettings>();
        }

        private class PlayerPerks
        {
            public Hash<GatherMode, double> Gather { get; set; } = new Hash<GatherMode, double>();
            public Hash<DurabilityMode, double> Durability { get; set; } = new Hash<DurabilityMode, double>();
            public Hash<DamageMode, double> DamageOut { get; set; } = new Hash<DamageMode, double>();
            public Hash<DamageMode, double> DamageIn { get; set; } = new Hash<DamageMode, double>();
            public Hash<DamageMode, double> Dodge { get; set; } = new Hash<DamageMode, double>();
            public double Upgrade { get; set; } = 1;
            public double Build { get; set; } = 1;
            public double Repair { get; set; } = 1;
            public double Magazine { get; set; }

            public void AddPerks(SkinSettings skinPerks)
            {
                if (skinPerks == null)
                {
                    return;
                }

                foreach (KeyValuePair<GatherMode, double> gather in skinPerks.Gather)
                {
                    Gather[gather.Key] += gather.Value;
                }

                foreach (KeyValuePair<DurabilityMode, double> durability in skinPerks.Durability)
                {
                    Durability[durability.Key] += durability.Value;
                }

                foreach (KeyValuePair<DamageMode, double> damageOut in skinPerks.DamageOut)
                {
                    DamageOut[damageOut.Key] += damageOut.Value;
                }

                foreach (KeyValuePair<DamageMode, double> damageIn in skinPerks.DamageIn)
                {
                    DamageIn[damageIn.Key] += damageIn.Value;
                }

                foreach (KeyValuePair<DamageMode, double> dodge in skinPerks.Dodge)
                {
                    Dodge[dodge.Key] += dodge.Value;
                }
                
                Build = skinPerks.Build;
                Upgrade = skinPerks.Upgrade;
                Repair = skinPerks.Repair;
                Magazine = skinPerks.Magazine;
            }

            public void Reset()
            {
                Gather.Clear();
                Durability.Clear();
                DamageOut.Clear();
                DamageIn.Clear();
                Dodge.Clear();
                Upgrade = 1;
                Build = 1;
                Repair = 1;
                Magazine = 0;
            }

            public StringBuilder PrintPerks()
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Player Perks: ");
                foreach (KeyValuePair<GatherMode, double> gatherPerk in Gather)
                {
                    if (gatherPerk.Value != 1)
                    {
                        sb.AppendLine($"Gather Perk: {gatherPerk.Key} > {1 + gatherPerk.Value}x [1x (Base) + {gatherPerk.Value}x (perk)]");
                    }
                }

                foreach (KeyValuePair<DurabilityMode, double> durabilityPerk in Durability)
                {
                    if (durabilityPerk.Value != 1)
                    {
                        sb.AppendLine($"Durability Perk: {durabilityPerk.Key} > {1 - durabilityPerk.Value}x [1x (Base) - {durabilityPerk.Value}x (perk)]");
                    }
                }

                foreach (KeyValuePair<DamageMode, double> damageOutPerk in DamageOut)
                {
                    if (damageOutPerk.Value != 1)
                    { 
                        sb.AppendLine($"Damage Perk: {damageOutPerk.Key} > {1 + damageOutPerk.Value}x [1x (Base) + {damageOutPerk.Value}x (perk)]");
                    }
                }

                foreach (KeyValuePair<DamageMode, double> damageInPerk in DamageIn)
                {
                    if (damageInPerk.Value != 1)
                    {
                        sb.AppendLine($"Protection Perk: {damageInPerk.Key} > {1 - damageInPerk.Value}x [1x (Base) - {damageInPerk.Value}x (perk)]");
                    }
                }

                foreach (KeyValuePair<DamageMode, double> dodgePerk in Dodge)
                {
                    if (dodgePerk.Value != 0)
                    {
                        sb.AppendLine($"Dodge Perk: {dodgePerk.Key} > {dodgePerk.Value} (Chance between 0.0 - 1.0)");
                    }
                }

                if (Upgrade != 1)
                {
                    sb.AppendLine($"Upgrade Perk: {_pluginInstance.GetUpgradePerk(Upgrade)}x [1x (Base) - {Upgrade}x (perk)]");
                }

                if (Build != 1)
                {
                    sb.AppendLine($"Build Perk: {_pluginInstance.GetBuildPerk(Build)}x [1x (Base) - {Build}x (perk)]");
                }

                if (Repair != 1)
                {
                    sb.AppendLine($"Repair Perk: {_pluginInstance.GetRepairPerk(Repair)}x [1x (Base) - {Repair}x (perk)]");
                }

                if (Magazine != 0)
                {
                    sb.AppendLine($"Magazine Perk: {_pluginInstance.GetMagazinePerk(Magazine)} [{Magazine} (perk)]");
                }
                
                return sb;
            }
        }

        private class SkinSettings : PlayerPerks
        {
            [JsonIgnore] 
            public string Name { get; set; }
            [JsonIgnore] 
            public string ShortName { get; set; }
            [JsonIgnore]
            public ulong SkinId { get; set; }
            [JsonIgnore]
            public string Permission { get; set; }
        }

        #endregion

        #region Chat Commands

        [ChatCommand("skinperk")]
        private void SkinPerksCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                player.ChatMessage("Usage: /skinperk info\n/skinperk create\n/skinperk editor");
            }
            else
            {
                switch (args[0].ToLower())
                {
                    case "editor":
                    {
                        if (!HasPermission(player, UsePermission))
                        {
                            player.ChatMessage("No permission");
                            return;
                        }
                        
                        CreateEditor(player);
                        break;
                    }
                    case "info":
                    {
                        player.ChatMessage($"Available perks and their values (Base value is 1):\n{_playerPerks[player.userID].PrintPerks()}");
                        break;
                    }
                    case "create":
                    {
                        if (!HasPermission(player, UsePermission))
                        {
                            player.ChatMessage("No permission");
                            return;
                        }

                        if (args.Length < 2)
                        {
                            player.ChatMessage("Incorrect syntax: Use /skinperk create shortname skinid");
                        }
                        else
                        {
                            Item createdItem = ItemManager.Create(ItemManager.FindItemDefinition(args[1]), 1, Convert.ToUInt64(args[2]));
                            if (createdItem == null)
                            {
                                player.ChatMessage("The item you are trying to create is null, Use /skinperk create shortname skinid");
                            }
                            else
                            {
                                player.GiveItem(createdItem);
                            }
                        }
                        
                        break;
                    }
                }
            }
        }

        #endregion

        #region UI Helpers

        private static class Ui
        {
            private static string UiPanel { get; set; }

            public static CuiElementContainer Container(string color, float alpha, float fadeIn, float fadeOut, UiPosition pos, bool useCursor, string panel, string parent = "Overlay")
            {
                UiPanel = panel;
                return new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = Color(color, alpha), FadeIn = fadeIn},
                            RectTransform = { AnchorMin = pos.GetMin(), AnchorMax = pos.GetMax() },
                            CursorEnabled = useCursor,
                            FadeOut = fadeOut
                        },
                        new CuiElement().Parent = parent,
                        panel
                    }
                };
            }

            public static CuiElementContainer ContainerBlurOverlay(string color, float alpha, float fadeIn, float fadeOut, UiPosition pos, bool useCursor, string panel, string parent = "Overlay")
            {
                UiPanel = panel;
                return new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = Color(color, alpha), FadeIn = fadeIn, Material = "assets/content/ui/uibackgroundblur.mat" },
                            RectTransform = { AnchorMin = pos.GetMin(), AnchorMax = pos.GetMax() },
                            CursorEnabled = useCursor,
                            FadeOut = fadeOut
                        },
                        new CuiElement().Parent = parent,
                        panel
                    }
                };
            }

            [Flags]
            public enum BorderEnum : byte
            {
                Top = 1,
                Left = 2,
                Bottom = 4,
                Right = 8,
                All = 15
            }

            public static void Outline(CuiElementContainer container, UiPosition pos, string color, float alpha, float fadeIn, float fadeOut, int size = 1, BorderEnum border = BorderEnum.All)
            {
                if ((border & BorderEnum.Top) == BorderEnum.Top)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = {AnchorMin = $"{pos.XMin} {pos.YMax}", AnchorMax = $"{pos.XMax} {pos.YMax}", OffsetMin = $"0 -{size}"},
                        Image = { Color = Color(color, alpha), FadeIn = fadeIn},
                        FadeOut = fadeOut
                    }, UiPanel);
                }
                
                if ((border & BorderEnum.Left) == BorderEnum.Left)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = {AnchorMin = $"{pos.XMin} {pos.YMin}", AnchorMax = $"{pos.XMin} {pos.YMax}", OffsetMin = $"-{size} -{size}", OffsetMax = $"1 {size}"},
                        Image = { Color = Color(color, alpha), FadeIn = fadeIn},
                        FadeOut = fadeOut
                    }, UiPanel);
                }
                
                if ((border & BorderEnum.Bottom) == BorderEnum.Bottom)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{pos.XMin} {pos.YMin}", AnchorMax = $"{pos.XMax} {pos.YMin}", OffsetMin = $"0 -{size}" },
                        Image = { Color = Color(color, alpha), FadeIn = fadeIn},
                        FadeOut = fadeOut
                    }, UiPanel);
                }

                if ((border & BorderEnum.Right) == BorderEnum.Right)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = {AnchorMin = $"{pos.XMax} {pos.YMin}", AnchorMax = $"{pos.XMax} {pos.YMax}", OffsetMin = $"0 -{size}", OffsetMax = $"{size * 2} {size}"},
                        Image = { Color = Color(color, alpha), FadeIn = fadeIn},
                        FadeOut = fadeOut
                    }, UiPanel);
                }
            }
            
            public static void TextOutline(CuiElementContainer container, string text, string tcolor, float talpha, string ocolor, float oalpha, int size, UiPosition pos, float fadeIn, float fadeOut, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiElement
                {
                    Components =
                    {
                        new CuiTextComponent { Color = Color(tcolor, talpha), FontSize = size, Align = align, Text = text, Font = "robotocondensed-regular.ttf", FadeIn = fadeIn},
                        new CuiOutlineComponent { Distance = "1.5 1.5", Color = Color(ocolor, oalpha) },
                        new CuiRectTransformComponent { AnchorMin = pos.GetMin(), AnchorMax = pos.GetMax() }
                    },
                    FadeOut = fadeOut,
                    Parent = UiPanel
                });
            }

            public static void Label(CuiElementContainer container, string text, float alpha, string color, float fadeIn, float fadeOut, int size, UiPosition pos, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text, Font = "robotocondensed-regular.ttf", Color = Color(color, alpha), FadeIn = fadeIn},
                    RectTransform = { AnchorMin = pos.GetMin(), AnchorMax = pos.GetMax() },
                    FadeOut = fadeOut
                },
                UiPanel);
            }
            
            public static void Panel(CuiElementContainer container, string color, float alpha, float fadeIn, float fadeOut, UiPosition pos, bool useCursor)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = Color(color, alpha), FadeIn = fadeIn },
                    RectTransform = { AnchorMin = pos.GetMin(), AnchorMax = pos.GetMax() },
                    CursorEnabled = useCursor,
                    FadeOut = fadeOut
                },
                    UiPanel);
            }

            public static void Button(CuiElementContainer container, string color, float alpha, string text, string tcolor, float talpha, float fadeIn, float fadeOut, int size, UiPosition pos, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = Color(color, alpha), Command = command, FadeIn = fadeIn },
                    RectTransform = { AnchorMin = pos.GetMin(), AnchorMax = pos.GetMax() },
                    Text = { Text = text, Color = Color(tcolor, talpha), FontSize = size, Font = "robotocondensed-regular.ttf", Align = align, FadeIn = fadeIn },
                    FadeOut = fadeOut
                },
                UiPanel);
            }
            
            public static void InputBox(CuiElementContainer container, string color, string text, int size, float fadeOut, int charsLimit, UiPosition pos, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = UiPanel,
                    FadeOut = fadeOut,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = text,
                            FontSize = size,
                            Color = color,
                            Command = command,
                            Align = align,
                            CharsLimit = charsLimit,
                            Font = "robotocondensed-regular.ttf"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin =  pos.GetMin(),
                            AnchorMax = pos.GetMax()
                        }
                    }
                });
            }
            
            public static void Image(CuiElementContainer container, string url, UiPosition pos, string color, float alpha, float fadeIn, float fadeOut)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = UiPanel,
                    Components =
                    {
                        new CuiRawImageComponent { Png = !url?.StartsWith("http") ?? false ? url : null, Url = url?.StartsWith("http") ?? false ? url : null, FadeIn = fadeIn, Color = Color(color, alpha) },
                        new CuiRectTransformComponent { AnchorMin = pos.GetMin(), AnchorMax = pos.GetMax() }
                    },
                    FadeOut = fadeOut
                });
            }

            private static string Color(string hexColor, float alpha)
            {
                hexColor = hexColor.TrimStart('#');
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{red / 255.0} {green / 255.0} {blue / 255.0} {alpha / 255}";
            }
        }

        private class UiPosition
        {
            public float XMin { get; set; }
            public float YMin { get; set; }
            public float XMax { get; set; }
            public float YMax { get; set; }
            private bool Validate { get; }

            public UiPosition(float xMin, float yMin, float xMax, float yMax, bool val = true)
            {
                XMin = xMin;
                YMin = yMin;
                XMax = xMax;
                YMax = yMax;
                Validate = val;
            }

            public string GetMin() => $"{XMin} {YMin}";
            public string GetMax() => $"{XMax} {YMax}";

            public void SetX(float xPos, float xMax)
            {
                XMin = xPos;
                XMax = xMax;
            }

            public void SetY(float yMin, float yMax)
            {
                YMin = yMin;
                YMax = yMax;
            }

            public void ModifyX(float delta)
            {
                XMin += delta;
                XMax += delta;
            }

            public void ModifyXPad(float padding)
            {
                float spacing = (XMax - XMin + Math.Abs(padding)) * (padding < 0 ? -1 : 1);
                XMin += spacing;
                XMax += spacing;
            }

            public UiPosition CopyX(float yPos, float yMax)
            {
                return new UiPosition(XMin, yPos, XMax, yMax);
            }

            public void ModifyY(float delta)
            {
                YMin += delta;
                YMax += delta;
            }

            public UiPosition CopyY(float xPos, float yMax)
            {
                return new UiPosition(xPos, YMin, yMax, YMax);
            }

            public void ModifyYPad(float padding)
            {
                float spacing = (YMax - YMin + Math.Abs(padding)) * (padding < 0 ? -1 : 1);
                YMin += spacing;
                YMax += spacing;
            }
        }

        #endregion
        
        #region UI Creation & Display

        private void DisplayUi(BasePlayer player, Item item)
        {
            CuiElementContainer container = Ui.ContainerBlurOverlay(WhiteHex, 8f, 0f, 0f, _uiPositionBackground, true, "SkinPerksUi");
            Ui.Label(container, $"<b><size=18>Skin Perks Editor</size></b>\n{item.info.displayName.english} ({item.skin})\n<b>Perks:</b>", 125f, WhiteHex, 0f, 0f, 16, _uiPositionTitle);
            if (item.skin != 0)
            {
                _skinPerksEditors[player.userID] = new SkinPerksEditor(item);
                UiPosition inputPosition = new UiPosition(0f, 0.7f, 0.326f, 0.78f);;
                switch (item.info.category)
                {
                    case ItemCategory.Weapon:
                    {
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Damage [All]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set DamageOut All");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Damage [Building]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set DamageOut Construction");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Damage [Player]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set DamageOut Player");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Damage [Npc]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set DamageOut Npc");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Damage [Animal]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set DamageOut Animal");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Damage [Resource]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set DamageOut Resource");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Damage [Trap]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set DamageOut Trap");
                        inputPosition.SetY(0.7f, 0.78f);
                        inputPosition.ModifyXPad(0.01f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Damage [Vehicle]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set DamageOut Vehicle");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Durability", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set Durability Weapon");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Magazine Capacity", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set Magazine");
                        break;
                    }
                    case ItemCategory.Attire:
                    {
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Protection [All]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set DamageIn All");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Protection [Player]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set DamageIn Player");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Protection [Npc]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set DamageIn Npc");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Protection [Animal]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set DamageIn Animal");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Protection [Trap]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set DamageIn Trap");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Durability", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set Durability Clothing");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Dodge [All]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set Dodge All");
                        inputPosition.SetY(0.7f, 0.78f);
                        inputPosition.ModifyXPad(0.01f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Dodge [Player]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set Dodge Player");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Dodge [Npc]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set Dodge Npc");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Dodge [Animal]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set Dodge Animal");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Dodge [Trap]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set Dodge Trap");
                        break;
                    }
                    case ItemCategory.Tool:
                    {
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Gather [All]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set Gather All");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Gather [Tree]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set Gather Tree");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Gather [Ore]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set Gather Ore");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Gather [Pickup]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set Gather Pickup");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Gather [Crop]", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set Gather Crop");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Durability", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set Durability Tool");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Upgrade", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set Upgrade");
                        inputPosition.SetY(0.7f, 0.78f);
                        inputPosition.ModifyXPad(0.01f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Build", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set Build");
                        inputPosition.ModifyY(-0.09f);
                        Ui.Panel(container, WhiteHex, 60f, 0f, 0f, inputPosition, true);
                        Ui.InputBox(container, WhiteHex, "Repair", 14, 0f, 32, inputPosition, $"{nameof(SkinPerksUiCommand)} set Repair");
                        break;
                    }
                }
            }
            
            Ui.Button(container, "#acfa58", 95f, "<b>SAVE</b>", WhiteHex, 55f, 0f, 0f, 16, _uiPositionSave, $"{nameof(SkinPerksUiCommand)} save");
            CuiHelper.DestroyUi(player, "SkinPerksUi");
            CuiHelper.AddUi(player, container);
        }

        private class SkinPerksEditor
        {
            private SkinSettings SkinSettings { get; set; }
            private ulong SkinId { get; set; }

            public SkinPerksEditor(Item item)
            {
                SkinSettings = new SkinSettings();
                SkinId = item.skin;
            }

            public void ModifySkinPerk(Perks skinPerk, GatherMode gatherMode = GatherMode.Null, DurabilityMode durabilityMode = DurabilityMode.Null, DamageMode damageMode = DamageMode.Null, double value = 1)  
            {
                switch (skinPerk)
                {
                    case Perks.Gather:
                    {
                        SkinSettings.Gather[gatherMode] = value;
                        return;
                    }
                    case Perks.Durability:
                    {
                        SkinSettings.Durability[durabilityMode] = value;
                        return;
                    }
                    case Perks.DamageOut:
                    {
                        SkinSettings.DamageOut[damageMode] = value;
                        return;
                    }
                    case Perks.DamageIn:
                    {
                        SkinSettings.DamageIn[damageMode] = value;
                        return;
                    }
                    case Perks.Dodge:
                    {
                        SkinSettings.Dodge[damageMode] = value;
                        return;
                    }
                    case Perks.Upgrade:
                    {
                        SkinSettings.Upgrade = value;
                        return;
                    }
                    case Perks.Build:
                    {
                        SkinSettings.Build = value;
                        return;
                    }
                    case Perks.Repair:
                    {
                        SkinSettings.Repair = value;
                        return;
                    }
                    case Perks.Magazine:
                    {
                        SkinSettings.Magazine = value;
                        return;
                    }
                }
            }

            public void SaveSkinPerks()
            {
                _pluginInstance._pluginData.Skins[SkinId] = SkinSettings;
                _pluginInstance.SaveData();
            }

            public void ResetSkinPerks()
            {
                SkinSettings = new SkinSettings();
            }
        }

        [ConsoleCommand(nameof(SkinPerksUiCommand))]
        private void SkinPerksUiCommand(ConsoleSystem.Arg arg)
        {
            string[] args = arg.Args;
            if (args.Length <= 0)
            {
                return;
            }

            SkinPerksEditor skinPerksEditor = _skinPerksEditors[arg.Player().userID];
            switch (args[0].ToLower())
            {
                case "save":
                {
                    skinPerksEditor.SaveSkinPerks();
                    return;
                }
                case "set":
                {
                    switch (args[1])
                    {
                        case "Gather":
                        {
                            GatherMode gatherMode;
                            if (!Enum.TryParse(args[2], out gatherMode))
                            {
                                return;
                            }
                            
                            double value;
                            if (!Double.TryParse(args[3], out value))
                            {
                                return;
                            }
                            
                            skinPerksEditor.ModifySkinPerk(Perks.Gather, gatherMode, value: value);
                            return;
                        }
                        case "Durability":
                        {
                            DurabilityMode durabilityMode;
                            if (!Enum.TryParse(args[2], out durabilityMode))
                            {
                                return;
                            }
                            
                            double value;
                            if (!Double.TryParse(args[3], out value))
                            {
                                return;
                            }
                            
                            skinPerksEditor.ModifySkinPerk(Perks.Durability, durabilityMode: durabilityMode, value: value);
                            return;
                        }
                        case "DamageOut":
                        {
                            DamageMode damageMode;
                            if (!Enum.TryParse(args[2], out damageMode))
                            {
                                return;
                            }
                            
                            double value;
                            if (!Double.TryParse(args[3], out value))
                            {
                                return;
                            }
                            
                            skinPerksEditor.ModifySkinPerk(Perks.DamageOut, damageMode: damageMode, value: value);
                            return;
                        }
                        case "DamageIn":
                        {
                            DamageMode damageMode;
                            if (!Enum.TryParse(args[2], out damageMode))
                            {
                                return;
                            }
                            
                            double value;
                            if (!Double.TryParse(args[3], out value))
                            {
                                return;
                            }
                            
                            skinPerksEditor.ModifySkinPerk(Perks.DamageIn, damageMode: damageMode, value: value);
                            return;
                        }
                        case "Dodge":
                        {
                            DamageMode damageMode;
                            if (!Enum.TryParse(args[2], out damageMode))
                            {
                                return;
                            }
                            
                            double value;
                            if (!Double.TryParse(args[3], out value))
                            {
                                return;
                            }
                            
                            skinPerksEditor.ModifySkinPerk(Perks.Dodge, damageMode: damageMode, value: value);
                            return;
                        }
                        case "Upgrade":
                        {
                            double value;
                            if (!Double.TryParse(args[2], out value))
                            {
                                return;
                            }
                            
                            skinPerksEditor.ModifySkinPerk(Perks.Upgrade, value: value);
                            return;
                        }
                        case "Build":
                        {
                            double value;
                            if (!Double.TryParse(args[2], out value))
                            {
                                return;
                            }
                            
                            skinPerksEditor.ModifySkinPerk(Perks.Build, value: value);
                            return;
                        }
                        case "Repair":
                        {
                            double value;
                            if (!Double.TryParse(args[2], out value))
                            {
                                return;
                            }
                            
                            skinPerksEditor.ModifySkinPerk(Perks.Repair, value: value);
                            return;
                        }
                        case "Magazine":
                        {
                            double value;
                            if (!Double.TryParse(args[2], out value))
                            {
                                return;
                            }
                            
                            skinPerksEditor.ModifySkinPerk(Perks.Magazine, value: value);
                            return;
                        }
                    }
                    
                    return;
                }
                case "reset":
                {
                    _skinPerksEditors[arg.Player().userID].ResetSkinPerks();
                    return;
                }
            }
        }

        #endregion
    }
}