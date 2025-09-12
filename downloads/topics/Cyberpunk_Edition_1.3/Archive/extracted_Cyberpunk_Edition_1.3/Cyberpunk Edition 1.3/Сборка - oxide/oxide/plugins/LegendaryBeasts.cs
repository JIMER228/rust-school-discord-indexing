using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Rust.Modular;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using System.Text;

namespace Oxide.Plugins
{
    [Info("Legendary Beasts", "GuestMods", "1.1.4")]
    [Description("Legendary Beasts")]
    class LegendaryBeasts : CovalencePlugin
    {
        // PLugin References
        [PluginReference]
        private Plugin ServerRewards;
        [PluginReference]
        private Plugin Economics;

        struct LegendaryBeast
        {
            public MapMarkerGenericRadius mapMarkerCircle;
            public VendingMachineMapMarker mapMarkerLabel;
            public BaseEntity entity;
            public string name;
            public string type;
            public LegendaryBeastConfig config;
        }

        private System.Random _random = new System.Random();

        private List<LegendaryBeast> _legendaryBeasts = new List<LegendaryBeast>();

        private PluginConfig _pluginConfig;

        private const string MapMarkerCirclePrefab = "assets/prefabs/tools/map/genericradiusmarker.prefab";
        private const string MapMarkerVendorPrefab = "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";
        private const string SupplyDropPrefab = "assets/prefabs/misc/supply drop/supply_drop.prefab";

        private Dictionary<string, List<LegendaryBeastConfig>> AllowedEntityConfigMap = new Dictionary<string, List<LegendaryBeastConfig>>();

        private void Init()
        {
            LoadConfig();
            RegisterPermissions();

            if (_pluginConfig.ShowMapMarkers)
            {
                timer.Every(10f, () =>
                {
                    for (int i = _legendaryBeasts.Count - 1; i >= 0; i--)
                    {
                        var legendaryBeast = _legendaryBeasts[i];

                        if (legendaryBeast.entity == null || legendaryBeast.entity.net == null)
						{
                            ClearLegendaryBeast(legendaryBeast);
                            _legendaryBeasts.RemoveAt(i);

                            continue;
						}

                        legendaryBeast.mapMarkerCircle.transform.position = legendaryBeast.entity.transform.position;
                        legendaryBeast.mapMarkerCircle.SendUpdate();
                        legendaryBeast.mapMarkerCircle.SendNetworkUpdate();
                        legendaryBeast.mapMarkerLabel.transform.position = legendaryBeast.entity.transform.position;
                        legendaryBeast.mapMarkerLabel.SendNetworkUpdate();
                    }
                });
            }

            AllowedEntityConfigMap.Clear();
            foreach (var legendaryBeastConfig in _pluginConfig.LegendaryBeasts)
            {
                foreach (var allowedEntity in GetAllowedEntities(legendaryBeastConfig))
                {
                    if (!AllowedEntityConfigMap.ContainsKey(allowedEntity))
					{
                        AllowedEntityConfigMap[allowedEntity] = new List<LegendaryBeastConfig>();
					}

                    AllowedEntityConfigMap[allowedEntity].Add(legendaryBeastConfig);
                }
            }
        }

        private void Unload()
        {
            UnloadLegendaryBeasts();
        }

        #region Permissions

        private const string PERMISSION_FORMAT = "{0}.{1}";
        private const string PERMISSION_SUFFIX = "legendarybeasts";

        private string FormatPermission(string name)
        {
            return String.Format(PERMISSION_FORMAT, PERMISSION_SUFFIX, name);
        }

        private bool HasPermission(IPlayer player, string name)
        {
            return player.HasPermission(FormatPermission(name));
        }

        private void CreatePermission(string name)
        {
            permission.RegisterPermission(FormatPermission(name), this);
        }

        private void RegisterPermissions()
        {
            CreatePermission("cleanup");
            CreatePermission("spawn");
        }

        #endregion

        #region Commands

        [Command("legendarybeasts.cleanup", "lb.cleanup")]
        private void CleanupCommand(IPlayer player, string command, string[] args)
        {
            if (!HasPermission(player, "cleanup"))
            {
                player.Message("No permission to use that command.");
                return;
            }

            player.Message("Cleaning up Legendary Beast Entities");

            UnloadLegendaryBeasts();
            foreach (var gameObject in GameObject.FindObjectsOfType<MapMarker>())
            {
                if (gameObject.name.Contains("Legendary Beast"))
                {
                    gameObject.Kill();
                }
            }
        }

