// Reference: System.Drawing
using Facepunch;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("HitMarker", "BlackWolf", "1.0.6")]
    class HitMarker : RustPlugin
    {
        #region HOOKS

        // Register all hooks
        private void Init()
        {
            // Register all hooks explicitly
            permission.RegisterPermission("hitmarker.use", this);
            
            // Subscribe to hooks
            Subscribe(nameof(OnServerInitialized));
            Subscribe(nameof(OnEntityTakeDamage));
            Subscribe(nameof(OnPlayerAttack));
            Subscribe(nameof(OnEntityDeath));
            Subscribe(nameof(Unload));
        }

        #endregion

        #region CONFIGURATION

        private bool Changed;
        private bool enablesound;
        private string? soundeffect;
        private string? headshotsoundeffect;
        private float damageTimeout;
        private float destroyedTimeout = 5.0f; // Increased to 5 seconds for health bars and killed messages
		
        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }        

        protected override void LoadDefaultConfig()
        {
            enablesound = Convert.ToBoolean(GetConfig("Sound", "EnableSoundEffect", true), System.Globalization.CultureInfo.InvariantCulture);
            soundeffect = Convert.ToString(GetConfig("Sound", "Sound Effect", "assets/bundled/prefabs/fx/takedamage_hit.prefab"), System.Globalization.CultureInfo.InvariantCulture);
            headshotsoundeffect = Convert.ToString(GetConfig("Sound", "HeadshotSoundEffect", "assets/bundled/prefabs/fx/headshot.prefab"), System.Globalization.CultureInfo.InvariantCulture);
            GetVariable(Config, "Через сколько будет пропадать урон", out damageTimeout, 5.0f); // Increased to 5 seconds
            GetVariable(Config, "Через сколько будет пропадать destroyed", out destroyedTimeout, 5.0f); // Increased to 5 seconds
            
            SaveConfig();
        }
        public static void GetVariable<T>( DynamicConfigFile config, string name, out T value, T defaultValue )
        {
            config[ name ] = value = config[ name ] == null ? defaultValue : (T) Convert.ChangeType( config[ name ], typeof( T ), System.Globalization.CultureInfo.InvariantCulture );
        }
        #endregion
        
        #region FIELDS

        [PluginReference] private Plugin? Clans;
        
		Random rnd = new Random();
		
        List<BasePlayer> hitmarkeron = new List<BasePlayer>();

        Dictionary<BasePlayer, List<KeyValuePair<float, HitNfo>>> damageHistory = new Dictionary<BasePlayer, List<KeyValuePair<float, HitNfo>>>();

		class HitNfo
		{
			public int damage;
			public bool isHead;
			public bool isFriend;
			public float xs;
			public float ys;
			public float xe;
			public float ye;
			public int num;
			public string entityType;
		}

        // Class to store target information for splash damage
        class SplashTarget
        {
            public string EntityType { get; set; }
            public bool IsHeadshot { get; set; }
            public string EntityName { get; set; }
            public string TextColor { get; set; }
            public int Health { get; set; }
            public int MaxHealth { get; set; }
            public bool IsDestroyed { get; set; }
        }
		
        Dictionary<BasePlayer, Oxide.Plugins.Timer> destTimers = new Dictionary<BasePlayer, Oxide.Plugins.Timer>();
        #endregion

        #region COMMANDS

        [ChatCommand("hitmarker")]
        void cmdHitMarker(BasePlayer player, string cmd, string[] args)
        {
            if (!hitmarkeron.Contains(player))
            {
                hitmarkeron.Add(player);
                SendReply(player,
                    "<color=#00ffff>HitMarker</color>:" + " " + "<color=orange>Вы включили показ урона.</color>");
            }
            else
            {
                hitmarkeron.Remove(player);
                SendReply(player,
                    "<color=#00ffff>HitMarker</color>:" + " " + "<color=orange>Вы отключили показ урона.</color>");
            }
        }

        #endregion

        #region OXIDE HOOKS

        void Unload()
        {
            // Очищаем списки игроков при выгрузке плагина
            foreach (var player in BasePlayer.activePlayerList)
            {
                hitmarkeron.Remove(player);
                damageHistory.Remove(player);
            }
        }

        void OnServerInitialized(bool initial)
        {            
            // Загружаем конфигурацию и добавляем всех активных игроков в список
            LoadDefaultConfig();
            foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
                hitmarkeron.Add(current);
            }            
            // Запускаем таймер для обновления индикаторов урона
            timer.Every(0.1f, OnDamageTimer);
        }        

        void OnPlayerConnected(BasePlayer player)
        {
            // Добавляем игрока в список при подключении
            hitmarkeron.Add(player);
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            // Удаляем игрока из списков при отключении
            hitmarkeron.Remove(player);
            damageHistory.Remove(player);
        }
        
        void OnPlayerAttack(BasePlayer attacker, HitInfo hitinfo)
        {
            // Воспроизводим звуковые эффекты при атаке игрока
            if (hitinfo?.HitEntity == null || attacker == null) return;
            var victim = hitinfo.HitEntity as BasePlayer;
            if (victim != null && hitmarkeron.Contains(attacker) && attacker.transform != null && attacker.net?.connection != null)
            {                
                if (hitinfo.isHeadshot)
                {
                    if (enablesound && headshotsoundeffect != null)
                    {
                        Effect.server.Run(headshotsoundeffect, attacker.transform.position, Vector3.zero,
                            attacker.net.connection);
                    }
                }
                else
                {
                    if (enablesound && soundeffect != null)
                    {
                        Effect.server.Run(soundeffect, attacker.transform.position, Vector3.zero,
                            attacker.net.connection);
                    }
                }
            }
        }

        string ActionGUI = @"[
  {
    ""name"": ""hitmarkerAction_{0}_{1}"",
    ""parent"": ""Overlay"",
    ""components"": [
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""0.4 0.2"",
        ""anchormax"": ""0.6 0.25""
      }
    ]
  },
  {
    ""name"": ""hitmarkerActionText_{0}_{1}"",
    ""parent"": ""hitmarkerAction_{0}_{1}"",
    ""components"": [
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""0 0"",
        ""anchormax"": ""1 1""
      },
      {
        ""type"": ""UnityEngine.UI.Text"",
        ""text"": ""{2}"",
        ""fontSize"": 16,
        ""font"": ""robotocondensed-bold.ttf"",
        ""align"": ""MiddleCenter"",
        ""color"": ""{3}""
      }
    ]
  }
]";
        
        string DamageGUI = @"[
  {
    ""name"": ""hitmarkerDamage_{0}_{1}"",
    ""parent"": ""Overlay"",
    ""components"": [
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""{4} {5}"",
        ""anchormax"": ""{6} {7}""
      }
    ]
  },
  {
    ""name"": ""hitmarkerDamageText_{0}_{1}"",
    ""parent"": ""hitmarkerDamage_{0}_{1}"",
    ""components"": [
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""0 0"",
        ""anchormax"": ""1 1""
      },
      {
        ""type"": ""UnityEngine.UI.Text"",
        ""text"": ""{2}"",
        ""fontSize"": 14,
        ""font"": ""robotocondensed-bold.ttf"",
        ""align"": ""MiddleCenter"",
        ""color"": ""{3}""
      }
    ]
  }
]";
        
        static string HandleArgs(string json, params object[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var value = args[i];
                string strValue;
                
                if (value is float f)
                {
                    strValue = f.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (value is double d)
                {
                    strValue = d.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (value is IFormattable formattable)
                {
                    strValue = formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    strValue = value?.ToString() ?? "";
                }
                
                json = json.Replace("{" + i + "}", strValue);
            }
            return json;
        }
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null) return;
            var attacker = hitInfo.InitiatorPlayer;
            if (attacker == null || !hitmarkeron.Contains(attacker)) return;

            // Skip construction entities that shouldn't show damage
            if (entity is SimpleBuildingBlock) return;

            // Get pre-hit values
            float preHealth = entity.Health();
            float maxHealth = entity.MaxHealth();

            NextTick(() =>
            {
                // Check for splash damage
                bool isSplashDamage = hitInfo.damageTypes.Has(Rust.DamageType.Explosion) || 
                                     hitInfo.damageTypes.Has(Rust.DamageType.Heat) ||
                                     hitInfo.damageTypes.Has(Rust.DamageType.Blunt) ||
                                     hitInfo.damageTypes.Has(Rust.DamageType.ElectricShock);

                // Get post-hit values
                float postHealth = entity.Health();
                float actualDamage = Math.Max(0, preHealth - postHealth);
                float cappedDamage = Math.Min(actualDamage, preHealth);
                int damage = (int)Math.Round(cappedDamage);

                // Skip if no real damage was dealt
                if (damage <= 0) return;

                // Calculate health values
                int currentHealth = (int)Math.Round(postHealth);
                int maxHealthInt = (int)Math.Round(maxHealth);
                bool isDead = currentHealth <= 0 || entity.IsDead();

                // Get entity information
                string entityType = DetermineEntityType(entity);
                string entityName = GetEntityDisplayName(entity);
                bool isHead = hitInfo.isHeadshot;
                bool isFriend = false;

                // Check for friend status if entity is a player
                if (entity is BasePlayer targetPlayer)
                {
                    isFriend = (Clans?.Call("HasFriend", attacker.userID, targetPlayer.userID) as bool?) ?? false;
                }

                // Create target info
                var target = new SplashTarget
                {
                    EntityType = entityType,
                    IsHeadshot = isHead,
                    EntityName = entityName,
                    TextColor = GetTargetColor(entityType, isHead, entityName),
                    Health = currentHealth,
                    MaxHealth = maxHealthInt,
                    IsDestroyed = isDead
                };

                // For splash damage, collect all affected entities
                if (isSplashDamage)
                {
                    // Get nearby entities for splash damage
                    var targets = new List<SplashTarget>();
                    var nearbyEntities = Pool.GetList<BaseCombatEntity>();
                    Vis.Entities(entity.transform.position, 5f, nearbyEntities, LayerMask.GetMask("Deployed", "Construction", "Player (Server)", "AI"));

                    foreach (var nearbyEntity in nearbyEntities.Take(4)) // Limit to 4 targets
                    {
                        if (nearbyEntity == null || nearbyEntity == entity) continue;

                        // Only show important entities
                        string nearbyType = DetermineEntityType(nearbyEntity);
                        if (!IsImportantEntity(nearbyType, nearbyEntity)) continue;

                        targets.Add(new SplashTarget
                        {
                            EntityType = nearbyType,
                            IsHeadshot = false, // Splash damage doesn't cause headshots
                            EntityName = GetEntityDisplayName(nearbyEntity),
                            TextColor = GetTargetColor(nearbyType, false, GetEntityDisplayName(nearbyEntity)),
                            Health = (int)Math.Round(nearbyEntity.Health()),
                            MaxHealth = (int)Math.Round(nearbyEntity.MaxHealth()),
                            IsDestroyed = nearbyEntity.Health() <= 0 || nearbyEntity.IsDead()
                        });
                    }

                    // Add the original target first
                    targets.Insert(0, target);

                    // Show hit indicators for all targets
                    ShowHitIndicator(attacker, targets);

                    Pool.FreeList(ref nearbyEntities);
                }
                else
                {
                    // For single target, just show one indicator
                    ShowHitIndicator(attacker, new List<SplashTarget> { target });
                }

                // Show damage notification
                DamageNotifier(attacker, damage, isHead, isFriend, entityType);
            });
        }

        private bool IsImportantEntity(string entityType, BaseCombatEntity entity)
        {
            return entityType == "player" || 
                   entityType == "npc" || 
                   entity is RidableHorse2 || 
                   entityType == "tc" ||
                   entityType == "door" ||
                   entityType == "storage" ||
                   entityType == "building" ||
                   entityType == "vehicle";
        }

        // Helper method to determine entity type
        private string DetermineEntityType(BaseCombatEntity entity)
        {
            if (entity is BasePlayer) return "player";
            if (entity is NPCPlayer) return "npc";
            if (IsAnimal(entity)) return "animal";
            if (entity.ShortPrefabName?.Contains("barrel") == true || 
                entity.ShortPrefabName?.Contains("loot-barrel") == true ||
                entity.ShortPrefabName?.Contains("loot_barrel") == true) return "barrel";
            if (entity is BradleyAPC || 
                entity is BaseHelicopter ||
                entity is BaseVehicle || 
                entity.ShortPrefabName.Contains("minicopter") ||
                entity.ShortPrefabName.Contains("scraptransport") ||
                entity.ShortPrefabName.Contains("rowboat") ||
                entity.ShortPrefabName.Contains("rhib") ||
                entity.ShortPrefabName.Contains("hotairballoon") ||
                entity.ShortPrefabName.Contains("submarine") ||
                entity.ShortPrefabName.Contains("modularcar") ||
                entity.ShortPrefabName.Contains("snowmobile") ||
                entity.ShortPrefabName.Contains("workcart") ||
                entity.ShortPrefabName.Contains("tugboat") ||
                entity.ShortPrefabName.Contains("sedan") ||
                entity.ShortPrefabName.Contains("tank")) return "vehicle";
            if (entity is BuildingBlock) return "building";
            if (entity is Door) return "door";
            if (entity is StorageContainer) return "storage";
            if (entity is BuildingPrivlidge) return "tc";
            if (entity is BaseOven) return "furnace";
            if (entity is GunTrap || entity is BearTrap) return "trap";
            if (entity is Barricade) return "barricade";
            return "other";
        }

        // Helper method to get entity display name
        private string GetEntityDisplayName(BaseCombatEntity entity)
        {
            if (entity is BasePlayer) return "Player";
            if (entity is NPCPlayer) return "Scientist";
            if (IsAnimal(entity)) return GetAnimalDisplayName(entity);
            if (entity.ShortPrefabName?.Contains("barrel") == true) return GetBarrelDisplayName(entity);
            if (entity is BradleyAPC || entity is BaseHelicopter || entity is BaseVehicle) return GetVehicleDisplayName(entity);
            if (entity is AutoTurret) return "Auto Turret";
            if (entity is SamSite) return "SAM Site";
            if (entity is BuildingPrivlidge) return "TC";
            if (entity is BuildingBlock) return "Building";
            if (entity is Door) return "Door";
            if (entity is StorageContainer) return "Storage";
            if (entity is BaseOven) return "Furnace";
            if (entity is GunTrap) return "Gun Trap";
            if (entity is BearTrap) return "Bear Trap";
            if (entity is Barricade) return "Barricade";
            return entity.ShortPrefabName ?? "Unknown";
        }

        // Helper method to check if an entity is an animal
        private bool IsAnimal(BaseCombatEntity entity)
        {
            // First check if it's a building/structure - these should never be considered animals
            if (entity is BuildingPrivlidge || 
                entity is BuildingBlock || 
                entity is Door || 
                entity is StorageContainer || 
                entity is BaseOven || 
                entity is GunTrap || 
                entity is BearTrap || 
                entity is Barricade) return false;

            // Then check for animals
            if (entity is BaseAnimalNPC) return true;
            if (entity is RidableHorse2) return true;
            if (entity.ShortPrefabName?.Contains("horse") == true) return true;
            
            // Check for specific prefab names for wolves and other animals
            string prefabName = entity.ShortPrefabName?.ToLower() ?? "";
            return prefabName.Contains("wolf") || 
                   prefabName.Contains("bear") || 
                   prefabName.Contains("boar") || 
                   prefabName.Contains("stag") || 
                   prefabName.Contains("chicken") || 
                   prefabName.Contains("horse") || 
                   prefabName.Contains("deer") ||
                   prefabName.Contains("polarbear");
        }

        // Helper method to get a proper display name for animals
        private string GetAnimalDisplayName(BaseCombatEntity entity)
        {
            if (entity is RidableHorse2) return "Horse";
            
            string prefabName = entity.ShortPrefabName?.ToLower() ?? "";
            
            if (prefabName.Contains("wolf")) return "Wolf";
            if (prefabName.Contains("bear") && prefabName.Contains("polar")) return "Polar Bear";
            if (prefabName.Contains("bear")) return "Bear";
            if (prefabName.Contains("boar")) return "Boar";
            if (prefabName.Contains("stag") || prefabName.Contains("deer")) return "Deer";
            if (prefabName.Contains("chicken")) return "Chicken";
            if (prefabName.Contains("horse")) return "Horse";
            
            // Default to the short prefab name if we can't identify it
            return entity.ShortPrefabName;
        }

        // Helper method to get a proper display name for vehicles
        private string GetVehicleDisplayName(BaseCombatEntity entity)
        {
            string prefabName = entity.ShortPrefabName?.ToLower() ?? "";
            
            if (prefabName.Contains("bradley")) return "Bradley";
            if (prefabName.Contains("patrol")) return "Patrol Heli";
            if (prefabName.Contains("minicopter")) return "Mini";
            if (prefabName.Contains("scraptransport")) return "Scrap Heli";
            if (prefabName.Contains("rowboat")) return "Boat";
            if (prefabName.Contains("rhib")) return "RHIB";
            if (prefabName.Contains("hotairballoon")) return "Balloon";
            if (prefabName.Contains("submarine")) return "Sub";
            if (prefabName.Contains("modularcar")) return "Car";
            if (prefabName.Contains("snowmobile")) return "Snowmobile";
            if (prefabName.Contains("workcart")) return "Train";
            if (prefabName.Contains("tugboat")) return "Tugboat";
            if (prefabName.Contains("sedan")) return "Sedan";
            if (prefabName.Contains("tank")) return "Tank";
            
            // Default to a generic "Vehicle" if we can't identify it
            return "Vehicle";
        }

        // Helper method to get a proper display name for barrels
        private string GetBarrelDisplayName(BaseCombatEntity entity)
        {
            string prefabName = entity.ShortPrefabName?.ToLower() ?? "";
            
            if (prefabName.Contains("oil")) return "Oil Barrel";
            if (prefabName.Contains("fuel")) return "Fuel Barrel";
            if (prefabName.Contains("water")) return "Water Barrel";
            if (prefabName.Contains("blue")) return "Blue Barrel";
            if (prefabName.Contains("red")) return "Red Barrel";
            if (prefabName.Contains("yellow")) return "Yellow Barrel";
            if (prefabName.Contains("white")) return "White Barrel";
            if (prefabName.Contains("green")) return "Green Barrel";
            if (prefabName.Contains("black")) return "Black Barrel";
            if (prefabName.Contains("grey") || prefabName.Contains("gray")) return "Grey Barrel";
            if (prefabName.Contains("loot")) return "Loot Barrel";
            
            // Default to a generic "Barrel" if we can't identify it
            return "Barrel";
        }

        private string GetTargetColor(string targetType, bool isHeadshot, string entityName = "")
        {
            return targetType switch
            {
                "player" => isHeadshot ? "0.8 0.1 0.1 1" : "1 0.3 0.3 1",  // Dark red for headshots, light red for normal damage
                "npc" => isHeadshot ? "0.8 0.1 0.1 1" : "0.6 0.3 0.1 1",  // Dark red for headshots, brown for NPCs
                "animal" => isHeadshot ? "0.8 0.1 0.1 1" : "0 0.6 0 1",  // Dark red for headshots, dark green for animals
                "barrel" => "0.9 0.6 0.1 1",  // Orange for barrels
                "building" => "0.5 0.5 0.5 1",  // Gray for buildings
                "door" => "0.6 0.4 0.2 1",      // Brown for doors
                "storage" => "0.4 0.4 0.6 1",   // Blue-gray for storage
                "tc" => "0.8 0.2 0.8 1",        // Purple for Tool Cupboard
                "furnace" => "0.7 0.3 0.1 1",   // Orange-brown for furnaces
                "trap" => "0.8 0.1 0.1 1",      // Red for traps
                "barricade" => "0.4 0.4 0.4 1", // Dark gray for barricades
                "vehicle" => isHeadshot ? "0.8 0.1 0.1 1" : entityName.ToLower() switch
                {
                    "bradley" => "1 0.4 0 1",      // Orange for Bradley
                    "patrol heli" => "1 0 0 1",    // Red for patrol helicopter
                    "mini" => "0.2 0.6 1 1",       // Light blue for helicopters
                    "scrap heli" => "0.2 0.6 1 1", // Light blue for helicopters
                    "balloon" => "1 0.6 0.8 1",    // Pink for air balloons
                    "boat" => "0 0.8 1 1",         // Blue for boats
                    "rhib" => "0 0.8 1 1",         // Blue for boats
                    "car" => "0.5 0.5 0.5 1",      // Dark gray for cars
                    "sedan" => "0.5 0.5 0.5 1",    // Dark gray for cars
                    "tank" => "0.4 0.4 0.4 1",     // Darker gray for tanks
                    _ => "0.6 0.6 0.6 1"           // Gray default for vehicles
                },
                _ => "1 1 1 1"            // White default
            };
        }

        private string GetHealthBar(float currentHealth, float maxHealth, string entityType)
        {
            // Ensure health values are valid
            currentHealth = Math.Max(0, currentHealth);
            maxHealth = Math.Max(1, maxHealth);
            
            // Calculate percentage for color and bar fill only
            float healthPercent = Math.Min(1, currentHealth / maxHealth);
            
            string healthColor = healthPercent switch
            {
                var h when h > 0.7f => "#00ff00",
                var h when h > 0.3f => "#ffa500",
                _ => "#ff0000"
            };

            // Create health bar segments
            int filledSegments = (int)(healthPercent * 15);
            int emptySegments = 15 - filledSegments;
            
            string filledBar = new string('█', filledSegments);
            string emptyBar = new string('░', emptySegments);
            
            string healthBarHtml = $@"<size=14>
[<color={healthColor}>{filledBar}</color>{emptyBar}]</size>";

            // Show only current HP value, rounded to nearest integer
            return $"{healthBarHtml}\n<color={healthColor}>{(int)Math.Round(currentHealth)}</color>";
        }

        void DamageNotifier(BasePlayer player, int damage, bool isHead, bool isFriend, string entityType = "player")
        {
            if (player == null) return;

            // Don't show damage notification if damage is 0 or negative
            if (damage <= 0) return;

            List<KeyValuePair<float, HitNfo>> damages;
            if (!damageHistory.TryGetValue(player, out damages))
                damageHistory[player] = damages = new List<KeyValuePair<float, HitNfo>>();
            
            // Limit the number of simultaneous damage notifications to prevent overload
            if (damages.Count >= 5)
            {
                // Remove the oldest notification if we have too many
                if (damages.Count > 0)
                {
                    var oldest = damages[damages.Count - 1];
                    string oldNum = oldest.Value.num.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    CuiHelper.DestroyUi(player, $"hitmarkerDamageText_{oldNum}");
                    CuiHelper.DestroyUi(player, $"hitmarkerDamage_{oldNum}");
                    damages.RemoveAt(damages.Count - 1);
                }
            }
            
            // Определяем тип цели для правильного цвета
            string targetType = isFriend ? "friend" : entityType;
            
            // Generate random position around crosshair
            float randomX = 0.45f + (float)(rnd.NextDouble() - 0.5) * 0.1f;
            float randomY = 0.4f + (float)(rnd.NextDouble() - 0.5) * 0.1f;
            
            // Get color based on entity type and health percentage
            string damageColor;
            
            if (isHead)
            {
                damageColor = "#800000"; // Темно-красный для хедшотов
            }
            else if (isFriend)
            {
                damageColor = "#00ff00"; // Зеленый для друзей
            }
            else
            {
                // Match the health bar color scheme
                damageColor = entityType switch
                {
                    "player" => "#ff4d4d", // Светло-красный для игроков
                    "npc" => "#964B00",    // Коричневый для NPCs
                    "animal" => "#006400", // Темно-зеленый для животных
                    "vehicle" => "#808080",// Серый для транспорта
                    _ => "#ffffff"         // Белый по умолчанию
                };
            }
            
            var damageText = isHead ? 
                $"<color={damageColor}>⊕ -{damage}</color>" : 
                $"<color={damageColor}>-{damage}</color>";
            
            int timestamp = (int)Time.realtimeSinceStartup;
            string timestampStr = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture);
            int uniqueNum = rnd.Next(0, 10000);
            string id = uniqueNum.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string uniqueId = $"{timestampStr}_{id}";
            
            // Очищаем старые индикаторы перед показом новых (но не все, только если их слишком много)
            if (damages.Count > 10)
            {
                foreach (var oldDamage in damages.Skip(5))
                {
                    string oldNum = oldDamage.Value.num.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    CuiHelper.DestroyUi(player, $"hitmarkerDamageText_{oldNum}");
                    CuiHelper.DestroyUi(player, $"hitmarkerDamage_{oldNum}");
                }
                // Keep only the 5 newest notifications
                damages = damages.Take(5).ToList();
                damageHistory[player] = damages;
            }
            
            damages.Insert(0, new KeyValuePair<float, HitNfo>(Time.time + damageTimeout, new HitNfo 
            { 
                damage = damage, 
                isHead = isHead, 
                isFriend = isFriend,
                num = uniqueNum,
                xs = randomX,
                ys = randomY,
                xe = randomX + 0.1f,
                ye = randomY + 0.05f,
                entityType = entityType
            }));
           
            // Updated DamageGUI to use larger font size for better visibility
            string updatedDamageGUI = @"[
  {
    ""name"": ""hitmarkerDamage_{0}_{1}"",
    ""parent"": ""Overlay"",
    ""components"": [
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""{4} {5}"",
        ""anchormax"": ""{6} {7}""
      }
    ]
  },
  {
    ""name"": ""hitmarkerDamageText_{0}_{1}"",
    ""parent"": ""hitmarkerDamage_{0}_{1}"",
    ""components"": [
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""0 0"",
        ""anchormax"": ""1 1""
      },
      {
        ""type"": ""UnityEngine.UI.Text"",
        ""text"": ""{2}"",
        ""fontSize"": 20,
        ""font"": ""robotocondensed-bold.ttf"",
        ""align"": ""MiddleCenter"",
        ""color"": ""1 1 1 1""
      }
    ]
  }
]";
           
            CuiHelper.AddUi(player, HandleArgs(updatedDamageGUI, timestampStr, id, damageText, "1 1 1 1", 
                randomX.ToString(System.Globalization.CultureInfo.InvariantCulture),
                randomY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                (randomX + 0.1f).ToString(System.Globalization.CultureInfo.InvariantCulture),
                (randomY + 0.05f).ToString(System.Globalization.CultureInfo.InvariantCulture)));

            destTimers[player] = timer.Once(damageTimeout, () =>
            {
                CuiHelper.DestroyUi(player, $"hitmarkerDamageText_{uniqueId}");
                CuiHelper.DestroyUi(player, $"hitmarkerDamage_{uniqueId}");
                // Use the actual num value for removal instead of generating a new random number
                damages.RemoveAll(x => x.Value.num == uniqueNum);
            });
        }

        // New method to show only hit marker without health bar
        private void ShowHitMarkerOnly(BasePlayer player, string targetType, bool isHeadshot, string entityName = "", bool isDestroyed = false)
        {
            if (player == null) return;

            string textColor = GetTargetColor(targetType, isHeadshot, entityName);
            
            // Clear existing UI elements
            CuiHelper.DestroyUi(player, "hitmarkerActionText_hit");
            CuiHelper.DestroyUi(player, "hitmarkerAction_hit");
            CuiHelper.DestroyUi(player, "hitmarkerHealth");
            CuiHelper.DestroyUi(player, "hitmarkerHealthText");
            CuiHelper.DestroyUi(player, "hitmarkerHealthBar");
            CuiHelper.DestroyUi(player, "hitmarkerHealthFill");
            CuiHelper.DestroyUi(player, "hitmarkerHealthIcon");
            CuiHelper.DestroyUi(player, "hitmarkerHealthBorder");
            CuiHelper.DestroyUi(player, "hitmarkerHealthSegments");
            CuiHelper.DestroyUi(player, "hitmarkerHealthPercent");

            // Only show hit text if not destroyed
            if (!isDestroyed)
            {
                // Format hit text with emoji for headshot
                var hitText = targetType switch
                {
                    "player" => isHeadshot ? "⊕ HIT!" : "HIT!",
                    "npc" => isHeadshot ? "⊕ HIT!" : "HIT!",
                    "animal" => isHeadshot ? $"⊕ {entityName.ToUpper()} HIT!" : $"{entityName.ToUpper()} HIT!",
                    "vehicle" => $"{entityName.ToUpper()} HIT!",
                    "building" => $"{entityName.ToUpper()} HIT!",
                    "door" => $"{entityName.ToUpper()} HIT!",
                    "storage" => $"{entityName.ToUpper()} HIT!",
                    "tc" => $"{entityName.ToUpper()} HIT!",
                    "furnace" => $"{entityName.ToUpper()} HIT!",
                    "trap" => $"{entityName.ToUpper()} HIT!",
                    "barricade" => $"{entityName.ToUpper()} HIT!",
                    _ => "HIT!"
                };

                var hitMarkerGui = @"[
                  {
                    ""name"": ""hitmarkerAction_hit"",
                    ""parent"": ""Overlay"",
                    ""components"": [
                      {
                        ""type"": ""RectTransform"",
                        ""anchormin"": ""0.4 0.2"",
                        ""anchormax"": ""0.6 0.25""
                      }
                    ]
                  },
                  {
                    ""name"": ""hitmarkerActionText_hit"",
                    ""parent"": ""hitmarkerAction_hit"",
                    ""components"": [
                      {
                        ""type"": ""RectTransform"",
                        ""anchormin"": ""0 0"",
                        ""anchormax"": ""1 1""
                      },
                      {
                        ""type"": ""UnityEngine.UI.Text"",
                        ""text"": ""{0}"",
                        ""fontSize"": 18,
                        ""font"": ""robotocondensed-bold.ttf"",
                        ""align"": ""MiddleCenter"",
                        ""color"": ""{1}""
                      }
                    ]
                  }
                ]";

                CuiHelper.AddUi(player, HandleArgs(hitMarkerGui, hitText, textColor));
                
                // Set timer for hit message
                timer.Once(damageTimeout, () =>
                {
                    CuiHelper.DestroyUi(player, "hitmarkerActionText_hit");
                    CuiHelper.DestroyUi(player, "hitmarkerAction_hit");
                });
            }

            // Show destroyed/killed message if entity is destroyed
            if (isDestroyed)
            {
                string destroyedText = targetType switch
                {
                    "building" => $"{entityName.ToUpper()} DESTROYED",
                    "door" => $"{entityName.ToUpper()} DESTROYED",
                    "storage" => $"{entityName.ToUpper()} DESTROYED",
                    "tc" => $"{entityName.ToUpper()} DESTROYED",
                    "furnace" => $"{entityName.ToUpper()} DESTROYED",
                    "trap" => $"{entityName.ToUpper()} DESTROYED",
                    "barricade" => $"{entityName.ToUpper()} DESTROYED",
                    _ => "KILLED"
                };

                string killedBarGui = @"[
                  {
                    ""name"": ""hitmarkerHealth"",
                    ""parent"": ""Overlay"",
                    ""components"": [
                      {
                        ""type"": ""RectTransform"",
                        ""anchormin"": ""0.4 0.15"",
                        ""anchormax"": ""0.6 0.2""
                      }
                    ]
                  },
                  {
                    ""name"": ""hitmarkerHealthBorder"",
                    ""parent"": ""hitmarkerHealth"",
                    ""components"": [
                      {
                        ""type"": ""RectTransform"",
                        ""anchormin"": ""0.05 0.2"",
                        ""anchormax"": ""0.95 0.8""
                      },
                      {
                        ""type"": ""UnityEngine.UI.Image"",
                        ""color"": ""0.12 0.12 0.12 0.95"",
                        ""material"": ""assets/content/ui/ui.background.transparent.radial.psd"",
                        ""sprite"": ""assets/content/ui/ui.background.transparent.radial.psd""
                      }
                    ]
                  },
                  {
                    ""name"": ""hitmarkerHealthBar"",
                    ""parent"": ""hitmarkerHealthBorder"",
                    ""components"": [
                      {
                        ""type"": ""RectTransform"",
                        ""anchormin"": ""0.01 0.1"",
                        ""anchormax"": ""0.99 0.9""
                      },
                      {
                        ""type"": ""UnityEngine.UI.Image"",
                        ""color"": ""0.08 0.08 0.08 0.9"",
                        ""material"": ""assets/content/ui/ui.background.transparent.radial.psd"",
                        ""sprite"": ""assets/content/ui/ui.background.transparent.radial.psd""
                      }
                    ]
                  },
                  {
                    ""name"": ""hitmarkerHealthIcon"",
                    ""parent"": ""hitmarkerHealth"",
                    ""components"": [
                      {
                        ""type"": ""RectTransform"",
                        ""anchormin"": ""0 0.2"",
                        ""anchormax"": ""0.1 0.8""
                      },
                      {
                        ""type"": ""UnityEngine.UI.Text"",
                        ""text"": ""☠"",
                        ""fontSize"": 22,
                        ""font"": ""robotocondensed-bold.ttf"",
                        ""align"": ""MiddleCenter"",
                        ""color"": ""1 0 0 1""
                      }
                    ]
                  },
                  {
                    ""name"": ""hitmarkerHealthPercent"",
                    ""parent"": ""hitmarkerHealth"",
                    ""components"": [
                      {
                        ""type"": ""RectTransform"",
                        ""anchormin"": ""0.1 0"",
                        ""anchormax"": ""0.9 1""
                      },
                      {
                        ""type"": ""UnityEngine.UI.Text"",
                        ""text"": ""{0}"",
                        ""fontSize"": 16,
                        ""font"": ""robotocondensed-bold.ttf"",
                        ""align"": ""MiddleCenter"",
                        ""color"": ""1 0 0 1""
                      }
                    ]
                  }
                ]";

                CuiHelper.AddUi(player, HandleArgs(killedBarGui, destroyedText));

                // Set longer timer for destroyed message
                timer.Once(destroyedTimeout, () =>
                {
                    CuiHelper.DestroyUi(player, "hitmarkerHealth");
                    CuiHelper.DestroyUi(player, "hitmarkerHealthIcon");
                    CuiHelper.DestroyUi(player, "hitmarkerHealthBorder");
                    CuiHelper.DestroyUi(player, "hitmarkerHealthBar");
                    CuiHelper.DestroyUi(player, "hitmarkerHealthPercent");
                });
            }
        }

        private string GetHealthBarColor(float healthPercent)
        {
            if (healthPercent > 0.7f) return "0 0.8 0 0.9";  // Green
            if (healthPercent > 0.3f) return "1 0.65 0 0.9"; // Orange
            return "1 0 0 0.9";                              // Red
        }

        private void ShowHitIndicator(BasePlayer player, List<SplashTarget> targets)
        {
            if (player == null || targets == null || targets.Count == 0) return;

            // Clear existing UI elements for all possible positions
            for (int i = 0; i < 4; i++)
            {
                string suffix = $"_{i}";
                CuiHelper.DestroyUi(player, "hitmarkerHealth" + suffix);
                CuiHelper.DestroyUi(player, "hitmarkerHealthText" + suffix);
                CuiHelper.DestroyUi(player, "hitmarkerHealthBar" + suffix);
                CuiHelper.DestroyUi(player, "hitmarkerHealthFill" + suffix);
                CuiHelper.DestroyUi(player, "hitmarkerHealthBorder" + suffix);
            }

            // Grid layout positions (2x2)
            var positions = new[]
            {
                (x: 0.35f, y: 0.15f),  // Bottom left
                (x: 0.55f, y: 0.15f),  // Bottom right
                (x: 0.35f, y: 0.21f),  // Top left
                (x: 0.55f, y: 0.21f)   // Top right
            };

            // Show only as many health bars as there are targets (max 4)
            int numBars = Math.Min(targets.Count, 4);
            
            for (int i = 0; i < numBars; i++)
            {
                var target = targets[i];
                var pos = positions[i];
                string suffix = $"_{i}";

                float healthPercent = Math.Min(1, (float)target.Health / target.MaxHealth);
                string healthColor = GetHealthBarColor(healthPercent);

                // Format display text based on entity type and state
                string displayText;
                if (target.IsDestroyed)
                {
                    displayText = target.EntityType switch
                    {
                        "building" => $"{target.EntityName} DESTROYED",
                        "door" => $"{target.EntityName} DESTROYED",
                        "storage" => $"{target.EntityName} DESTROYED",
                        "tc" => $"{target.EntityName} DESTROYED",
                        "furnace" => $"{target.EntityName} DESTROYED",
                        "trap" => $"{target.EntityName} DESTROYED",
                        "barricade" => $"{target.EntityName} DESTROYED",
                        _ => "KILLED"
                    };
                }
                else
                {
                    displayText = target.EntityType switch
                    {
                        "player" => $"{target.Health} HP",
                        "npc" => $"Scientist {target.Health} HP",
                        "animal" => $"{target.EntityName} {target.Health} HP",
                        "vehicle" => $"{target.EntityName} {target.Health} HP",
                        "building" => $"{target.EntityName} {target.Health} HP",
                        "door" => $"{target.EntityName} {target.Health} HP",
                        "storage" => $"{target.EntityName} {target.Health} HP",
                        "tc" => $"{target.EntityName} {target.Health} HP",
                        "furnace" => $"{target.EntityName} {target.Health} HP",
                        "trap" => $"{target.EntityName} {target.Health} HP",
                        "barricade" => $"{target.EntityName} {target.Health} HP",
                        _ => $"{target.Health} HP"
                    };
                }

                string healthBarGui = $@"[
                  {{
                    ""name"": ""hitmarkerHealth{suffix}"",
                    ""parent"": ""Overlay"",
                    ""components"": [
                      {{
                        ""type"": ""RectTransform"",
                        ""anchormin"": ""{pos.x} {pos.y}"",
                        ""anchormax"": ""{pos.x + 0.15f} {pos.y + 0.05f}""
                      }}
                    ]
                  }},
                  {{
                    ""name"": ""hitmarkerHealthBorder{suffix}"",
                    ""parent"": ""hitmarkerHealth{suffix}"",
                    ""components"": [
                      {{
                        ""type"": ""RectTransform"",
                        ""anchormin"": ""0.05 0.2"",
                        ""anchormax"": ""0.95 0.8""
                      }},
                      {{
                        ""type"": ""UnityEngine.UI.Image"",
                        ""color"": ""0.12 0.12 0.12 0.95"",
                        ""material"": ""assets/content/ui/ui.background.transparent.radial.psd"",
                        ""sprite"": ""assets/content/ui/ui.background.transparent.radial.psd""
                      }}
                    ]
                  }},
                  {{
                    ""name"": ""hitmarkerHealthBar{suffix}"",
                    ""parent"": ""hitmarkerHealthBorder{suffix}"",
                    ""components"": [
                      {{
                        ""type"": ""RectTransform"",
                        ""anchormin"": ""0.01 0.1"",
                        ""anchormax"": ""0.99 0.9""
                      }},
                      {{
                        ""type"": ""UnityEngine.UI.Image"",
                        ""color"": ""0.08 0.08 0.08 0.9"",
                        ""material"": ""assets/content/ui/ui.background.transparent.radial.psd"",
                        ""sprite"": ""assets/content/ui/ui.background.transparent.radial.psd""
                      }}
                    ]
                  }},
                  {{
                    ""name"": ""hitmarkerHealthFill{suffix}"",
                    ""parent"": ""hitmarkerHealthBar{suffix}"",
                    ""components"": [
                      {{
                        ""type"": ""RectTransform"",
                        ""anchormin"": ""0 0"",
                        ""anchormax"": ""{healthPercent} 1""
                      }},
                      {{
                        ""type"": ""UnityEngine.UI.Image"",
                        ""color"": ""{healthColor}"",
                        ""material"": ""assets/content/ui/ui.background.transparent.radial.psd"",
                        ""sprite"": ""assets/content/ui/ui.background.transparent.radial.psd""
                      }}
                    ]
                  }},
                  {{
                    ""name"": ""hitmarkerHealthText{suffix}"",
                    ""parent"": ""hitmarkerHealth{suffix}"",
                    ""components"": [
                      {{
                        ""type"": ""RectTransform"",
                        ""anchormin"": ""0.1 0"",
                        ""anchormax"": ""0.9 1""
                      }},
                      {{
                        ""type"": ""UnityEngine.UI.Text"",
                        ""text"": ""{displayText}"",
                        ""fontSize"": 14,
                        ""font"": ""robotocondensed-bold.ttf"",
                        ""align"": ""MiddleCenter"",
                        ""color"": ""{(target.IsDestroyed ? "1 0 0 1" : healthColor)}""
                      }}
                    ]
                  }}
                ]";

                CuiHelper.AddUi(player, healthBarGui);
            }

            // Set timer to remove all UI elements after 5 seconds
            timer.Once(5.0f, () =>
            {
                for (int i = 0; i < numBars; i++)
                {
                    string suffix = $"_{i}";
                    CuiHelper.DestroyUi(player, "hitmarkerHealth" + suffix);
                    CuiHelper.DestroyUi(player, "hitmarkerHealthText" + suffix);
                    CuiHelper.DestroyUi(player, "hitmarkerHealthBar" + suffix);
                    CuiHelper.DestroyUi(player, "hitmarkerHealthFill" + suffix);
                    CuiHelper.DestroyUi(player, "hitmarkerHealthBorder" + suffix);
                }
            });
        }

        void OnDamageTimer()
        {            
            var toRemove = Pool.Get<List<BasePlayer>>(); 
            
            // Process only a limited number of players per tick to reduce server load
            int processedPlayers = 0;
            int maxPlayersPerTick = 5; // Limit the number of players processed per tick
            
            foreach (var dmgHistoryKVP in damageHistory)
            {
                // Skip if we've processed enough players this tick
                if (processedPlayers >= maxPlayersPerTick) break;
                
                DrawDamageNotifier(dmgHistoryKVP.Key);
                if (dmgHistoryKVP.Value.Count == 0)
                    toRemove.Add(dmgHistoryKVP.Key);
                
                processedPlayers++;
            }
            
            toRemove.ForEach(p=>damageHistory.Remove(p));
            Pool.FreeList(ref toRemove);
        }

        void DestroyLastCui(BasePlayer player)
        {
            Oxide.Plugins.Timer tmr;
            if (destTimers.TryGetValue(player, out tmr))
            {
                tmr?.Callback?.Invoke();
                if (tmr != null && !tmr.Destroyed)
                    timer.Destroy(ref tmr);
            }
        }
        
        void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null) return;
            var attacker = hitInfo.InitiatorPlayer;
            if (attacker == null || !hitmarkeron.Contains(attacker)) return;
            
            // Skip construction entities that shouldn't show destruction message
            if (entity is SimpleBuildingBlock) return;
            
            var isHead = hitInfo.isHeadshot;
            bool isFriend = false;
            
            // Check if target is a player and if they're a friend
            if (entity is BasePlayer targetPlayer)
            {
                isFriend = (Clans?.Call("HasFriend", attacker.userID, targetPlayer.userID) as bool?) ?? false;
            }
            
            // Determine entity type
            string entityType = DetermineEntityType(entity);

            // Get damage amount, ensure it's at least 1 for death notifications
            int damage = (int)hitInfo.damageTypes.Total();
            if (damage <= 0) damage = 1;
            
            // Check if this is a splash damage kill (multiple entities killed at once)
            bool isSplashDamage = hitInfo.damageTypes.Has(Rust.DamageType.Explosion) || 
                                 hitInfo.damageTypes.Has(Rust.DamageType.Heat) ||
                                 hitInfo.damageTypes.Has(Rust.DamageType.ElectricShock);
            
            // For splash damage, only show notifications for important entities
            if (isSplashDamage)
            {
                // Skip less important entities during splash damage to reduce load
                if (entityType == "animal" && !(entity is RidableHorse2)) return;
                if (entityType == "npc" && !entity.ShortPrefabName.Contains("scientist")) return;
                
                // For remaining entities, use a simpler notification
                DamageNotifier(attacker, damage, isHead, isFriend, entityType);
                
                // Only show health bar for players and important entities
                if (entityType == "player" || entity is RidableHorse2 || entity.ShortPrefabName.Contains("scientist"))
                {
                    if (entityType == "player")
                        ShowHitMarkerOnly(attacker, "player", isHead);
                    else if (entityType == "npc")
                        ShowHitMarkerOnly(attacker, "npc", isHead);
                    else if (entityType == "animal")
                    {
                        string entityName = GetAnimalDisplayName(entity);
                        ShowHitMarkerOnly(attacker, "animal", isHead, entityName);
                    }
                }
                return;
            }
            
            // For non-splash damage kills, show full notifications
            // Show appropriate death notification based on entity type
            if (entity is BasePlayer)
            {
                // Show death notification
                DamageNotifier(attacker, damage, isHead, isFriend, entityType);
                ShowHitMarkerOnly(attacker, "player", isHead);
            }
            else if (entity is NPCPlayer)
            {
                // Special handling for scientists
                bool isScientist = entity.ShortPrefabName?.Contains("scientist") == true || 
                                  entity.ShortPrefabName?.Contains("npc") == true;
                
                // Ensure we always show KILLED for scientists
                if (isScientist)
                {
                    // Show death notification with guaranteed damage
                    DamageNotifier(attacker, Math.Max(damage, 50), isHead, false, entityType);
                    ShowHitMarkerOnly(attacker, "npc", isHead);
                }
                else
                {
                    // Show regular death notification
                    DamageNotifier(attacker, damage, isHead, false, entityType);
                    ShowHitMarkerOnly(attacker, "npc", isHead);
                }
            }
            else if (IsAnimal(entity))
            {
                // Get a proper display name for the animal
                string entityName = GetAnimalDisplayName(entity);
                
                // Show death notification
                DamageNotifier(attacker, damage, isHead, false, entityType);
                ShowHitMarkerOnly(attacker, "animal", isHead, entityName);
            }
            else if (entity.ShortPrefabName?.Contains("barrel") == true || 
                    entity.ShortPrefabName?.Contains("loot-barrel") == true ||
                    entity.ShortPrefabName?.Contains("loot_barrel") == true)
            {
                // Special handling for barrels
                string barrelName = GetBarrelDisplayName(entity);
                
                // Show death notification
                DamageNotifier(attacker, damage, false, false, entityType);
                ShowHitMarkerOnly(attacker, "barrel", false, barrelName);
            }
            else if (entity is BradleyAPC || 
                    entity is BaseHelicopter ||
                    entity is BaseVehicle || 
                    entity.ShortPrefabName.Contains("minicopter") ||
                    entity.ShortPrefabName.Contains("scraptransport") ||
                    entity.ShortPrefabName.Contains("rowboat") ||
                    entity.ShortPrefabName.Contains("rhib") ||
                    entity.ShortPrefabName.Contains("hotairballoon") ||
                    entity.ShortPrefabName.Contains("submarine") ||
                    entity.ShortPrefabName.Contains("modularcar") ||
                    entity.ShortPrefabName.Contains("snowmobile") ||
                    entity.ShortPrefabName.Contains("workcart") ||
                    entity.ShortPrefabName.Contains("tugboat") ||
                    entity.ShortPrefabName.Contains("sedan") ||
                    entity.ShortPrefabName.Contains("tank"))
            {
                // Show death notification
                DamageNotifier(attacker, damage, false, false, entityType);
                ShowHitMarkerOnly(attacker, "vehicle", false, GetVehicleDisplayName(entity));
            }
            else if (entity is BuildingPrivlidge)
            {
                // Special handling for TC
                DamageNotifier(attacker, damage, false, false, "tc");
                ShowHitMarkerOnly(attacker, "tc", false, "TC", true);
            }
            else if (entity is BuildingBlock)
            {
                DamageNotifier(attacker, damage, false, false, "building");
                ShowHitMarkerOnly(attacker, "building", false, "Building", true);
            }
            else if (entity is Door)
            {
                DamageNotifier(attacker, damage, false, false, "door");
                ShowHitMarkerOnly(attacker, "door", false, "Door", true);
            }
            else if (entity is StorageContainer)
            {
                DamageNotifier(attacker, damage, false, false, "storage");
                ShowHitMarkerOnly(attacker, "storage", false, "Storage", true);
            }
            else if (entity is BaseOven)
            {
                DamageNotifier(attacker, damage, false, false, "furnace");
                ShowHitMarkerOnly(attacker, "furnace", false, "Furnace", true);
            }
            else if (entity is GunTrap || entity is BearTrap)
            {
                DamageNotifier(attacker, damage, false, false, "trap");
                ShowHitMarkerOnly(attacker, "trap", false, entity is GunTrap ? "Gun Trap" : "Bear Trap", true);
            }
            else if (entity is Barricade)
            {
                DamageNotifier(attacker, damage, false, false, "barricade");
                ShowHitMarkerOnly(attacker, "barricade", false, "Barricade", true);
            }
            else
            {
                // For all other entities
                string entityName = entity.ShortPrefabName;
                if (entity is AutoTurret) entityName = "Auto Turret";
                if (entity is SamSite) entityName = "SAM Site";
                
                // Show death notification
                DamageNotifier(attacker, damage, false, false, "other");
                ShowHitMarkerOnly(attacker, "other", false, entityName, true);
            }
        }
        
        #endregion

        #region UI
		
        void DrawDamageNotifier(BasePlayer player)
        {						
            if (player == null) return;

            List<KeyValuePair<float, HitNfo>> damages;
            if (!damageHistory.TryGetValue(player, out damages)) return;
            
            // Skip if there are too many notifications to prevent overload
            if (damages.Count > 20)
            {
                // Clean up excess notifications
                for (int i = 10; i < damages.Count; i++)
                {
                    var item = damages[i];
                    CuiHelper.DestroyUi(player, $"hitmarkerDamageText_{item.Value.num}");
                    CuiHelper.DestroyUi(player, $"hitmarkerDamage_{item.Value.num}");
                }
                
                // Keep only the 10 newest notifications
                damages = damages.Take(10).ToList();
                damageHistory[player] = damages;
            }
			
            float time = Time.time;			
            for (var i = damages.Count-1; i >= 0; i--)
            {
                var item = damages[i];
                int timestamp = (int)Time.realtimeSinceStartup;
                string timestampStr = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string uniqueId = $"{timestampStr}_{item.Value.num}";

                // Clean up old UI first
                CuiHelper.DestroyUi(player, $"hitmarkerDamageText_{uniqueId}");
                CuiHelper.DestroyUi(player, $"hitmarkerDamage_{uniqueId}");

                if (item.Key < time)
                    damages.RemoveAt(i);
                else
                {
                    // Get color based on entity type and headshot status
                    string damageColor;
                    
                    if (item.Value.isHead)
                    {
                        damageColor = "#800000"; // Темно-красный для хедшотов
                    }
                    else if (item.Value.isFriend)
                    {
                        damageColor = "#00ff00"; // Зеленый для друзей
                    }
                    else
                    {
                        // Используем тип сущности из HitNfo
                        damageColor = item.Value.entityType switch
                        {
                            "player" => "#ff4d4d", // Светло-красный для игроков
                            "npc" => "#964B00",    // Коричневый для NPCs
                            "animal" => "#006400", // Темно-зеленый для животных
                            "vehicle" => "#808080",// Серый для транспорта
                            _ => "#ffffff"         // Белый по умолчанию
                        };
                    }
                    
                    string damageText = item.Value.isHead ? 
                        $"<color={damageColor}>⊕ -{item.Value.damage}</color>" : 
                        $"<color={damageColor}>-{item.Value.damage}</color>";
                    
                    // Updated DamageGUI to use larger font size for better visibility
                    string updatedDamageGUI = @"[
  {
    ""name"": ""hitmarkerDamage_{0}_{1}"",
    ""parent"": ""Overlay"",
    ""components"": [
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""{4} {5}"",
        ""anchormax"": ""{6} {7}""
      }
    ]
  },
  {
    ""name"": ""hitmarkerDamageText_{0}_{1}"",
    ""parent"": ""hitmarkerDamage_{0}_{1}"",
    ""components"": [
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""0 0"",
        ""anchormax"": ""1 1""
      },
      {
        ""type"": ""UnityEngine.UI.Text"",
        ""text"": ""{2}"",
        ""fontSize"": 20,
        ""font"": ""robotocondensed-bold.ttf"",
        ""align"": ""MiddleCenter"",
        ""color"": ""1 1 1 1""
      }
    ]
  }
]";
                    
                    CuiHelper.AddUi(player, HandleArgs(updatedDamageGUI, timestampStr, item.Value.num.ToString(System.Globalization.CultureInfo.InvariantCulture), 
                        damageText,
                        "1 1 1 1",
                        item.Value.xs.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        item.Value.ys.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        item.Value.xe.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        item.Value.ye.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                    
                    destTimers[player] = timer.Once(damageTimeout, () =>
                    {
                        CuiHelper.DestroyUi(player, $"hitmarkerDamageText_{uniqueId}");
                        CuiHelper.DestroyUi(player, $"hitmarkerDamage_{uniqueId}");
                    });
                }
            }			            
        }        

        #endregion
    }
}
