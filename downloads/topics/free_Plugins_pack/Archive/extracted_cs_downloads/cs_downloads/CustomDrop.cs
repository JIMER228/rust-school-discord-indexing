using System.Collections.Generic;
using System.Linq;
using System;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
	[Info("CustomDrop", "King", "1.0.0")]
	public class CustomDrop : RustPlugin
	{
        #region [Vars]
        private const Boolean LanguageEn = false;

        private Dictionary<UInt64, SupplyDropSetting> SupplyDropInfo = new Dictionary<UInt64, SupplyDropSetting>();
		private Dictionary<UInt64, SupplyDropSetting> SupplySignalInfo = new Dictionary<UInt64, SupplyDropSetting>();
		private Dictionary<UInt64, SupplyDropSetting> CargoPlaneInfo = new Dictionary<UInt64, SupplyDropSetting>();
        private Dictionary<UInt64, SupplyDropSetting> ResourseList = new Dictionary<UInt64, SupplyDropSetting>();
        private List<UInt64> LootEntityList = new List<UInt64>();
        #endregion

        #region [Rust-Api]
        private void OnExplosiveDropped(BasePlayer player, SupplySignal supplySignal, ThrownWeapon weapon) =>
            OnExplosiveThrown(player, supplySignal, weapon);

		private void OnExplosiveThrown(BasePlayer player, SupplySignal supplySignal, ThrownWeapon weapon)
		{
			if (player == null || weapon == null || weapon.skinID == 0 || supplySignal == null || supplySignal.net == null) return;

            SupplyDropSetting findSupply = config.AirDropList.FirstOrDefault(p => p.SkinID == weapon.skinID);
            if (findSupply == null) return;

			SupplySignalInfo[supplySignal.net.ID.Value] = findSupply;
        }

        private void OnCargoPlaneSignaled(CargoPlane cargoPlane, SupplySignal supplySignal)
        {
			if (cargoPlane == null || supplySignal == null || supplySignal.net == null) return;

            SupplyDropSetting findSupply;
            if (!SupplySignalInfo.TryGetValue(supplySignal.net.ID.Value, out findSupply)) return;

			CargoPlaneInfo[cargoPlane.net.ID.Value] = findSupply;
			SupplySignalInfo.Remove(supplySignal.net.ID.Value);
        }

        private void OnSupplyDropDropped(SupplyDrop supplyDrop, CargoPlane cargoPlane)
        {
            if (supplyDrop == null || cargoPlane == null || cargoPlane.net == null) return;

            SupplyDropSetting findSupply;
            if (!CargoPlaneInfo.TryGetValue(cargoPlane.net.ID.Value, out findSupply)) return;

            SupplyDropInfo[supplyDrop.net.ID.Value] = findSupply;
            CargoPlaneInfo.Remove(cargoPlane.net.ID.Value);
        }

        private void OnSupplyDropLanded(SupplyDrop supplyDrop)
        {
            if (supplyDrop == null || supplyDrop.net == null) return;

            SupplyDropSetting findSupply;
            if (!SupplyDropInfo.TryGetValue(supplyDrop.net.ID.Value, out findSupply)) return;

            Transform transform = supplyDrop.transform;
            Vector3 position = transform.position;
            position.y = GetGroundPosition(position);
            SupplyDropInfo.Remove(supplyDrop.net.ID.Value);
            supplyDrop.Kill();

            BaseEntity entity = GameManager.server.CreateEntity(findSupply.PrefabName, position, transform.rotation);
            if (entity == null) return;
            entity.Spawn();

            ResourceDispenser resourceDispenser = entity.GetComponent<ResourceDispenser>();
            if (resourceDispenser != null)
            {
                ItemAmount findHQM = resourceDispenser.finishBonus.FirstOrDefault(p => p.itemid == -1982036270);
                if (findHQM != null) resourceDispenser.finishBonus.Remove(findHQM);
                ResourseList[entity.net.ID.Value] = findSupply;
            }
        }

		private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
		{
            if (dispenser == null || player == null || item == null) return;

            BaseEntity entity = dispenser.baseEntity;
            if (entity == null || entity.net == null) return;

            SupplyDropSetting findSupply;
            if (!ResourseList.TryGetValue(entity.net.ID.Value, out findSupply)) return;
            
            Single Rate = 1.0f;
            foreach (KeyValuePair<String, Single> Rates in config.AirDropRates)
            {
                if(HasPermision(player.UserIDString, Rates.Key))
                    Rate = Rates.Value;
            }

            item.amount = Convert.ToInt32(item.amount * Rate);
		}

		private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
		{
            if (dispenser == null || player == null || item == null) return;

            BaseEntity entity = dispenser.baseEntity;
            if (entity == null || entity.net == null) return;

            SupplyDropSetting findSupply;
            if (!ResourseList.TryGetValue(entity.net.ID.Value, out findSupply)) return;

            Single Rate = 1.0f;
            foreach (KeyValuePair<String, Single> Rates in config.AirDropRates)
            {
                if(HasPermision(player.UserIDString, Rates.Key))
                    Rate = Rates.Value;
            }

            item.amount = Convert.ToInt32(item.amount * Rate);

            ItemSetting RandomItem = findSupply.ItemSettings.GetRandom();
            Int32 Amount = Oxide.Core.Random.Range(RandomItem.MinAmount, RandomItem.MaxAmount);
            GiveItem(player, RandomItem.ShortName, Amount, RandomItem.SkinID, null);
            ResourseList.Remove(entity.net.ID.Value);
		}

        private void OnLootEntity(BasePlayer player, LootContainer entity)
        {
            if (player == null || entity == null || entity?.net?.ID == null || LootEntityList.Contains(entity.net.ID.Value)) return;

            LootEntityList.Add(entity.net.ID.Value);

            Int32 Chance = Oxide.Core.Random.Range(0, 100);
            foreach (SupplyDropSetting AirDrop in config.AirDropList)
            {
                if (Chance >= (100 - AirDrop.Rarity))
                {
                    GiveItem(player, "supply.signal", 1, AirDrop.SkinID, AirDrop.DisplayName);
                    break;
                }
            }
        }

		private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
		{
			if (entity == null || info == null) return;

			BasePlayer player = info.InitiatorPlayer;
			if (player == null) return;

            if (entity.PrefabName.Contains("barrel"))
            {
                Int32 Chance = Oxide.Core.Random.Range(0, 100);
                foreach (SupplyDropSetting AirDrop in config.AirDropList)
                {
                    if (Chance >= (100 - AirDrop.Rarity))
                    {
                        GiveItem(player, "supply.signal", 1, AirDrop.SkinID, AirDrop.DisplayName);
                        break;
                    }
                }
            }
        }
        #endregion

        #region [Oxide-Api]
        private void Init()
        {
			Unsubscribe("OnDispenserGather");
			Unsubscribe("OnDispenserBonus");
        }

        private void OnServerInitialized()
        {
            RegisterPermissions();

            if (config.AirDropRatesUse)
            {
                Subscribe("OnDispenserGather");
                Subscribe("OnDispenserBonus");
            }
        }

        private void Unload()
        {
            foreach (UInt64 netID in CargoPlaneInfo.Keys)
            {
                CargoPlane cargoPlane = BaseNetworkable.serverEntities.Find(new NetworkableId(netID)) as CargoPlane;
                if (cargoPlane == null) continue;

                cargoPlane.Kill();
            }

            foreach (UInt64 netID in SupplyDropInfo.Keys)
            {
                SupplyDrop supplyDrop = BaseNetworkable.serverEntities.Find(new NetworkableId(netID)) as SupplyDrop;
                if (supplyDrop == null) continue;

                supplyDrop.Kill();
            }

            foreach (UInt64 netID in ResourseList.Keys)
            {
                BaseEntity resourceDispenser = BaseNetworkable.serverEntities.Find(new NetworkableId(netID)) as BaseEntity;
                if (resourceDispenser == null) continue;

                resourceDispenser.Kill();
            }

            SupplyDropInfo.Clear();
            SupplySignalInfo.Clear();
            CargoPlaneInfo.Clear();
            ResourseList.Clear();
            LootEntityList.Clear();
        }
        #endregion

        #region [ConsoleCommand]
		[ConsoleCommand("cd.give")]
		private void CustomDropGive(ConsoleSystem.Arg arg)
        {
		    BasePlayer player = arg.Player();
            if (player != null && !player.IsAdmin) return;

			if (!arg.HasArgs(3))
			{
                PrintWarning(LanguageEn ? $"Error syntax! Use: cd.give [Name|Steamid] [SkinID] [Amount]" : $"Ошибка синтаксиса! Используйте: cd.give [Name|Steamid] [SkinID] [Amount]");
				return;
			}

            BasePlayer findPlayer = BasePlayer.FindAwakeOrSleeping(arg.Args[0]);
            if (findPlayer == null)
            {
                PrintWarning(LanguageEn ? $"Player not found." : $"Игрок не найден.");
                return;
            }

            UInt64 SkinID;
            if (!UInt64.TryParse(arg.Args[1], out SkinID))
            {
                PrintWarning(LanguageEn ? $"SkinID is not a number." : $"СкинАйди не является числом.");
                return;
            }

            SupplyDropSetting findSupply = config.AirDropList.FirstOrDefault(p => p.SkinID == SkinID);
            if (findSupply == null)
            {
                PrintWarning(LanguageEn ? $"CustomDrop was not found." : $"CustomDrop не найден.");
                return;
            }

            Int32 Amount;
            if (!Int32.TryParse(arg.Args[2], out Amount))
            {
                PrintWarning(LanguageEn ? $"Amount is not a number." : $"Количество не является числом.");
                return;
            }

            GiveItem(findPlayer, "supply.signal", Amount, findSupply.SkinID, findSupply.DisplayName);
        }
        #endregion

        #region [Functional]
		private void RegisterPermissions()
		{
            foreach (String perm in config.AirDropRates.Keys)
            {
                if (!String.IsNullOrEmpty(perm) && !permission.PermissionExists(perm))
                    permission.RegisterPermission(perm, this);
            }

            foreach (String perm in config.AirDropRates.Keys)
            {
                if (!String.IsNullOrEmpty(perm) && !permission.PermissionExists(perm))
                    permission.RegisterPermission(perm, this);
            }
		}

        private Boolean HasPermision(String userID, String Permission)
        {
            if (permission.UserHasPermission(userID, Permission))
                return true;
            return false;
        }

        private void GiveItem(BasePlayer player, String ShortName, Int32 Amount, UInt64 SkinID, String ItemName)
        {
            Item Signal = ItemManager.CreateByName(ShortName, Amount, SkinID);
            if (Signal == null) return;

            if (!string.IsNullOrWhiteSpace(ItemName))
                Signal.name = ItemName;

            if (Signal.MoveToContainer(player.inventory.containerMain))
                player.Command("note.inv", Signal.info.itemid, Signal.amount,
                    !String.IsNullOrEmpty(Signal.name) ? Signal.name : String.Empty,
                    (Int32)BaseEntity.GiveItemReason.PickedUp);
            else
                Signal.Drop(player.inventory.containerMain.dropPosition,
                    player.inventory.containerMain.dropVelocity);
        }

        static float GetGroundPosition(Vector3 position)
        {
            float y = TerrainMeta.HeightMap.GetHeight(position);
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(position.x, position.y + 200f, position.z), Vector3.down, out hit, Mathf.Infinity, LayerMask.GetMask(new[] {
                "Terrain", "World", "Default", "Construction", "Deployed"
            }
            )) && !hit.collider.name.Contains("rock_cliff")) return Mathf.Max(hit.point.y, y);
            return y;
        }
        #endregion

        #region [Config]
        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion < Version)
                UpdateConfigValues();

            Config.WriteObject(config, true);
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < Version)
            {
                if (Version == new VersionNumber(1, 0, 0))
                {
                    //
                }

                PrintWarning("Config checked completed!");
            }
            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        public class ItemSetting
        {
            [JsonProperty(LanguageEn ? "Item Shortname" : "Shortname предмета")]
            public String ShortName;

            [JsonProperty(LanguageEn ? "SkinID" : "SkinID")]
            public UInt32 SkinID;

            [JsonProperty(LanguageEn ? "Minimum amount" : "Минимальное количество")]
            public Int32 MinAmount;

            [JsonProperty(LanguageEn ? "Maximum amount" : "Максимальное количество")]
            public Int32 MaxAmount;
        }

        private class SupplyDropSetting
        {
            [JsonProperty(LanguageEn ? "Displayed Item Name | Signal Grenade" : "Отображаемое имя предмета | Сигнальная гранат")]
            public String DisplayName;

            [JsonProperty(LanguageEn ? "PrefabName | Signal Grenade" : "Название префаба | Сигнальная гранат")]
            public String PrefabName;

            [JsonProperty(LanguageEn ? "SkinID item | Signal Grenade" : "SkinID предмета | Сигнальная гранат")]
            public UInt64 SkinID;

            [JsonProperty(LanguageEn ? "Chance of dropping a signal grenade" : "Шанс выпадения сигнальной гранаты")]
            public Int32 Rarity;

            [JsonProperty(LanguageEn ? "Bonus item of breaking stone" : "Бонусный предмет ломании камня")]
            public List<ItemSetting> ItemSettings;
        }

        private class PluginConfig
        {
            [JsonProperty(LanguageEn ? "Custom AirDrop settings" : "Настройки кастомного АирДропа")]
            public List<SupplyDropSetting> AirDropList = new List<SupplyDropSetting>();

            [JsonProperty(LanguageEn ? "Use rates to mine rocks with AirDrop ?" : "Использовать рейты для добычи камней с АирДропа ?")]
            public Boolean AirDropRatesUse;

            [JsonProperty(LanguageEn ? "Rates for gather stone with AirDrop | Permission - Rates" : "Рейты для добычи камней с АирДропа | Permission - Rates")]
            public Dictionary<String, Single> AirDropRates = new Dictionary<String, Single>();

            [JsonProperty(LanguageEn ? "Config version" : "Версия конфигурации")]
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    AirDropList = new List<SupplyDropSetting>()
                    {
                        new SupplyDropSetting
                        {
                            DisplayName = LanguageEn ? "Signal Grenade | Minicopter" : "Сигнальная шашка | Миникоптер",
                            PrefabName = "assets/content/vehicles/Minicopter/Minicopter.entity.prefab",
                            SkinID = 3060835632,
                            Rarity = 40,
                            ItemSettings = new List<ItemSetting>()
                        },
                        new SupplyDropSetting
                        {
                            DisplayName = LanguageEn ? "Signal Grenade | Sulfur stone" : "Сигнальная шашка | Серный камень",
                            PrefabName = "assets/bundled/prefabs/autospawn/resource/ores/sulfur-ore.prefab",
                            SkinID = 3060813889,
                            Rarity = 40,
                            ItemSettings = new List<ItemSetting>
                            {
                                new ItemSetting
                                {
                                    ShortName = "metalpipe",
                                    SkinID = 0,
                                    MinAmount = 10,
                                    MaxAmount = 20
                                },
                                new ItemSetting
                                {
                                    ShortName = "metalblade",
                                    SkinID = 0,
                                    MinAmount = 10,
                                    MaxAmount = 20
                                },
                            }
                        },
                        new SupplyDropSetting
                        {
                            DisplayName = LanguageEn ? "Signal Grenade | Metal stone" : "Сигнальная шашка | Металический камень",
                            PrefabName = "assets/bundled/prefabs/autospawn/resource/ores/metal-ore.prefab",
                            SkinID = 3060812671,
                            Rarity = 40,
                            ItemSettings = new List<ItemSetting>
                            {
                                new ItemSetting
                                {
                                    ShortName = "metalpipe",
                                    SkinID = 0,
                                    MinAmount = 10,
                                    MaxAmount = 20
                                },
                                new ItemSetting
                                {
                                    ShortName = "metalblade",
                                    SkinID = 0,
                                    MinAmount = 10,
                                    MaxAmount = 20
                                },
                            }
                        },
                        new SupplyDropSetting
                        {
                            DisplayName = LanguageEn ? "Signal Grenade | Default stone" : "Сигнальная шашка | Обычный камень",
                            PrefabName = "assets/bundled/prefabs/autospawn/resource/ores/stone-ore.prefab",
                            SkinID = 3060813413,
                            Rarity = 40,
                            ItemSettings = new List<ItemSetting>
                            {
                                new ItemSetting
                                {
                                    ShortName = "metalpipe",
                                    SkinID = 0,
                                    MinAmount = 10,
                                    MaxAmount = 20
                                },
                                new ItemSetting
                                {
                                    ShortName = "metalblade",
                                    SkinID = 0,
                                    MinAmount = 10,
                                    MaxAmount = 20
                                },
                            }
                        }
                    },
                    AirDropRatesUse = true,
                    AirDropRates = new Dictionary<String, Single>()
                    {
                        ["customdrop.x2"] = 2.0f,
                        ["customdrop.x3"] = 3.0f,
                        ["customdrop.x4"] = 4.0f
                    },
                    PluginVersion = new VersionNumber()
                };
            }
        }
        #endregion
    }
}