        [Command("legendarybeasts.spawn", "lb.spawn")]
        private void SpawnCommand(IPlayer player, string command, string[] args)
        {
            if (!HasPermission(player, "spawn"))
            {
                player.Message("No permission to use that command.");
                return;
            }

            var message = new StringBuilder();

            if (args.Length == 0)
            {
                message.AppendLine("Legendary Beasts Spawn Command Usage");
                message.AppendLine("List beasts and their IDs: /legendarybeasts.spawn list");
                message.AppendLine("Spawn a beast by its ID: /legendarybeasts.spawn {id}");
            }
            else
            {
                if (args[0] == "list")
                {
                    for (int i = 0; i < _pluginConfig.LegendaryBeasts.Length; i++)
                    {
                        var legendaryBeast = _pluginConfig.LegendaryBeasts[i];

                        var names = string.Join(",", NamesFor(legendaryBeast));
                        var types = string.Join(",", GetAllowedEntities(legendaryBeast));

                        message.AppendLine($"( ID {i} ) - LVL: {legendaryBeast.Level}, NAMES: {names}, TYPES: {types}");
                    }
                }
                else
                {
                    try
                    {
                        int index = int.Parse(args[0]);

                        if (index >= 0 && index < _pluginConfig.LegendaryBeasts.Length)
                        {
                            var legendaryBeastConfig = _pluginConfig.LegendaryBeasts[index];

                            var allowedEntities = GetAllowedEntities(legendaryBeastConfig);
                            var entityName = allowedEntities[_random.Next(allowedEntities.Length)];

                            BasePlayer basePlayer = player.Object as BasePlayer;
                            BaseEntity entity = GameManager.server.CreateEntity($"assets/rust.ai/agents/{entityName}/{entityName}.prefab", basePlayer.ServerPosition);
                            entity.name = $"__{entity.name}";

                            if (entity)
                            {
                                entity.Spawn();
                                var legendaryBeast = CreateLegendaryBeast(entity as BaseEntity, legendaryBeastConfig);

                                var spawnMessage = HasName(legendaryBeastConfig) ? _pluginConfig.Language.NamedSpawnMessage : _pluginConfig.Language.GenericSpawnMessage;

                                player.Message(InterpolateMessage(spawnMessage, legendaryBeast.name, legendaryBeast.type, legendaryBeast.config.Level));
                            }
                        }
                        else
                        {
                            message.AppendLine($"Please enter a valid ID between 0 and {_pluginConfig.LegendaryBeasts.Length - 1}");
                        }
                    }
                    catch (FormatException)
                    {
                        message.AppendLine("Please enter a valid ID");
                    }
                }
            }

            player.Message(message.ToString());
        }
        #endregion

        #region Config
        private class PluginConfig
        {
            // Maximum number of legendary beasts allowed, -1 means there is no maximum
            public int MaximumLegendaryBeasts;

            // Allowed entities that can become legendary
            public string[] AllowedEntities;

            // The chance between 1 and 100 that an entity can spawn as a legendary
            public int LegendarySpawnChance;

            // Whether or not legendary beasts have the ability to flee from attacks
            public bool ShouldLegendaryBeastsFlee;

            // Whether or not to broadcast a message to the server when a legendary beast appears
            public bool BroadcastOnSpawn;

            // Whether or not to send the player a message when slaying a beast
            public bool SendMessageOnDeath;

            // Whether or not to show map markers for legendary beasts
            public bool ShowMapMarkers;

            // Types of legendary beasts that can spawn
            public LegendaryBeastConfig[] LegendaryBeasts;

            // Language configuration
            public LanguageConfig Language = new LanguageConfig();

            // Map marker configuration
            public MapMarkerConfig MapMarker = new MapMarkerConfig();
        }

        private class ItemDropConfig
        {
            public string Name;
            public int Amount = 1;
            public string Skin = "";
            public string CustomDisplayName = "";
        }

        private class LegendaryBeastConfig
        {
            public int Level = 1;
            public int Health = 1000;
            public float AttackRangeScale = 1;
            public float AttackDamageScale = 1;
            public float AttackRateScale = 1;
            public float MovementSpeedScale = 1;
            public bool DropsAirDropLoot = true;
            public ItemDropConfig[] CustomLootDrops = { };
            public int ServerRewardPoints = 0;
            public double Economics = 0.0;
            public string[] Names = { };
            public string[] AllowedEntities = { };
        }

        private class LanguageConfig
        {
            public string NamedMapMarkerLabel = "%name% the %type% [LVL %level%]";
            public string NamedSpawnMessage = "%name% the Legendary %type% [LVL %level%] has spawned";
            public string NamedDeathMessage = "%name% the Legendary %type% [LVL %level%] has been slain";
            public string GenericMapMarkerLabel = "%type% [LVL %level%]";
            public string GenericSpawnMessage = "Legendary %type% [LVL %level%] has spawned";
            public string GenericDeathMessage = "Legendary %type% [LVL %level%] has been slain";
        }

