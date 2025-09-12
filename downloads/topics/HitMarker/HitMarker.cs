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
        #region CONFIGURATION

        private bool Changed;
        private bool enablesound;
        private string? soundeffect;
        private string? headshotsoundeffect;
        private float damageTimeout;
		
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
            GetVariable(Config, "Через сколько будет пропадать урон", out damageTimeout, 0.5f);
            
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
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            // Initial checks
            if (entity == null || hitInfo == null) return;
            var attacker = hitInfo.InitiatorPlayer;
            if (attacker == null || !hitmarkeron.Contains(attacker)) return;
            
            var isHead = hitInfo.isHeadshot;
            bool isFriend = false;
            
            // Skip construction entities
            if (entity is BuildingBlock || entity is Door || entity is StorageContainer || 
                entity is BuildingPrivlidge || entity is BaseOven || 
                entity is GunTrap || entity is BearTrap || 
                entity is Barricade || entity is SimpleBuildingBlock) return;

            // Check for friend
            if (entity is BasePlayer targetPlayer)
            {
                isFriend = (Clans?.Call("HasFriend", attacker.userID, targetPlayer.userID) as bool?) ?? false;
            }

            // Store pre-hit values
            float preHealth = entity.Health();
            float maxHealth = entity.MaxHealth();

            NextTick(() =>
            {
                // Get post-hit values
                float postHealth = entity.Health();
                
                // Calculate actual damage dealt (difference between pre and post health)
                float actualDamage = Math.Max(0, preHealth - postHealth);
                
                // Cap the displayed damage at the pre-hit health value
                // This ensures we never show more damage than the entity had health
                float cappedDamage = Math.Min(actualDamage, preHealth);
                int damage = (int)Math.Round(cappedDamage);
                
                // Skip if no real damage was dealt
                if (damage <= 0) return;
                
                // Calculate health values - use actual values, not percentages
                int currentHealth = (int)Math.Round(postHealth);
                int maxHealthInt = (int)Math.Round(maxHealth);
                
                bool isDead = currentHealth <= 0 || entity.IsDead();
                
                // Determine if this was a killing blow
                bool isKillingBlow = isDead || currentHealth <= 0;
                
                // Special handling for NPCs
                if (entity is NPCPlayer)
                {
                    bool isScientist = entity.ShortPrefabName?.Contains("scientist") == true;
                    if (isScientist && currentHealth > 0)
                    {
                        isKillingBlow = false;
                    }
                }
                
                // Check for corpse/harvestable
                bool isCorpse = entity.ShortPrefabName?.Contains("corpse") ?? false;
                bool isHarvestable = false;
                
                if (isCorpse || isDead)
                {
                    isHarvestable = hitInfo.damageTypes.Has(Rust.DamageType.Generic) || 
                                   hitInfo.damageTypes.Has(Rust.DamageType.Blunt) ||
                                   hitInfo.damageTypes.Has(Rust.DamageType.Slash);
                    
                    if (!isHarvestable) return;
                }

                // Check for tool hits
                bool isToolHit = hitInfo.WeaponPrefab != null && 
                                (hitInfo.damageTypes.Has(Rust.DamageType.Generic) ||
                                 hitInfo.damageTypes.Has(Rust.DamageType.Blunt) ||
                                 hitInfo.damageTypes.Has(Rust.DamageType.Slash));

                // Determine entity type
                string entityType = DetermineEntityType(entity);

                // Show damage notification with capped damage value
                if (damage > 0 || isToolHit)
                {
                    DamageNotifier(attacker, damage, isHead, isFriend, entityType);
                }
                
                // Show appropriate hit indicator with actual health values
                if (isDead || isKillingBlow)
                {
                    ShowHitMarkerOnly(attacker, entityType, isHead, GetEntityDisplayName(entity));
                }
                else
                {
                    ShowHitIndicator(attacker, entityType, isHead, GetEntityDisplayName(entity), currentHealth, maxHealthInt);
                }
            });
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
            return entity.ShortPrefabName ?? "Unknown";
        }

        // Helper method to check if an entity is an animal
        private bool IsAnimal(BaseCombatEntity entity)
        {
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
                "player" => isHeadshot ? "0.8 0.1 0.1 1" : "1 0.3 0.3 1",  // Темно-красный для хедшотов, светло-красный для обычного урона
                "npc" => isHeadshot ? "0.8 0.1 0.1 1" : "0.6 0.3 0.1 1",  // Темно-красный для хедшотов, коричневый для NPC
                "animal" => isHeadshot ? "0.8 0.1 0.1 1" : "0 0.6 0 1",  // Темно-красный для хедшотов, темно-зеленый для животных
                "barrel" => "0.9 0.6 0.1 1",  // Оранжевый для бочек
                "vehicle" => isHeadshot ? "0.8 0.1 0.1 1" : entityName.ToLower() switch
                {
                    "bradley" => "1 0.4 0 1",      // Оранжевый для Bradley
                    "patrol heli" => "1 0 0 1",    // Красный для патрульного вертолета
                    "mini" => "0.2 0.6 1 1",       // Голубой для вертолетов
                    "scrap heli" => "0.2 0.6 1 1", // Голубой для вертолетов
                    "balloon" => "1 0.6 0.8 1",    // Розовый для воздушных шаров
                    "boat" => "0 0.8 1 1",         // Синий для лодок
                    "rhib" => "0 0.8 1 1",         // Синий для лодок
                    "car" => "0.5 0.5 0.5 1",      // Темно-серый для машин
                    "sedan" => "0.5 0.5 0.5 1",    // Темно-серый для машин
                    "tank" => "0.4 0.4 0.4 1",     // Еще более темно-серый для танков
                    _ => "0.6 0.6 0.6 1"           // Серый по умолчанию для транспорта
                },
                _ => "1 1 1 1"            // Белый по умолчанию
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
        private void ShowHitMarkerOnly(BasePlayer player, string targetType, bool isHeadshot, string entityName = "")
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

            // Format hit text with emoji for headshot
            var hitText = targetType switch
            {
                "player" => isHeadshot ? "⊕ HIT!" : "HIT!",
                "npc" => isHeadshot ? "⊕ HIT!" : "HIT!",
                "animal" => isHeadshot ? $"⊕ {entityName.ToUpper()} HIT!" : $"{entityName.ToUpper()} HIT!",
                "vehicle" => $"{entityName.ToUpper()} HIT!",
                _ => "HIT!"
            };

            // Updated hit marker GUI with better styling
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

            // Updated killed message UI with rounded corners and better styling
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
                    ""text"": ""KILLED"",
                    ""fontSize"": 16,
                    ""font"": ""robotocondensed-bold.ttf"",
                    ""align"": ""MiddleCenter"",
                    ""color"": ""1 0 0 1""
                  }
                ]
              }
            ]";

            CuiHelper.AddUi(player, killedBarGui);

            // Set synchronized timers for UI removal
            timer.Once(damageTimeout, () =>
            {
                CuiHelper.DestroyUi(player, "hitmarkerActionText_hit");
                CuiHelper.DestroyUi(player, "hitmarkerAction_hit");
                CuiHelper.DestroyUi(player, "hitmarkerHealth");
                CuiHelper.DestroyUi(player, "hitmarkerHealthIcon");
                CuiHelper.DestroyUi(player, "hitmarkerHealthBorder");
                CuiHelper.DestroyUi(player, "hitmarkerHealthBar");
                CuiHelper.DestroyUi(player, "hitmarkerHealthPercent");
            });
        }

        private string GetHealthBarColor(float healthPercent)
        {
            if (healthPercent > 0.7f) return "0 0.8 0 0.9";  // Green
            if (healthPercent > 0.3f) return "1 0.65 0 0.9"; // Orange
            return "1 0 0 0.9";                              // Red
        }

        private void ShowHitIndicator(BasePlayer player, string targetType, bool isHeadshot, string entityName, int health, int maxHealth)
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

            // Format hit text
            var hitText = targetType switch
            {
                "player" => isHeadshot ? "⊕ HIT!" : "HIT!",
                "npc" => isHeadshot ? "⊕ HIT!" : "HIT!",
                "animal" => isHeadshot ? $"⊕ {entityName.ToUpper()} HIT!" : $"{entityName.ToUpper()} HIT!",
                "vehicle" => $"{entityName.ToUpper()} HIT!",
                _ => "HIT!"
            };

            // Updated hit marker GUI with better styling
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

            // Show health bar if entity has health
            if (health > 0)
            {
                float healthPercent = Math.Min(1, (float)health / maxHealth);
                string healthColor = GetHealthBarColor(healthPercent);

                // Updated health bar GUI with rounded corners and better styling
                string healthBarGui = @"[
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
                    ""name"": ""hitmarkerHealthFill"",
                    ""parent"": ""hitmarkerHealthBar"",
                    ""components"": [
                      {
                        ""type"": ""RectTransform"",
                        ""anchormin"": ""0 0"",
                        ""anchormax"": ""{0} 1""
                      },
                      {
                        ""type"": ""UnityEngine.UI.Image"",
                        ""color"": ""{1}"",
                        ""material"": ""assets/content/ui/ui.background.transparent.radial.psd"",
                        ""sprite"": ""assets/content/ui/ui.background.transparent.radial.psd""
                      }
                    ]
                  },
                  {
                    ""name"": ""hitmarkerHealthText"",
                    ""parent"": ""hitmarkerHealth"",
                    ""components"": [
                      {
                        ""type"": ""RectTransform"",
                        ""anchormin"": ""0.1 0"",
                        ""anchormax"": ""0.9 1""
                      },
                      {
                        ""type"": ""UnityEngine.UI.Text"",
                        ""text"": ""{2} HP"",
                        ""fontSize"": 16,
                        ""font"": ""robotocondensed-bold.ttf"",
                        ""align"": ""MiddleCenter"",
                        ""color"": ""{1}""
                      }
                    ]
                  }
                ]";

                CuiHelper.AddUi(player, HandleArgs(healthBarGui, 
                    healthPercent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    healthColor,
                    health.ToString(System.Globalization.CultureInfo.InvariantCulture)));

                // Set synchronized timers for UI removal
                timer.Once(5f, () =>
                {
                    CuiHelper.DestroyUi(player, "hitmarkerHealth");
                    CuiHelper.DestroyUi(player, "hitmarkerHealthText");
                    CuiHelper.DestroyUi(player, "hitmarkerHealthBar");
                    CuiHelper.DestroyUi(player, "hitmarkerHealthFill");
                    CuiHelper.DestroyUi(player, "hitmarkerHealthBorder");
                });
            }

            // Set timer to remove hit marker
            timer.Once(damageTimeout, () =>
            {
                CuiHelper.DestroyUi(player, "hitmarkerActionText_hit");
                CuiHelper.DestroyUi(player, "hitmarkerAction_hit");
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
            
            // Skip construction entities
            if (entity is BuildingBlock || entity is Door || entity is StorageContainer || 
                entity is BuildingPrivlidge || entity is BaseOven || 
                entity is GunTrap || entity is BearTrap || 
                entity is Barricade || entity is SimpleBuildingBlock) return;
                
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
                // Специальная обработка для бочек
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
            else
            {
                // Для всех остальных сущностей (включая турели и зенитки)
                string entityName = entity.ShortPrefabName;
                if (entity is AutoTurret) entityName = "Auto Turret";
                if (entity is SamSite) entityName = "SAM Site";
                
                // Show death notification
                DamageNotifier(attacker, damage, false, false, "other");
                ShowHitMarkerOnly(attacker, "other", false, entityName);
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
