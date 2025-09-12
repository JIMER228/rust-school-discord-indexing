using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("Smart Weapon", "YaMang -w-", "1.0.9")]
    [Description("https://discord.gg/DTQuEE7neZ")]
    internal partial class SmartWeapon : RustPlugin
    {
        #region Fields
        Dictionary<BasePlayer, SmartWeapons> SmartCache = new Dictionary<BasePlayer, SmartWeapons>();
        private static SmartWeapon Instance { get; set; }
        int oldDebugLevel = 0;
        bool oldReport = false;

        #endregion

        #region Hook
        private void OnServerInitialized(bool initial)
        {
            Instance = this;
            oldDebugLevel = ConVar.AntiHack.debuglevel;
            oldReport = ConVar.AntiHack.reporting;

            ConVar.AntiHack.debuglevel = 0;
            ConVar.AntiHack.reporting = false;

            for (int i = 0; i < _config.generalSettings.Commands.Count; i++)
            {
                cmd.AddChatCommand(_config.generalSettings.Commands[i], this, nameof(SmartWeaponCMD));
            }

            var perm = _config.smartWeaponSettings;

            for (int i = 0; i < perm.Count; i++)
            {
                var dic = perm.ElementAt(i);
                permission.RegisterPermission(dic.Key, this);
            }
        }

        private void SmartWeaponCMD(BasePlayer player, string command, string[] args)
        {

            if (!HasPermission(player))
            {
                Messages(player, Lang("NoPerm"));
                return;
            }
            
            var smartweapon = player.GetComponent<SmartWeapons>();
            if (!smartweapon)
            {
                player.gameObject.AddComponent<SmartWeapons>();
            }
            else
            {
                if (smartweapon)
                {
                    smartweapon.DestroyComponent();
                }
            }
        }

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
            var smartweapon = GetOrAddWeapon(player);

            if (smartweapon == null) return;
            if (smartweapon.Target == null) return;
            if (!smartweapon.IsHit) return;
            projectiles.projectiles.Clear();

            var target = smartweapon?.Target ?? null;
            if (target == null) return;

            if (!smartweapon.IsVisibleTarget()) return;

            float damage = 0;

            var active = player.GetActiveItem();

            if(_config.generalSettings.damageWeapon.ContainsKey(active.info.shortname))
            {
                var config = _config.generalSettings.damageWeapon[active.info.shortname];
                damage = config.Damage;

                if (smartweapon.IsHeadshot)
                {
                    if(_config.generalSettings.headBonusDamage != 0)
                        damage = damage * config.HeadBonus;
                }
            }

            HitInfo info = null;

            if (projectile.primaryMagazine.ammoType.itemid == -1321651331)
                info = new HitInfo(player, target, Rust.DamageType.Explosion, damage);
            else
                info = new HitInfo(player, target, Rust.DamageType.Bullet, damage);
            var hit = new Effect();

            hit.Init(Effect.Type.Generic, player.eyes.position, new Vector3());

            if (target as BasePlayer && smartweapon.IsHeadshot)
            {
                info.HitBone = StringPool.toNumber["head"];
                hit.pooledString = "assets/bundled/prefabs/fx/headshot.prefab";
            }
            else
            {
                info.HitBone = StringPool.toNumber["spine1"];
                hit.pooledString = "assets/bundled/prefabs/fx/hit_notify.prefab";
            }

            EffectNetwork.Send(hit, player.Connection);

            info.HitEntity = player;
            info.Weapon = (AttackEntity)active.GetHeldEntity();
            info.WeaponPrefab = active.GetHeldEntity();
            target.Hurt(info);
            //헬기 를 락온 할 경우 헬기가 플레이어를 공격을 못함.
            //Interface.CallHook("IOnBaseCombatEntityHurt", player, info); //테스트 못해봄
        }
        
        private object OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            var smartweapon = GetOrAddWeapon(attacker);

            if (smartweapon == null) return null;
            if (info == null) return null;
            if (smartweapon.Target == null) return null;
            if (info.HitEntity != smartweapon.Target) return null;

            info.damageTypes.ScaleAll(0);
            return true;
        }


        void OnPlayerDisconnected(BasePlayer player, string reason) => Disable(player);

        private void Disable(BasePlayer player)
        {
            var smartweapon = player.GetComponent<SmartWeapons>();

            if (smartweapon)
            {
                smartweapon.DestroyComponent();
                
            }
        }

        void Unload()
        {
            ConVar.AntiHack.debuglevel = oldDebugLevel;
            ConVar.AntiHack.reporting = oldReport;

            foreach (var player in BasePlayer.activePlayerList)
            {
                var smartweapon = player.GetComponent<SmartWeapons>();

                if (smartweapon)
                {
                    smartweapon.DestroyComponent();
                    
                }
            }
        }

        #endregion

        #region Helper
        private SmartWeapons GetOrAddWeapon(BasePlayer player, bool shouldAdd = false)
        {
            SmartWeapons weapon;
            if (!SmartCache.TryGetValue(player, out weapon))
            {
                if (!shouldAdd) return player.GetComponent<SmartWeapons>();
                weapon = player.GetOrAddComponent<SmartWeapons>();
                SmartCache.Add(player, weapon);
            }

            return weapon;
        }
        private bool HasPermission(BasePlayer player)
        {
            var perm = _config.smartWeaponSettings;

            for (int i = 0; i < perm.Count; i++)
            {
                var dic = perm.ElementAt(i);
                if (permission.UserHasPermission(player.UserIDString, dic.Key))
                {
                    return true;
                }
            }

            return false;
        }



        private void Messages(BasePlayer player, string text) => player.SendConsoleCommand("chat.add", 2, _config.generalSettings.SteamID, $"{_config.generalSettings.Prefix} {text}");
        private void SendTip(BasePlayer player, int num, string text) => player.SendConsoleCommand($"gametip.showtoast {num} \"{text}\"");
        #endregion

        #region Config        
        private ConfigData _config;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "General Settings")] public GeneralSettings generalSettings { get; set; }
            [JsonProperty(PropertyName = "UI Settings")] public UISettings UISettings { get; set; }
            [JsonProperty(PropertyName = "Smart Weapon Settings")] public Dictionary<string, SmartWeaponSettings> smartWeaponSettings { get; set; }
            public Oxide.Core.VersionNumber Version { get; set; }
        }

        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigData>();

            if (_config.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(_config, true);
        }

        protected override void LoadDefaultConfig() => _config = GetBaseConfig();
        public class CustomItem
        {
            public ulong SkinId { get; set; }
            public float Damage { get; set; }
            public float HeadBonus { get; set; }
        }
        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                generalSettings = new GeneralSettings
                {
                    Prefix = "[Smart-Weapon]",
                    Commands = new List<string>
                    {
                        "sw",
                        "smartweapon"
                    },
                    SteamID = "0",
                    damageWeapon = new Dictionary<string, CustomItem>
                    {
                        {
                            "rifle.ak", new CustomItem
                            {
                                SkinId = 0,
                                Damage = 50,
                                HeadBonus = 1.5f
                            }
                        },
                        {
                            "rifle.ak.ice", new CustomItem
                            {
                                SkinId = 0,
                                Damage = 50,
                                HeadBonus = 1.5f
                            }
                        },
                        {
                            "rifle.lr300", new CustomItem
                            {
                                SkinId = 0,
                                Damage = 40,
                                HeadBonus = 1.5f
                            }
                        },
                        {
                            "smg.2", new CustomItem
                            {
                                SkinId = 0,
                                Damage = 30,
                                HeadBonus = 1.5f
                            }
                        },
                        {
                            "lmg.m249", new CustomItem
                            {
                                SkinId = 0,
                                Damage = 65,
                                HeadBonus = 1.5f
                            }
                        },
                        {
                            "hmlmg", new CustomItem
                            {
                                SkinId = 0,
                                Damage = 57,
                                HeadBonus = 1.5f
                            }
                        },
                        {
                            "rifle.m39", new CustomItem
                            {
                                SkinId = 0,
                                Damage = 50,
                                HeadBonus = 1.5f
                            }
                        },
                        {
                            "pistol.m92", new CustomItem
                            {
                                SkinId = 0,
                                Damage = 45,
                                HeadBonus = 1.5f
                            }
                        },
                        {
                            "smg.mp5", new CustomItem
                            {
                                SkinId = 0,
                                Damage = 35,
                                HeadBonus = 1.5f
                            }
                        },
                        {
                            "pistol.python", new CustomItem
                            {
                                SkinId = 0,
                                Damage = 55,
                                HeadBonus = 1.5f
                            }
                        },
                        {
                            "rifle.semiauto", new CustomItem
                            {
                                SkinId = 0,
                                Damage = 40,
                                HeadBonus = 1.5f
                            }
                        },
                        {
                            "smg.thompson", new CustomItem
                            {
                                SkinId = 0,
                                Damage = 37,
                                HeadBonus = 1.5f
                            }
                        },
                        {
                            "pistol.semiauto", new CustomItem
                            {
                                SkinId = 0,
                                Damage = 40,
                                HeadBonus = 1.5f
                            }
                        },
                        {
                            "pistol.revolver", new CustomItem
                            {
                                SkinId = 0,
                                Damage = 35,
                                HeadBonus = 1.5f
                            }
                        }
                    },
                    BlockZones = new List<string> { },
                    Debug = false
                },
                UISettings = new UISettings
                {
                    AnchorMin = "0.4260417 0.3222224",
                    AnchorMax = "0.5739583 0.4148146",
                    Text = "{0} | {1} | {2}"
                },
                smartWeaponSettings = new Dictionary<string, SmartWeaponSettings>
                {
                    {
                        "smartweapon.use.admin", new SmartWeaponSettings
                        {
                            Angle = 30,
                            HitDistance = 100,
                            HitChance = 100,
                            HeadChance = 100,
                            bypassWall = true,
                            CanTargetPlayer = true,
                            flagsAdmin = true
                        }
                    },
                    {
                        "smartweapon.use.3", new SmartWeaponSettings
                        {
                            Angle = 30,
                            HitDistance = 100,
                            HitChance = 100,
                            HeadChance = 100,
                            bypassWall = false,
                            CanTargetPlayer = true,
                            flagsAdmin = false
                        }
                    },
                    {
                        "smartweapon.use.2", new SmartWeaponSettings
                        {
                            Angle = 30,
                            HitDistance = 100,
                            HitChance = 100,
                            HeadChance = 100,
                            bypassWall = false,
                            CanTargetPlayer = true,
                            flagsAdmin = false
                        }
                    },
                    {
                        "smartweapon.use.1", new SmartWeaponSettings
                        {
                            Angle = 30,
                            HitDistance = 100,
                            HitChance = 80,
                            HeadChance = 50,
                            bypassWall = false,
                            CanTargetPlayer = false,
                            flagsAdmin = false
                        }
                    }

                },
                Version = Version
            };
        }
        public class UISettings
        {
            public string AnchorMin { get; set; }
            public string AnchorMax { get; set; }
            public string Text { get; set; }
        }

        public class GeneralSettings
        {
            [JsonProperty(PropertyName = "Prefix", Order = 1)] public string Prefix { get; set; }
            [JsonProperty(PropertyName = "SteamID", Order = 2)] public string SteamID { get; set; }
            [JsonProperty(PropertyName = "Commands", Order = 3)] public List<string> Commands { get; set; }
            [JsonProperty(PropertyName = "Head Bonus Damage", Order = 4)] public float headBonusDamage { get; set; }
            [JsonProperty(PropertyName = "Damage Weapon (Fired Only)", Order = 5)] public Dictionary<string, CustomItem> damageWeapon { get; set; }
            [JsonProperty(PropertyName = "Disable Smart Weapon Zones", Order = 6)] public List<string> BlockZones { get; set; }
            [JsonProperty(PropertyName = "Lockdown Location Player Display Settings ※[0 ~ 2] recommend※", Order = 7)] public float LookPlayerPostion { get; set; }
            [JsonProperty(PropertyName = "Lockdown Location Animal Display Settings ※[0 ~ 0.5] recommend※", Order = 8)] public float LookAnimalPostion { get; set; }
            [JsonProperty(PropertyName = "Lockdown Location Entity Display Settings ※[0 ~ 2] recommend※", Order = 8)] public float LookEntityPostion { get; set; }
            [JsonProperty(PropertyName = "Debug", Order = 20)] public bool Debug { get; set; }
        }

        public class SmartWeaponSettings
        {
            [JsonProperty(PropertyName = "Angle", Order = 1)] public float Angle { get; set; }
            [JsonProperty(PropertyName = "Hit Distance", Order = 2)] public int HitDistance { get; set; }
            [JsonProperty(PropertyName = "Hit Chance [0 - 100]", Order = 3)] public int HitChance { get; set; }
            [JsonProperty(PropertyName = "Head Chance [0 - 100]", Order = 4)] public int HeadChance { get; set; }
            [JsonProperty(PropertyName = "Bypass Wall", Order = 5)] public bool bypassWall { get; set; }
            [JsonProperty(PropertyName = "Can Target Player", Order = 6)] public bool CanTargetPlayer { get; set; }
            [JsonProperty(PropertyName = "Can Target Animal", Order = 7)] public bool CanTargetAnimal { get; set; }
            [JsonProperty(PropertyName = "Can Target Scientist", Order = 8)] public bool CanTargetScientist { get; set; }
            [JsonProperty(PropertyName = "Can Target APC", Order = 9)] public bool CanTargetAPC { get; set; }
            [JsonProperty(PropertyName = "Can Target Heli", Order = 10)] public bool CanTargetHeli { get; set; }
            [JsonProperty(PropertyName = "Flag Admin? (true - Text | false - UI)", Order = 15)] public bool flagsAdmin { get; set; }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");
            if(_config.Version == new VersionNumber(1, 0, 5))
            {
                if(_config.UISettings.Text == "{0} | {1} | {2} | {3}")
                {
                    _config.UISettings.Text = "{0} | {1} | {2}";
                }

            }
            _config.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Active", "<color=#00ff00>Active Smart Weapon</color>" },
                { "DeActive", "<color=red>Disable Smart Weapon</color>" },
                { "LockDown", "<size=20>[ Target Locked ] \n{0} | {1} | {2}</size>" },
                { "NoPermission", "<color=red>You are have not permission</color>" }

            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Active", "<color=#00ff00>Active Smart Weapon</color>" },
                { "DeActive", "<color=red>Disable Smart Weapon</color>" },
                { "LockDown", "<size=20>[ Target Locked ] \n{0} | {1} | {2}</size>" },
                { "NoPermission", "<color=red>당신은 권한이 없습니다.</color>" }
            }, this, "ko");
        }

        private string Lang(string key, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this), args);
        }

        #endregion

        #region Main
        private class SmartWeapons : MonoBehaviour
        {
            [PluginReference] private Plugin Clans, ZoneManager;
            private BasePlayer player;
            private List<BaseCombatEntity> entities;
            private List<BaseCombatEntity> targets;
            private DateTime OldTime;

            public BaseCombatEntity Target { get; private set; }
            private string TargetName 
            {
                get
                {
                    if(Target == null)
                    {
                        return "";
                    }
                    if(Target is BasePlayer)
                    {
                        return (Target as BasePlayer).displayName;
                    }
                    else if (Target is BaseAnimalNPC)
                    {
                        if (string.IsNullOrEmpty((Target as BaseAnimalNPC).name))
                            return (Target as BaseAnimalNPC).ShortPrefabName;
                        else
                            return (Target as BaseAnimalNPC).name;
                    }
                    return Target.ShortPrefabName ?? "UNKNOWN";
                }
            }

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                entities = new List<BaseCombatEntity>();
                targets = new List<BaseCombatEntity>();
                OldTime = new DateTime();
                
                Instance.Messages(player, $"{Instance.Lang("Active")}");
            }
            public bool IsHit
            {
                get
                {
                    if(getBypassWall() && !IsVisibleTarget())
                    {
                        return false;
                    }
                    return OverPercent(getHitChance());
                }
            }

            public bool IsHeadshot
            {
                get
                {
                    return OverPercent(getHeadChance());
                }
            }

            private void FixedUpdate()
            {
                if(OldTime == null)
                    OldTime = DateTime.Now;

                DateTime startTime = DateTime.Now;

                TimeSpan timeDifference = OldTime - startTime;

                bool isTimeDifferenceGreaterThanPointOneSeconds = timeDifference.TotalSeconds > 0.1;

                if (isTimeDifferenceGreaterThanPointOneSeconds)
                    return;

                if (!IsLockdown()) 
                {
                    Target = null;
                    return;
                }

                if (Target == null)
                {
                    if (player.GetActiveItem() == null) return;
                    if (getBlockZone()) return;
                    if (FindTargets())
                    {
                        if (getFlagAdmin()) DrawText();
                        else DrawGUI();
                        
                    }
                }
                else
                {
                    if (getFlagAdmin()) DrawText();
                    else DrawGUI();
                }
                
            }
            private void OnDestroy()
            {
                Instance.Messages(player, Instance.Lang("DeActive"));
            }
            public void DestroyComponent()
            {
                DestroyImmediate(this);
            }
            List<string> allowTypes = new List<string>();

            private bool FindTargets()
            {
                entities.Clear();
                targets.Clear();

                Vis.Entities(player.transform.position, getDistance(), entities, LayerMask.GetMask("Default", "AI", "Player (Server)"));
                if (entities.Count == 0) return false;
                bool isEntityVisible(BaseCombatEntity ent)
                {
                    if (getBypassWall()) return true;
                    return IsEntityVisible(ent);
                }

                foreach (var entity in entities)
                {
                    if (entity is BradleyAPC) { if (getCanTargetAPC() && isEntityVisible(entity)) { targets.Add(entity); } }
                    
                    if (entity is PatrolHelicopter) { if (getCanTargetHeli() && isEntityVisible(entity)) targets.Add(entity); }
                    
                    if (entity is CH47Helicopter) { if (getCanTargetHeli() && isEntityVisible(entity)) targets.Add(entity); }
                    
                    if (entity is ScientistNPC || entity is TunnelDweller || entity is UnderwaterDweller) { if (getCanTargetScientists() && IsEntityVisible(entity)) { targets.Add(entity); } }

                    if (entity is BaseAnimalNPC || entity is RidableHorse || entity is SimpleShark) { if (getCanTargetAnimal() && isEntityVisible(entity)) targets.Add(entity); }
                    
                    if (entity is BasePlayer target)
                    {
                        if (target.IsNpc || target.Connection == null) continue; //플레이어가 NPC 일 경우 예외
                        if (target == player || IsTeamMate(player, target)) continue;
                        if (!getCanTargetPlayer() && target.userID.IsSteamId()) continue;
                        if (!getBypassWall())
                        {
                            if (!isEntityVisible(target)) continue;
                        }
                        targets.Add(target);
                    }
                }
                if (targets.Count == 0)
                {
                    return false;
                }
                //else
                //{
                //    targets.AddRange(entities);
                //}

                var ray = player.eyes.HeadRay();

                Target = targets.OrderBy(x => (x.transform.position - ray.ClosestPoint(x.transform.position)).sqrMagnitude).First();

                return true;
            }

            private float getAngle()
            {
                var perm = Instance._config.smartWeaponSettings;

                for (int i = 0; i < perm.Count; i++)
                {
                    var dic = perm.ElementAt(i);
                    if (Instance.permission.UserHasPermission(player.UserIDString, dic.Key))
                    {
                        return dic.Value.Angle;
                    }
                }

                return 0;
            }
            private float getDistance()
            {
                var perm = Instance._config.smartWeaponSettings;

                for (int i = 0; i < perm.Count; i++)
                {
                    var dic = perm.ElementAt(i);
                    if (Instance.permission.UserHasPermission(player.UserIDString, dic.Key))
                    {
                        return dic.Value.HitDistance;
                    }
                }

                return 0;
            }
            private bool getBypassWall()
            {
                var perm = Instance._config.smartWeaponSettings;

                for (int i = 0; i < perm.Count; i++)
                {
                    var dic = perm.ElementAt(i);
                    if (Instance.permission.UserHasPermission(player.UserIDString, dic.Key))
                    {
                        return dic.Value.bypassWall;
                    }
                }

                return false;
            }
            private bool getCanTargetPlayer()
            {
                var perm = Instance._config.smartWeaponSettings;

                for (int i = 0; i < perm.Count; i++)
                {
                    var dic = perm.ElementAt(i);
                    if (Instance.permission.UserHasPermission(player.UserIDString, dic.Key))
                    {
                        return dic.Value.CanTargetPlayer;
                    }
                }

                return false;
            }
            private bool getCanTargetAnimal()
            {
                var perm = Instance._config.smartWeaponSettings;

                for (int i = 0; i < perm.Count; i++)
                {
                    var dic = perm.ElementAt(i);
                    if (Instance.permission.UserHasPermission(player.UserIDString, dic.Key))
                    {
                        return dic.Value.CanTargetAnimal;
                    }
                }

                return false;
            }
            private bool getCanTargetAPC()
            {
                var perm = Instance._config.smartWeaponSettings;
                for (int i = 0; i < perm.Count; i++)
                {
                    var dic = perm.ElementAt(i);
                    if (Instance.permission.UserHasPermission(player.UserIDString, dic.Key))
                    {
                        return dic.Value.CanTargetAPC;
                    }
                }
                return false;
            }
            private bool getCanTargetHeli()
            {
                var perm = Instance._config.smartWeaponSettings;

                for (int i = 0; i < perm.Count; i++)
                {
                    var dic = perm.ElementAt(i);
                    if (Instance.permission.UserHasPermission(player.UserIDString, dic.Key))
                    {
                        return dic.Value.CanTargetHeli;
                    }
                }

                return false;
            }

            private bool getCanTargetScientists()
            {
                var perm = Instance._config.smartWeaponSettings;

                for (int i = 0; i < perm.Count; i++)
                {
                    var dic = perm.ElementAt(i);
                    if (Instance.permission.UserHasPermission(player.UserIDString, dic.Key))
                    {
                        return dic.Value.CanTargetScientist;
                    }
                }

                return false;
            }

            private bool getFlagAdmin()
            {
                var perm = Instance._config.smartWeaponSettings;

                for (int i = 0; i < perm.Count; i++)
                {
                    var dic = perm.ElementAt(i);
                    if (Instance.permission.UserHasPermission(player.UserIDString, dic.Key))
                    {
                        return dic.Value.flagsAdmin;
                    }
                }

                return false;
            }
            private bool IsPlayerInZone(string zoneID, BasePlayer player) => (bool)ZoneManager?.Call("IsPlayerInZone", zoneID, player);
            private bool getBlockZone()
            {
                if (ZoneManager == null) return false;
                var zoneList = Instance._config.generalSettings.BlockZones;

                var findZone = false;

                foreach (var id in zoneList)
                {
                    if (IsPlayerInZone(id, player))
                    {
                        Instance.Messages(player, Instance.Lang("DeActive"));
                        DestroyComponent();
                        findZone = true;
                        break;
                    }
                }

                if (findZone) return true;
                else return false;
            }
            private int getHeadChance()
            {
                var perm = Instance._config.smartWeaponSettings;

                for (int i = 0; i < perm.Count; i++)
                {
                    var dic = perm.ElementAt(i);
                    if (Instance.permission.UserHasPermission(player.UserIDString, dic.Key))
                    {
                        return dic.Value.HeadChance;
                    }
                }

                return 0;
            }
            private int getHitChance()
            {
                var perm = Instance._config.smartWeaponSettings;

                for (int i = 0; i < perm.Count; i++)
                {
                    var dic = perm.ElementAt(i);
                    if (Instance.permission.UserHasPermission(player.UserIDString, dic.Key))
                    {
                        return dic.Value.HitChance;
                    }
                }

                return 0;
            }
            bool OverPercent(int percent)
            {
                if (UnityEngine.Random.Range(0, 101) <= percent) return true;
                else return false;
            }
            private bool IsClanMember(string playerId, string otherId) => (bool)(Clans?.Call("IsClanMember", playerId, otherId));
            private bool IsTeamMate(BasePlayer player, BasePlayer victim)
            {
                if (victim == null) return false;
                if (player.Team != null && player.Team.members.Contains(victim.userID)) return true;
                if (Clans != null && IsClanMember(player.UserIDString, victim.UserIDString)) return true;
                return false;
            }

            private bool IsLockdown() => player.serverInput.IsDown(BUTTON.FIRE_SECONDARY);
            public bool IsVisibleTarget()
            {
                if (IsEntityVisible(Target)) return true;
                else return false;
            }
            private bool IsEntityVisible(BaseCombatEntity entity)
            {
                if (!IsInAngle(entity))
                    return false;
                if (!IsInDistance(entity)) return false;
                var target = entity as BasePlayer;

                if (target != null)
                {
                    return target.IsVisible(player.eyes.position, target.eyes.position);
                }
                return entity.IsVisible(player.eyes.position, entity.transform.position);// + new Vector3(0, 1.5f, 0));
            }
            private float getDistance(BaseCombatEntity ent)
            {
                return Vector3.Distance(player.transform.position, ent.transform.position);

            }
            private bool IsInDistance(BaseCombatEntity ent)
            {
                if (Vector3.Distance(player.transform.position, ent.transform.position) < getDistance())
                    return true;
                else return false;
            }
            private bool IsEntityPlayer(BaseCombatEntity entity)
            {
                if (entity.IsNpc) return false;
                else return true;
            }
            private bool IsEntityAnimal(BaseCombatEntity entity)
            {
                if (entity is BaseAnimalNPC) return true;
                else return false;
            }
            private bool IsInAngle(BaseCombatEntity entity)
            {
                if(entity is BaseAnimalNPC)
                {
                    return IsInAnimalSight(entity, getDistance(entity));
                    //float angleThreshold = 100;
                    //if (angleThreshold == 0) return false;

                    //// 플레이어의 시점에서 엔티티까지의 벡터를 계산합니다.
                    //Vector3 fromPlayerToEntity = (entity.transform.position - player.transform.position).normalized;

                    //// 플레이어의 시점 벡터를 구합니다.
                    //Vector3 playerForward = player.eyes.HeadForward();

                    //// 플레이어의 시점 벡터와 플레이어에서 엔티티까지의 벡터 사이의 각도를 계산합니다.
                    //float angle = Vector3.Angle(playerForward, fromPlayerToEntity - new Vector3(0, 0));

                    //// 플레이어와 엔티티 사이의 거리를 계산합니다.
                    //float distance = Vector3.Distance(player.transform.position, entity.transform.position);

                    //// 각도와 거리를 고려하여 엔티티가 각도 범위 내에 있는지 확인합니다.
                    //bool isInAngle = Mathf.Abs(angle * distance) <= angleThreshold;

                    //return isInAngle;
                }
                else if (entity is BradleyAPC)
                {
                    float angleThreshold = 150f;
                    if (angleThreshold == 0) return false;

                    // 플레이어의 시점에서 엔티티까지의 벡터를 계산합니다.
                    Vector3 fromPlayerToEntity = (entity.transform.position - player.transform.position).normalized;

                    // 플레이어의 시점 벡터를 구합니다.
                    Vector3 playerForward = player.eyes.HeadForward();

                    // 플레이어의 시점 벡터와 플레이어에서 엔티티까지의 벡터 사이의 각도를 계산합니다.
                    float angle = Vector3.Angle(playerForward, fromPlayerToEntity);

                    // 플레이어와 엔티티 사이의 거리를 계산합니다.
                    float distance = Vector3.Distance(player.transform.position, entity.transform.position);

                    // 각도와 거리를 고려하여 엔티티가 각도 범위 내에 있는지 확인합니다.
                    bool isInAngle = Mathf.Abs(angle * distance) <= angleThreshold;

                    return isInAngle;
                }
                else if (entity is BaseHelicopter)
                {
                    float angleThreshold = 500f;
                    if (angleThreshold == 0) return false;

                    // 플레이어의 시점에서 엔티티까지의 벡터를 계산합니다.
                    Vector3 fromPlayerToEntity = (entity.transform.position - player.transform.position).normalized;

                    // 플레이어의 시점 벡터를 구합니다.
                    Vector3 playerForward = player.eyes.HeadForward();

                    // 플레이어의 시점 벡터와 플레이어에서 엔티티까지의 벡터 사이의 각도를 계산합니다.
                    float angle = Vector3.Angle(playerForward, fromPlayerToEntity);

                    // 플레이어와 엔티티 사이의 거리를 계산합니다.
                    float distance = Vector3.Distance(player.transform.position, entity.transform.position);

                    // 각도와 거리를 고려하여 엔티티가 각도 범위 내에 있는지 확인합니다.
                    bool isInAngle = Mathf.Abs(angle * distance) <= angleThreshold;

                    return isInAngle;
                }
                else if (entity is CH47Helicopter)
                {
                    float angleThreshold = 1000f;
                    if (angleThreshold == 0) return false;

                    // 플레이어의 시점에서 엔티티까지의 벡터를 계산합니다.
                    Vector3 fromPlayerToEntity = (entity.transform.position - player.transform.position).normalized;

                    // 플레이어의 시점 벡터를 구합니다.
                    Vector3 playerForward = player.eyes.HeadForward();

                    // 플레이어의 시점 벡터와 플레이어에서 엔티티까지의 벡터 사이의 각도를 계산합니다.
                    float angle = Vector3.Angle(playerForward, fromPlayerToEntity);

                    // 플레이어와 엔티티 사이의 거리를 계산합니다.
                    float distance = Vector3.Distance(player.transform.position, entity.transform.position);

                    // 각도와 거리를 고려하여 엔티티가 각도 범위 내에 있는지 확인합니다.
                    bool isInAngle = Mathf.Abs(angle * distance) <= angleThreshold;

                    return isInAngle;
                }
                else // player
                {
                    float angleThreshold = getAngle();
                    if (angleThreshold == 0) return false;

                    // 플레이어의 시점에서 엔티티까지의 벡터를 계산합니다.
                    Vector3 fromPlayerToEntity = (entity.transform.position - player.transform.position).normalized;

                    // 플레이어의 시점 벡터를 구합니다.
                    Vector3 playerForward = player.eyes.HeadForward();

                    // 플레이어의 시점 벡터와 플레이어에서 엔티티까지의 벡터 사이의 각도를 계산합니다.
                    float angle = Vector3.Angle(playerForward, fromPlayerToEntity);

                    // 플레이어와 엔티티 사이의 거리를 계산합니다.
                    float distance = Vector3.Distance(player.transform.position, entity.transform.position);

                    // 각도와 거리를 고려하여 엔티티가 각도 범위 내에 있는지 확인합니다.
                    bool isInAngle = Mathf.Abs(angle * distance) <= angleThreshold;

                    return isInAngle;
                }
            }
            private bool IsInAnimalSight(BaseCombatEntity entity, float distanceThreshold)
            {
                float angleThreshold = 100;
                if (angleThreshold == 0) return false;

                Vector3 playerPosition = player.transform.position;
                Vector3 entityPosition = entity.transform.position;
                float distance = Vector3.Distance(player.transform.position, entity.transform.position);
                // 플레이어와 동물 사이의 거리를 계산합니다.


                // 거리가 범위를 벗어나면 false를 반환합니다.
                if (distance > distanceThreshold)
                {
                    return false;
                }

                // 플레이어에서 동물까지의 벡터를 계산합니다.
                Vector3 playerToEntity = (entityPosition - playerPosition).normalized;

                // 플레이어의 시야 방향을 구합니다.
                Vector3 playerForward = player.eyes.HeadForward();

                // 플레이어 시야 방향과 플레이어에서 동물까지의 벡터 사이의 각도를 계산합니다.
                float angle = Vector3.Angle(playerForward, playerToEntity);
                bool isInAngle = Mathf.Abs(angle * distance) <= angleThreshold;

                return isInAngle;
                // 각도가 범위 내에 있는지 확인합니다.
                //if (angle <= angleThreshold)
                //{
                //    // 플레이어 시야와 동물 사이에 장애물이 있는지 확인합니다.
                //    RaycastHit hit;
                //    if (Physics.Raycast(playerPosition, playerToEntity, out hit, distance, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
                //    {
                //        // 장애물에 가려져 있다면 false를 반환합니다.
                //        if (hit.collider.gameObject != entity.gameObject)
                //        {
                //            return false;
                //        }
                //    }

                //    return true;
                //}

                //return false;
            }
            string MainContainer = "UI_SmartWeapon";
            private void DrawGUI()
            {
                if (Target == null) return;
                if (Instance == null) return;

                var main = new CuiElementContainer();
                

                main.Add(new CuiElement
                {
                    Name = MainContainer,
                    Parent = "Hud",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0 0 0 0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = Instance._config.UISettings.AnchorMin,
                            AnchorMax = Instance._config.UISettings.AnchorMax
                        }

                    }
                });

                main.Add(new CuiElement
                {
                    Name = "Text",
                    Parent = MainContainer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{string.Format(Instance._config.UISettings.Text, TargetName, Target.health, string.Format("{0:N1}", getDistance(Target)))}",
                            Font = "permanentmarker.ttf",
                            Align = TextAnchor.MiddleCenter,
                            Color = IsVisibleTarget() ? "0 0.5194479 0.01588263 1" : "1 0 0 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                });

                CuiHelper.DestroyUi(player, MainContainer);
                CuiHelper.AddUi(player, main);
                Instance.NextFrame(() =>
                {
                    if (!player.serverInput.IsDown(BUTTON.FIRE_SECONDARY))
                        CuiHelper.DestroyUi(player, MainContainer);
                });
            }
            private void DrawText()
            {
                if (Target == null) return;

                if (!player.IsAdmin) player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);

                //int distance = Convert.ToInt32(string.Format("{0:N1}", getDistance(Target)));

                //if (distance <= 20)
                //{
                //    player.SendConsoleCommand("ddraw.text", 0.1, IsVisibleTarget(Target) ? Color.green : Color.red, Target.transform.position + new Vector3(0, 2f, 0),
                //           $"<size=14>[ Target Locked ] \n{TargetName} | {Target.health} | {string.Format("{0:N1}", getDistance(Target))}</size>");
                //}

                

                if(Target is BaseAnimalNPC)
                {
                    player.SendConsoleCommand("ddraw.text", 0.1, IsVisibleTarget() ? Color.green : Color.red, Target.transform.position + new Vector3(0, Instance._config.generalSettings.LookAnimalPostion, 0),
                           $"{string.Format(Instance._config.UISettings.Text, TargetName, Target.health, string.Format("{0:N1}", getDistance(Target)))}");
                }
                else if (IsEntityPlayer(Target))
                {
                    player.SendConsoleCommand("ddraw.text", 0.1, IsVisibleTarget() ? Color.green : Color.red, Target.transform.position + new Vector3(0, Instance._config.generalSettings.LookEntityPostion, 0),
                           $"{string.Format(Instance._config.UISettings.Text, TargetName, Target.health, string.Format("{0:N1}", getDistance(Target)))}");
                }
                else
                {
                    player.SendConsoleCommand("ddraw.text", 0.1, IsVisibleTarget() ? Color.green : Color.red, Target.transform.position + new Vector3(0, Instance._config.generalSettings.LookPlayerPostion, 0),
                          $"{string.Format(Instance._config.UISettings.Text, TargetName, Target.health, string.Format("{0:N1}", getDistance(Target)))}");//$"<size=20>[ Target Locked ] \n{TargetName} | {Target.health} | {string.Format("{0:N1}", getDistance(Target))}</size>");
                }
                

                //$"{Instance.Lang("Lockdown", TargetName, Target.health, getDistance(Target))}");

                if (!player.IsAdmin) player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
            }
        }

        #endregion
    }
}