        private class MapMarkerConfig
		{
            public ColorConfig FillColor = new ColorConfig()
            {
                R = 0,
                G = 0,
                B = 0
            };
            public ColorConfig BorderColor = new ColorConfig()
            {
                R = 255,
                G = 0,
                B = 0
            };
            public float Alpha = 0.5F;
            public float Radius = 0.5F;
		}

        private class ColorConfig
		{
            public int R = 255;
            public int G = 255;
            public int B = 255;

            public Color ToColor()
			{
                return new Color(R / 255.0F, G / 255.0F, B / 255.0F);
			}
		}

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                MaximumLegendaryBeasts = 1,
                AllowedEntities = new string[] { "bear", "boar", "wolf" },
                LegendarySpawnChance = 60,
                ShouldLegendaryBeastsFlee = false,
                BroadcastOnSpawn = true,
                SendMessageOnDeath = true,
                ShowMapMarkers = true,
                LegendaryBeasts = new LegendaryBeastConfig[]
                {
                    new LegendaryBeastConfig()
                    {
                        Level = 1,
                        Health = 1000,
                        ServerRewardPoints = 1000
                    },
                    new LegendaryBeastConfig()
                    {
                        Level = 2,
                        Health = 2000,
                        AttackDamageScale = 2,
                        AttackRateScale = 2,
                        MovementSpeedScale = 2,
                        ServerRewardPoints = 2000
                    },
                    new LegendaryBeastConfig()
                    {
                        Level = 3,
                        Health = 3000,
                        AttackDamageScale = 3,
                        AttackRateScale = 3,
                        MovementSpeedScale = 3,
                        CustomLootDrops = new ItemDropConfig[]
                        {
                            new ItemDropConfig()
                            {
                                Name = "fat.animal",
                                Amount = 100
                            }
                        },
                        ServerRewardPoints = 3000
                    }
                },
                Language = new LanguageConfig() { },
                MapMarker = new MapMarkerConfig() {
                    FillColor = new ColorConfig()
                    {
                        R = 0,
                        G = 0,
                        B = 0
                    },
                    BorderColor = new ColorConfig()
                    {
                        R = 255,
                        G = 0,
                        B = 0
                    },
                    Alpha = 0.5F,
                    Radius = 0.5F
                }
            };
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private void LoadConfig()
        {
            _pluginConfig = Config.ReadObject<PluginConfig>();

            if (_pluginConfig.Language == null)
            {
                _pluginConfig.Language = new LanguageConfig();
            }
        }

        private void SaveConfig()
        {
            Config.WriteObject(_pluginConfig, true);
        }

        #endregion

        private string GetEntityName(BaseEntity entity)
        {
            return entity.ShortPrefabName.Substring(0, 1).ToUpper() + entity.ShortPrefabName.Substring(1);
        }

        private void UnloadLegendaryBeasts()
        {
            foreach (var legendaryBeast in _legendaryBeasts)
            {
                ClearLegendaryBeast(legendaryBeast);
            }

            _legendaryBeasts.Clear();
        }

        private string[] GetAllowedEntities(LegendaryBeastConfig legendaryBeastConfig)
		{
            if (legendaryBeastConfig.AllowedEntities.Length != 0)
			{
                return legendaryBeastConfig.AllowedEntities;
			}

            return _pluginConfig.AllowedEntities;
		}

        private LegendaryBeastConfig SelectLegendaryBeastConfig(LegendaryBeastConfig[] selections = null)
        {
            if (selections != null)
			{
                return selections[_random.Next(selections.Length)];
            }

            return _pluginConfig.LegendaryBeasts[_random.Next(_pluginConfig.LegendaryBeasts.Length)];
        }

        private string[] NamesFor(LegendaryBeastConfig legendaryBeastConfig)
		{
            return legendaryBeastConfig.Names;
		}

        private string SelectRandomName(LegendaryBeastConfig legendaryBeastConfig)
        {
            var names = NamesFor(legendaryBeastConfig);
            return names[_random.Next(names.Length)];
        }

        private bool HasName(LegendaryBeastConfig legendaryBeastConfig)
		{
            return legendaryBeastConfig.Names.Length > 0;
		}

