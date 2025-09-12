// #define DEBUG

using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("kryIncreaseWeapons", "xkrystalll", "1.1.16")]
    class kryIncreaseWeapons : RustPlugin
    {
        #region Configuration
        public static ConfigData cfg;
        public class ConfigData
        {
            [JsonProperty("Increased weapon")]
            public Dictionary<string, WeaponsInfo> weapons;
            [JsonProperty("Blocked for repair")]
            public List<string> blockedShortnames;
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData()
            {
                weapons = new Dictionary<string, WeaponsInfo>()
                {
                    {"aktest", new WeaponsInfo("АК-47", 2323019876, 10f, 0f, "rifle.ak", 20, true, false, true, true, true)},
                    {"lrtest", new WeaponsInfo("M4", 2319796265, 0.1f, 10f, "rifle.lr300", 5, false, true, false, false, true)}
                },
                blockedShortnames = new List<string>()
                {
                    "rifle.lr300",
                    "rifle.m39"
                }
            };
            SaveConfig(config);
        }
        void LoadConfig()
        {
            cfg = Config.ReadObject<ConfigData>();
            SaveConfig(cfg);
        }
        void SaveConfig(object config) => Config.WriteObject(config, true);
        #endregion

        #region Fields
        private List<uint> _createdWeapons = new List<uint>();
        // private List<uint> _netIDsWeapons = new List<uint>();
        private bool isReady = false;
        public List<WeaponsInfo> weaponsVal;
        public List<string> weaponsKeys;
        public class WeaponsInfo
        {
            public WeaponsInfo(string name, ulong skin, float dmulti, float cmulti, string shortname, int sizeOfMagazine, bool maybeRepair, bool deleteOnBreak, bool entityMultiplier, bool botsMultiplier, bool IsInfiniteBullets)
            {
                this.shortname = shortname;
                this.name = name;
                this.skin = skin;
                this.damageMultiplier = dmulti;
                this.conditionMultiplier = cmulti;
                this.deleteOnBreak = deleteOnBreak;
                this.maybeRepair = maybeRepair;
                this.entityMultiplier = entityMultiplier;
                this.botsMultiplier = botsMultiplier;
                this.sizeOfMagazine = sizeOfMagazine;
                this.IsInfiniteBullets = IsInfiniteBullets;
            }
            [JsonProperty("Shortname")]
            public string shortname;
            [JsonProperty("Weapon name")]
            public string name;
            [JsonProperty("Weapon skin (IMPORTANT!!! Must be unique)")]
            public ulong skin;
            [JsonProperty("Damage multiplier")]
            public float damageMultiplier;
            [JsonProperty("Weapon breakage multiplier")]
            public float conditionMultiplier;
            [JsonProperty("Can repair?")]
            public bool maybeRepair;
            [JsonProperty("Delete on break?")]
            public bool deleteOnBreak;
            [JsonProperty("Multiply damage on buildings?")]
            public bool entityMultiplier;
            [JsonProperty("Multiply damage on bots?")]
            public bool botsMultiplier;
            [JsonProperty("How many rounds can there be in the magazine? (0 if you don't need to change it)")]
            public int sizeOfMagazine;
            [JsonProperty("Endless rounds of ammunition? (If true, the cartridges will be added themselves)")]
            public bool IsInfiniteBullets;
        }
        #endregion

        #region Hooks
        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (!isReady)
                return;
            if (container == null || item == null)
                return;
            if (container.playerOwner == null) 
                return;
            // AvG Лаймон
            if (item == null || string.IsNullOrEmpty(item.name) || item.skin == null || weaponsVal == null)
                return;

            WeaponsInfo weapon = weaponsVal.FirstOrDefault(x => x.skin == item.skin);
            if (weapon == null) 
                return;
            UpdateWeapon(item, weapon);

        }
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            // try
            // {
                if (info == null || entity == null)
                    return null;
                if (info.InitiatorPlayer == null)
                    return null;
                // PrintWarning($"{entity.name}, {info.InitiatorPlayer.UserIDString}");
                // PrintWarning($"{info.Weapon == null}");
                BasePlayer p = info.InitiatorPlayer;

                if (p == null)
                    return null;
                var playerWeapon = info.InitiatorPlayer.GetActiveItem();
                if (playerWeapon == null)
                    return null;
                // if (info.Weapon == null)
                //     return null;
                // if (info.Weapon.GetItem() == null)
                //     return null;
                // PDebug("Start OETD");
                // if (string.IsNullOrEmpty(info.Weapon.GetItem().name))
                //     return null;
                WeaponsInfo weapon = cfg.weapons.Values.ToList().FirstOrDefault(x => x.skin == playerWeapon.skin);
                BaseEntity item = info.InitiatorPlayer.GetHeldEntity();

                // try { weapon = ; } catch { return null; }
                if (weapon == null)
                {
                    PDebug("[OnEntityTakeDamage] cfg.weapons => FirstOrDefault(skin, name) == null");
                    return null;
                }
                bool scaled = false;
                if (entity.IsDead()) 
                    return null;
                if (weapon.entityMultiplier && (entity is BuildingBlock || entity is Door))
                {
                    info.damageTypes.ScaleAll(weapon.damageMultiplier);
                    scaled = true;
                }
                if (weapon.botsMultiplier && (entity is NPCPlayer || entity.IsNpc || entity is ScientistNPC || entity is BaseAnimalNPC))
                {
                    info.damageTypes.ScaleAll(weapon.damageMultiplier);
                    scaled = true;
                }
                if (entity is BasePlayer && scaled == false && (entity as BasePlayer).userID.IsSteamId())
                    info.damageTypes.ScaleAll(weapon.damageMultiplier);

                return null;
            // }
            // catch (Exception e)
            // {
            //     PrintError($"Error on OnEntityTakeDamage - {e.Message}");
            //     LogToFile("errors", $"Error on OnEntityTakeDamage - {e.Message}", this);
            //     return null;
            // }
        }
        object OnItemRepair(BasePlayer player, Item item)
        {
            if (item == null)
                return null;

            WeaponsInfo weapon = weaponsVal.FirstOrDefault(x => x.skin == item.skin);
            if (cfg.blockedShortnames.Contains(item.info.shortname))
            {
                player.ChatMessage(string.Format(GetMsg("dontcanrepair"), item.name));
                return false;
            }

            // try { weapon = ; } catch { return null; }
            if (weapon == null )
                return null;

            if (!weapon.maybeRepair || cfg.blockedShortnames.Contains(item.info.shortname))
            {
                player.ChatMessage(string.Format(GetMsg("dontcanrepair"), item.name.Trim() == "" ? "этот предмет!" : $"{item.name}"));
                return false;
            }
            return null;
        }
        void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            if (player.GetActiveItem() == null)
            {
                PDebug("[OnRocketLaunched] player => GetActiveItem() returns null");
                return;
            }
            Item i = player.GetActiveItem();
            var projectile = i.GetHeldEntity().GetComponent<BaseProjectile>();
            if (projectile == null)
            {
                PDebug("[OnRockedLaunched] i => GetHeldEntity() => GetComponent<BaseProjectile>() returns null");
                return;
            }
            var itemInfo = weaponsVal.FirstOrDefault(x => x.skin == i.skin);
            if (!weaponsVal.Any(x => i.skin == x.skin))
            {
                var defaultWeapon = ItemManager.CreateByName(i.info.shortname).GetHeldEntity()?.GetComponent<BaseProjectile>();
                projectile.primaryMagazine.capacity = defaultWeapon.primaryMagazine.capacity;
                PDebug($"[OnRocketLaunched] Weapon with skin '{i.skin}' not founded");
                return;
            }
            projectile.primaryMagazine.capacity = itemInfo.sizeOfMagazine;
            if (itemInfo.IsInfiniteBullets)
            {
                NextTick(() => 
                {
                    projectile.TopUpAmmo();
                    projectile.SendNetworkUpdate();
                });
            }
        }
        void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
            if (projectile == null)
                return;

            if (projectile.GetItem() == null)
            {
                PDebug("[OnWeaponFired] Projectile => GetItem() returns null");
                return;
            }
            Item i = projectile.GetItem();
            if (!weaponsVal.Any(x => i.skin == x.skin))
            {
                PDebug($"[OnWeaponFired] Weapon with skin '{i.skin}' not founded");
                return;
            }
            var itemInfo = weaponsVal.FirstOrDefault(x => x.skin == i.skin);
            projectile.primaryMagazine.capacity = itemInfo.sizeOfMagazine;
            if (itemInfo.IsInfiniteBullets)
            {
                NextTick(() => 
                {
                    projectile.TopUpAmmo();
                    projectile.SendNetworkUpdate();
                });
            }
        }
        private void PDebug(object message, bool log = true)
        {
#if DEBUG
            PrintWarning($"DEBUG => {message.ToString()}");
            if (log)
                LogToFile("debug", message.ToString(), this, true);
#endif
        }
        private void ChangeItemSkin(Item item, ulong targetSkin)
        {
            item.skin = targetSkin;
            item.MarkDirty();

            BaseEntity heldEntity = item.GetHeldEntity();
            if (heldEntity != null)
            {
                heldEntity.skinID = targetSkin;
                heldEntity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }
        }

        void OnLoseCondition(Item item, ref float amount)
        {
            if (item == null) 
                return;
            WeaponsInfo weapon;
            try { weapon = weaponsVal.First(x => x.skin == item.skin); } 
            catch { return; }

            float ConditionBefore = item.condition;
            BasePlayer p = item.GetOwnerPlayer();
            if (p == null)
                return;

            NextTick(() =>
            {
                if (weapon.conditionMultiplier == 0)
                {
                    item.condition = ConditionBefore;
                    return;
                }
                float ConditionAfter = item.condition;
                item.condition += 0.25f;
                float SetCondition = ((ConditionBefore - ConditionAfter) * weapon.conditionMultiplier);
                item.condition -= SetCondition;
                item.GetHeldEntity()?.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                
                if (item.condition == 0 || item.isBroken)
                {
                    if (weapon.deleteOnBreak) 
                        item.UseItem(1);
                    return;
                }
            });

        }
        void OnServerInitialized()
        {
            LoadConfig();
            weaponsVal = cfg.weapons.Values.ToList();
            weaponsKeys = cfg.weapons.Keys.ToList();
        }
        #endregion

        #region Methods
        // private List<uint> ParseData()
        // {
        //     var list = new List<uint>();
        //     foreach (uint id in _netIDsWeapons)
        //     {
        //         if (!ValidEntity<BaseEntity>(id))
        //             continue;
                
        //         list.Add(id);
        //     }
        //     return list;
        // }
        // private bool ValidEntity<T>(uint id) where T : BaseEntity
        // {
        //     bool hasEntity = false;
        //     var entities = UnityEngine.Object.FindObjectsOfType<T>();

        //     foreach (T x in entities)
        //     {
        //         if (x.net == null)
        //             continue;
        //         if (x.net.ID == id)
        //         {
        //             hasEntity = true;
        //             break;
        //         }
        //     }
        //     // PrintDebug($"Entity with net id - {id} was {(hasEntity ? "found" : "NOT found")}.");

        //     return hasEntity;
        // }
        // private T GetEntity<T>(uint id) where T : BaseEntity
        // {
        //     T entity = null;
        //     var entities = UnityEngine.Object.FindObjectsOfType<T>();
        //     foreach (T x in entities)
        //     {
        //         if (x.net.ID == id)
        //         {
        //             entity = x;
        //             break;
        //         }
        //     }
        //     return entity;
        // }
        private void UpdateWeapon(Item item, WeaponsInfo info)
        {
            if (item == null || info == null)
                return;
            
            BaseProjectile projectile = item.GetHeldEntity() as BaseProjectile;
            if (projectile == null)
                return;

            if (info.sizeOfMagazine != 0)
                projectile.primaryMagazine.capacity = info.sizeOfMagazine;

            if (info.IsInfiniteBullets)
                projectile.SetFlag(BaseEntity.Flags.Reserved6, true);
            
            projectile.SendNetworkUpdate(BasePlayer.NetworkQueue.Count);
            item.GetOwnerPlayer()?.inventory?.ServerUpdate(0f);
        }
        #endregion

        #region commands
        [ConsoleCommand("giveweapon")]
        private void cmdGiveWeapon(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
            {
                if (!arg.Player().IsAdmin)
                    return;
            }

            if (arg.Args.IsNullOrEmpty() || arg.Args?.Count() < 2)
            {
                if (arg.Player() != null)
                {
                    arg.Player().ConsoleMessage("Usage: giveweapon player weapon");
                    return;
                }
                PrintWarning("Usage: giveweapon player weapon");
                return;
            }

            // List<string> arg = args.Args.ToList();
            BasePlayer target = BasePlayer.Find(arg.Args[0]);
            if (target == null)
            {
                if (arg.Player() != null)
                {
                    arg.Player().ConsoleMessage($"Player '{arg.Args[0]}' is offline or not exists.");
                    return;
                }
                PrintError($"Player '{arg.Args[0]}' is offline or not exists.");
                return;
            }

            int index = weaponsKeys.FindIndex(x => x == arg.Args[1]);

            if (index == -1)
            {
                if (arg.Player() != null)
                {
                    arg.Player().ConsoleMessage($"Weapon dont exists.");
                    return;
                }
                return;
            }
            WeaponsInfo info = cfg.weapons.Values.ToList()[index];

            Item itemToGive = ItemManager.Create(ItemManager.FindItemDefinition(info.shortname), 1);

            itemToGive.name = weaponsVal.ToList()[index].name;

            target?.inventory.GiveItem(itemToGive);
            NextTick(() =>
            {
                BaseProjectile weapon = itemToGive.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
#if DEBUG
                    Puts($"weapon {weapon._name} not equals null");
#endif
                    if (info.sizeOfMagazine != 0)
                    {
                        weapon.primaryMagazine.capacity = info.sizeOfMagazine;
                        // weapon.TopUpAmmo();
                        weapon.SendNetworkUpdate(BasePlayer.NetworkQueue.Count);
                    }
                }
                // if (info.IsInfiniteBullets)
                //     weapon.SetFlag(BaseEntity.Flags.Reserved6, true);
                ChangeItemSkin(itemToGive, weaponsVal.ToList()[index].skin);
            });
        }
        #endregion

        #region Data
        // private void SaveData()
        // {
        //     Interface.Oxide.DataFileSystem.WriteObject("kryIncreaseWeapons/weapons", ParseData());
        // }
        // void LoadData()
        // {
        //     _netIDsWeapons = Interface.Oxide?.DataFileSystem?.ReadObject<List<uint>>("kryIncreaseWeapons/weapons")
        //         ?? new List<uint>();
        // }
        #endregion

        #region Lang
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"dontcanrepair", "Вы <color=red>не можете</color> починить {0}!"}
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"dontcanrepair", "You <color=red>can't</color> fix {0}!"}
            }, this);
        }
        string GetMsg(string key) => lang.GetMessage(key, this);
        #endregion
    }
}