        private LegendaryBeast CreateLegendaryBeast(BaseEntity entity, LegendaryBeastConfig selectedLegendaryBeastConfig = null)
        {
            LegendaryBeastConfig legendaryBeastConfig = selectedLegendaryBeastConfig;

            if (legendaryBeastConfig == null)
			{
                legendaryBeastConfig = SelectLegendaryBeastConfig();
			}

            int level = legendaryBeastConfig.Level;
            int health = legendaryBeastConfig.Health;
            float attackRangeScale = legendaryBeastConfig.AttackRangeScale;
            float attackDamageScale = legendaryBeastConfig.AttackDamageScale;
            float attackRateScale = legendaryBeastConfig.AttackRateScale;
            float movementSpeedScale = legendaryBeastConfig.MovementSpeedScale;
            string type = GetEntityName(entity);

            LegendaryBeast legendaryBeast = new LegendaryBeast();
            legendaryBeast.entity = entity;
            legendaryBeast.name = "";
            legendaryBeast.type = type;
            legendaryBeast.config = legendaryBeastConfig;

            if (legendaryBeastConfig.Names.Length > 0)
            {
                legendaryBeast.name = SelectRandomName(legendaryBeastConfig);
            }

            if (_pluginConfig.ShowMapMarkers)
            {
                var mapMarkerLabel = HasName(legendaryBeastConfig) ? _pluginConfig.Language.NamedMapMarkerLabel : _pluginConfig.Language.GenericMapMarkerLabel;
                var markerName = InterpolateMessage(mapMarkerLabel, legendaryBeast.name, legendaryBeast.type, legendaryBeast.config.Level);
                legendaryBeast.mapMarkerCircle = CreateMapMarkerCircle(markerName, entity.transform.position,
                    _pluginConfig.MapMarker.FillColor.ToColor(), _pluginConfig.MapMarker.BorderColor.ToColor(),
                    _pluginConfig.MapMarker.Alpha, _pluginConfig.MapMarker.Radius);
                legendaryBeast.mapMarkerLabel = CreateMapMarkerLabel(markerName, entity.transform.position);
            }

            _legendaryBeasts.Add(legendaryBeast);

            BaseCombatEntity combatEntity = entity as BaseCombatEntity;
            combatEntity.InitializeHealth(health, health);

            NextFrame(() =>
            {
                var brain = combatEntity.GetComponent<AnimalBrain>();
                var animal = combatEntity.GetComponent<BaseAnimalNPC>();

                animal.AttackRange *= attackRangeScale;
                animal.AttackDamage *= attackDamageScale;
                animal.AttackRate *= attackRateScale;

                if (!_pluginConfig.ShouldLegendaryBeastsFlee)
                {
                    brain.states.Remove(AIState.Flee);
                    brain.states.Add(AIState.Flee, brain.states[AIState.Chase]);
                }

                brain.Navigator.FastSpeedFraction *= movementSpeedScale;

                entity.SendNetworkUpdate();
            });

            return legendaryBeast;
        }

        private MapMarkerGenericRadius CreateMapMarkerCircle(String name, Vector3 position, Color background, Color border, float alpha, float radius)
        {
            MapMarkerGenericRadius mapMarker = GameManager.server.CreateEntity(MapMarkerCirclePrefab, position) as MapMarkerGenericRadius;
            mapMarker.name = name;
            mapMarker.color1 = background; // Background
            mapMarker.color2 = border; // Border
            mapMarker.alpha = alpha;
            mapMarker.radius = radius;

            mapMarker.Spawn();
            mapMarker.SendUpdate();

            return mapMarker;
        }

        private VendingMachineMapMarker CreateMapMarkerLabel(String name, Vector3 position)
        {
            VendingMachineMapMarker mapMarker = GameManager.server.CreateEntity(MapMarkerVendorPrefab, position) as VendingMachineMapMarker;
            mapMarker.name = name;
            mapMarker.markerShopName = name;

            mapMarker.Spawn();

            return mapMarker;
        }

        private void ClearLegendaryBeast(LegendaryBeast legendaryBeast)
        {
            if (legendaryBeast.entity != null)
            {
                legendaryBeast.entity.Kill();
            }

            if (_pluginConfig.ShowMapMarkers)
            {
                legendaryBeast.mapMarkerCircle.Kill();
                legendaryBeast.mapMarkerCircle.SendUpdate();
                legendaryBeast.mapMarkerLabel.Kill();
            }
        }

        private void SpawnSupplyCrate(Vector3 position, bool airDropLoot, ItemDropConfig[] customDrops)
        {
            Vector3 newPosition = position + new Vector3(0, 20, 0);
            BaseEntity supplyCrateEntity = GameManager.server.CreateEntity(SupplyDropPrefab, newPosition);

            if (supplyCrateEntity != null)
            {
                SupplyDrop drop = supplyCrateEntity.GetComponent<SupplyDrop>();
                drop.Spawn();

                ItemContainer container = drop.inventory;
                if (!airDropLoot)
                {
                    while (container.itemList.Count > 0)
                    {
                        var item = container.itemList[0];
                        item.RemoveFromContainer();
                        item.Remove(0f);
                        item.DoRemove();
                    }
                }

                foreach (var customDrop in customDrops)
                {
                    var skinID = 0UL;

                    if (customDrop.Skin.Length != 0)
					{
                        try
                        {
                            skinID = ulong.Parse(customDrop.Skin);
                        }
                        catch
                        {
                            LogWarning("{0} is not a valid skin ID for custom drop {1}", customDrop.Skin, customDrop.Name);
                        }
                    }

                    var item = ItemManager.CreateByName(customDrop.Name, customDrop.Amount, skinID);

                    if (customDrop.CustomDisplayName.Length != 0)
					{
                        item.name = customDrop.CustomDisplayName;
					}

                    item.MarkDirty();
                    item.MoveToContainer(container, -1, false);
                }

                if (container.itemList.Count == 0)
                {
                    drop.Kill();
                }
            }
        }

        private void SpawnReward(LegendaryBeast legendaryBeast, BasePlayer player)
        {
            if (legendaryBeast.config.Economics != 0.0)
			{
                Economics?.Call("Deposit", player.userID, legendaryBeast.config.Economics);
            }

            if (legendaryBeast.config.ServerRewardPoints != 0)
            {
                ServerRewards?.Call("AddPoints", player.userID, legendaryBeast.config.ServerRewardPoints);
            }

            SpawnSupplyCrate(legendaryBeast.entity.transform.position, legendaryBeast.config.DropsAirDropLoot, legendaryBeast.config.CustomLootDrops);
        }

        private string InterpolateMessage(string message, string name = "", string type = "", int level = 0)
        {
            return message
                .Replace("%name%", name)
                .Replace("%type%", type)
                .Replace("%level%", level.ToString());
        }

        #region Events

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity.name.StartsWith("__"))
            {
                return;
            }

            if (!AllowedEntityConfigMap.ContainsKey(entity.ShortPrefabName))
            {
                return;
            }

            if (_legendaryBeasts.Count < _pluginConfig.MaximumLegendaryBeasts
                || _pluginConfig.MaximumLegendaryBeasts == -1)
            {
                if (entity.HasComponent<AnimalBrain>())
                {
                    var chance = _random.Next(1, 100);

                    if (chance <= _pluginConfig.LegendarySpawnChance)
                    {
                        var config = SelectLegendaryBeastConfig(AllowedEntityConfigMap[entity.ShortPrefabName].ToArray());
                        var legendaryBeast = CreateLegendaryBeast(entity as BaseEntity, config);

                        if (_pluginConfig.BroadcastOnSpawn)
                        {
                            var spawnMessage = HasName(legendaryBeast.config) ? _pluginConfig.Language.NamedSpawnMessage : _pluginConfig.Language.GenericSpawnMessage;

                            server.Broadcast(InterpolateMessage(spawnMessage, legendaryBeast.name, legendaryBeast.type, legendaryBeast.config.Level));
                        }
                    }
                }
            }
        }


        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            // List<int> toRemove = null;

            for (int i = _legendaryBeasts.Count - 1; i >= 0; i--)
            {
                var legendaryBeast = _legendaryBeasts[i];

                if (legendaryBeast.entity?.net?.ID == entity.net?.ID)
                {
                    /*if (toRemove == null)
                    {
                        toRemove = new List<int>();
                    }

                    toRemove.Add(i);*/
                    if (info != null)
                    {
                        var attacker = info.Initiator;

                        if (attacker != null && attacker is BasePlayer)
                        {
                            var player = attacker as BasePlayer;
                            if (_pluginConfig.SendMessageOnDeath)
                            {
                                var deathMessage = HasName(legendaryBeast.config) ? _pluginConfig.Language.NamedDeathMessage : _pluginConfig.Language.GenericDeathMessage;

                                player.ChatMessage(InterpolateMessage(deathMessage, legendaryBeast.name, legendaryBeast.type, legendaryBeast.config.Level));
                            }
                            SpawnReward(legendaryBeast, player);
                        }
                    }

                    ClearLegendaryBeast(legendaryBeast);
                    _legendaryBeasts.RemoveAt(i);
                }
            }

            /*if (toRemove != null)
            {
                foreach (int index in toRemove)
                {
                    ClearLegendaryBeast(_legendaryBeasts[index]);
                    _legendaryBeasts.RemoveAt(index);
                }
            }*/
        }

        #endregion
    }
}