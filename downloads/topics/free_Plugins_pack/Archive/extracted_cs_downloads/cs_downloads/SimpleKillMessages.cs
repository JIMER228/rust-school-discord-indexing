using Newtonsoft.Json;
using Rust;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProtoBuf;
using CompanionServer.Handlers;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("SimpleKillMessages", ".h1pex", "1.2.5")]
    [Description("Displayed death/kill information in chat upon death, with some extra features!")]
    class SimpleKillMessages : CovalencePlugin
    {
        [PluginReference]
        private Plugin Economics;

        PluginConfig _config;

        private void Init()
        {
            _config = Config.ReadObject<PluginConfig>();
        }

        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    throw new JsonException();
                }

                if (!_config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintWarning($"PluginConfig file {Name}.json updated.");

                    SaveConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();

                PrintError("Config file contains an error and has been replaced with the default file.");
            }

        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Prefix", Order = 0)]
            public string Prefix { get; set; }

            [JsonProperty(PropertyName = "Chat Icon", Order = 1)]
            public int ChatIcon { get; set; }

            [JsonProperty(PropertyName = "Prevent NPC", Order = 2)]
            public bool PreventNPC { get; set; }

            [JsonProperty(PropertyName = "Keep Held Item In Hotbar On Death", Order = 3)]
            public bool PreventDropOnDeath { get; set; }

            [JsonProperty(PropertyName = "Reward Kill (Economics)")]
            public bool EconomicsRewardsEnabled { get; set; }

            [JsonProperty(PropertyName = "Points Per Kill")]
            public double EconomicsPointsReward { get; set; }

            [JsonProperty(PropertyName = "Show Messages Global?")]
            public bool ShowMessagesGlobally { get; set; }

            [JsonProperty(PropertyName = "Global Radius/Distance? (Show to only players in certain radius? 0 = everyone)")]
            public float GlobalRadius { get; set; }

            [JsonProperty(PropertyName = "Use Radius? (Will switch to distance if false)")]
            public bool UseRadius { get; set; }

            [JsonProperty(PropertyName = "Entity Names (Replaces those ugly names with better names)")]
            public Dictionary<string, string> EntityNames { get; set; }

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                Prefix = "<color=#42f566>SimpleKillMessages:</color> ",
                ChatIcon = 0,
                PreventNPC = true,
                PreventDropOnDeath = true,
                EconomicsRewardsEnabled = false,
                EconomicsPointsReward = 2,
                ShowMessagesGlobally = false,
                GlobalRadius = 0,
                UseRadius = true,
                EntityNames = new Dictionary<string, string>() {
                    { "rocket_basic", "Rocket" },
                    { "rocket_hv", "HV Rocket" },
                    { "explosive.satchel.deployed", "Satchel Charge" },
                    { "grenade.beancan.deployed", "Beancan Grenade" },
                    { "grenade.f1.deployed", "F1 Grenade" }
                }
            };
        }
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["DeathByWoundsVictim"] = "<size=12><color=#b6bab7>You bled out after being attacked by <color=#61ff7e>{0}</color> ({1})!</color></size>",
                ["DeathByWoundsKiller"] = "<size=12><color=#b6bab7>You killed <color=#61ff7e>{0}</color> as they bled out!</color></size>",
                ["DeathByWoundsGlobal"] = "<size=12><color=#b6bab7><color=#61ff7e>{0}</color> ({2}) killed <color=#61ff7e>{1}</color> as they bled out!</color></size>",
                ["DeathBySuicideVictim"] = "<size=12><color=#b6bab7>You died by suicide!</color></size>",
                ["DeathBySuicideGlobal"] = "<size=12><color=#b6bab7><color=#61ff7e>{0}</color> died by suicide!</color></size>",
                ["DeathByBurningVictim"] = "<size=12><color=#b6bab7>You were burned to death by <color=#61ff7e>{0}</color> ({1} HP)!</color></size>",
                ["DeathByBurningKiller"] = "<size=12><color=#b6bab7>You burned <color=#61ff7e>{0}</color> to death!</color></size>",
                ["DeathByBurningGlobal"] = "<size=12><color=#b6bab7><color=#61ff7e>{0}</color> ({2} HP) burned <color=#61ff7e>{1}</color> to death!</color></size>",
                ["DeathByMeleeVictim"] = "<size=12><color=#b6bab7>You were killed by <color=#61ff7e>{0}</color> ({2} HP) with a <color=#61ff7e>{1}</color>!</color></size>",
                ["DeathByMeleeKiller"] = "<size=12><color=#b6bab7>You killed <color=#61ff7e>{0}</color> with a <color=#61ff7e>{1}</color>!</color></size>",
                ["DeathByMeleeGlobal"] = "<size=12><color=#b6bab7><color=#61ff7e>{0}</color> ({3} HP) killed <color=#61ff7e>{1}</color> with a <color=#61ff7e>{2}</color>!</color></size>",
                ["DeathByExplosionVictim"] = "<size=12><color=#b6bab7>You were exploded by <color=#61ff7e>{0}</color> ({2} HP) with a <color=#61ff7e>{1}</color>!</color></size>",
                ["DeathByExplosionKiller"] = "<size=12><color=#b6bab7>You exploded <color=#61ff7e>{0}</color> with a <color=#61ff7e>{1}</color>!</color></size>",
                ["DeathByExplosionGlobal"] = "<size=12><color=#b6bab7><color=#61ff7e>{0}</color> ({3} HP) exploded <color=#61ff7e>{1}</color> with a <color=#61ff7e>{2}</color>!</color></size>",
                ["DeathByProjectileVictim"] = "<size=12><color=#b6bab7>You were killed by <color=#61ff7e>{0}</color> ({4} HP) with a <color=#61ff7e>{1}</color> from <color=#61ff7e>{2} meters</color> with a shot to your <color=#61ff7e>{3}</color>!</color></size>",
                ["DeathByProjectileKiller"] = "<size=12><color=#b6bab7>You killed <color=#61ff7e>{0}</color> with a <color=#61ff7e>{1}</color> from <color=#61ff7e>{2}</color> meters with a shot to their <color=#61ff7e>{3}</color>!</color></size>",
                ["DeathByProjectileGlobal"] = "<size=12><color=#b6bab7><color=#61ff7e>{0}</color> ({5} HP) killed <color=#61ff7e>{1}</color> with a <color=#61ff7e>{2}</color> from <color=#61ff7e>{3}</color> meters with a shot to their <color=#61ff7e>{4}</color>!</color></size>",
                ["RewardedForKill"] = "<size=12><color=#b6bab7>You have received a total of <color=#61ff7e>{0} RP</color> for killing <color=#61ff7e>{1}</color>!</color></size>"
            }, this);
        }

        private string Lang(string key, params object[] args) => _config.Prefix + String.Format(lang.GetMessage(key, this, _config.ChatIcon.ToString()), args);

        object CanDropActiveItem(BasePlayer player)
        {
            if (player == null || !_config.PreventDropOnDeath)
                return null;
            return false;
        }

        private static bool IsExplosion(HitInfo hit) => (hit.WeaponPrefab != null && (hit.WeaponPrefab.ShortPrefabName.Contains("grenade") || hit.WeaponPrefab.ShortPrefabName.Contains("explosive")))
                                                        || hit.damageTypes.GetMajorityDamageType() == DamageType.Explosion || (!hit.damageTypes.IsBleedCausing() && hit.damageTypes.Has(DamageType.Explosion));

        private void OnPlayerDeath(BasePlayer entity, HitInfo info)
        {
            if (entity == null) return;

            if (_config.PreventNPC && entity.IsNpc ||
                _config.PreventNPC && !entity.userID.IsSteamId() ||
                _config.PreventNPC && entity.UserIDString.Length != 17) return;

            if (info == null)
            {
                if (!entity.lastAttacker) return;

                BasePlayer wAttacker = entity.lastAttacker.ToPlayer();

                if (wAttacker == null ||
                _config.PreventNPC && wAttacker.IsNpc ||
                _config.PreventNPC && !wAttacker.userID.IsSteamId() ||
                _config.PreventNPC && wAttacker.UserIDString.Length != 17) return;

                if (wAttacker != null && entity.IsWounded())
                {
                    DeathFromWounds(entity, wAttacker);
                }
                return;
            }

            BasePlayer attacker = info.InitiatorPlayer;

            if (attacker == null) return;

            if (_config.PreventNPC && attacker.IsNpc ||
                _config.PreventNPC && !attacker.userID.IsSteamId() ||
                _config.PreventNPC && attacker.UserIDString.Length != 17) return;

            if (attacker == entity)
            {
                DeathFromSuicide(entity);
                return;
            }

            if (IsExplosion(info))
            {
                DeathFromExplosion(entity, attacker, info);
                return;
            }

            if (info.damageTypes.GetMajorityDamageType() == DamageType.Heat || (!info.damageTypes.IsBleedCausing() && info.damageTypes.Has(DamageType.Heat)))
            {
                DeathFromBurning(entity, attacker);
                return;
            }

            if (!info.IsProjectile())
            {
                DeathFromMelee(entity, attacker, info);
                return;
            }

            string distance = GetDistance(entity, info);
            if (distance == null) return;

            DeathFromProjectile(entity, attacker, info, distance);

            if (_config.EconomicsRewardsEnabled && attacker != entity)
            {
                if (!Economics)
                {
                    LogError("You are attempting to reward players with Economics points but you do not seem to have economics installed.");
                    return;
                }

                Economics.Call("Deposit", attacker.userID, _config.EconomicsPointsReward);
                attacker.ChatMessage(Lang("RewardedForKill", _config.EconomicsPointsReward, entity.displayName));
            }
        }

        string GetDistance(BaseCombatEntity entity, HitInfo info)
        {
            float distance = 0.0f;

            if (entity != null && info.Initiator != null)
            {
                distance = Vector3.Distance(info.Initiator.transform.position, entity.transform.position);
            }
            return distance.ToString("0.0").Equals("0.0") ? "" : distance.ToString("0.0") + "m";
        }

        private void DeathFromWounds(BasePlayer victim, BasePlayer attacker)
        {
            if (_config.ShowMessagesGlobally)
            {
                SendGlobalMessageExcluding(
                    new BasePlayer[] { victim, attacker },
                    "DeathByWoundsGlobal",
                    new[] { attacker.displayName, victim.displayName, attacker.Health().ToString("#0.#") }
                );
            }

            victim.ChatMessage(Lang("DeathByWoundsVictim", attacker.displayName, attacker.Health().ToString("#0.#")));
            attacker.ChatMessage(Lang("DeathByWoundsKiller", victim.displayName));
        }

        public string GetEntityAlias(string key)
        {
            string alias = "Unknown";

            if (!_config.EntityNames.TryGetValue(key, out alias))
            {
                alias = key;
            }

            return alias;
        }

        private void DeathFromExplosion(BasePlayer victim, BasePlayer attacker, HitInfo info)
        {
            string EntityName = GetEntityAlias(info.WeaponPrefab.name);

            if (_config.ShowMessagesGlobally)
            {
                SendGlobalMessageExcluding(
                    new BasePlayer[] { victim, attacker },
                    "DeathByExplosionGlobal",
                    new[] { attacker.displayName, victim.displayName, EntityName, attacker.Health().ToString("#0.#") }
                );
            }

            victim.ChatMessage(Lang("DeathByExplosionVictim", attacker.displayName, EntityName, attacker.Health().ToString("#0.#")));
            attacker.ChatMessage(Lang("DeathByExplosionKiller", victim.displayName, EntityName));
        }

        private void DeathFromSuicide(BasePlayer victim)
        {
            if (_config.ShowMessagesGlobally)
            {
                SendGlobalMessageExcluding(
                    new BasePlayer[] { victim },
                    "DeathBySuicideGlobal",
                    new[] { victim.displayName }
                );
            }

            victim.ChatMessage(Lang("DeathBySuicideVictim"));
        }

        private void DeathFromBurning(BasePlayer victim, BasePlayer attacker)
        {
            if (_config.ShowMessagesGlobally)
            {
                SendGlobalMessageExcluding(
                    new BasePlayer[] { victim, attacker },
                    "DeathByBurningGlobal",
                    new[] { attacker.displayName, victim.displayName, attacker.Health().ToString("#0.#") }
                );
            }

            victim.ChatMessage(Lang("DeathByBurningVictim", attacker.displayName, attacker.Health().ToString("#0.#")));
            attacker.ChatMessage(Lang("DeathByBurningKiller", victim.displayName));
        }

        private void DeathFromMelee(BasePlayer victim, BasePlayer attacker, HitInfo info)
        {
            AttackEntity wpn = info.Weapon;

            if (wpn == null ||
                wpn.GetItem() == null ||
                wpn.GetItem().info == null ||
                wpn.GetItem().info.displayName == null ||
                wpn.GetItem().info.displayName.english == null) return;

            if (_config.ShowMessagesGlobally)
            {
                SendGlobalMessageExcluding(
                    new BasePlayer[] { victim, attacker },
                    "DeathByMeleeGlobal",
                    new[] { attacker.displayName, victim.displayName, wpn.GetItem().info.displayName.english, attacker.Health().ToString("#0.#") }
                );
            }

            victim.ChatMessage(Lang("DeathByMeleeVictim", attacker.displayName, wpn.GetItem().info.displayName.english, attacker.Health().ToString("#0.#")));
            attacker.ChatMessage(Lang("DeathByMeleeKiller", victim.displayName, wpn.GetItem().info.displayName.english));
        }

        private void DeathFromProjectile(BasePlayer victim, BasePlayer attacker, HitInfo info, string dist)
        {
            AttackEntity wpn = info.Weapon;
            string lastShotLoc = info.boneName;


            if (wpn == null ||
                wpn.GetItem() == null ||
                wpn.GetItem().info == null ||
                wpn.GetItem().info.displayName == null ||
                wpn.GetItem().info.displayName.english == null) return;

            if (_config.ShowMessagesGlobally)
            {
                SendGlobalMessageExcluding(
                    new BasePlayer[] { victim, attacker },
                    "DeathByProjectileGlobal",
                    new[] { attacker.displayName, victim.displayName, wpn.GetItem().info.displayName.english, dist, lastShotLoc, attacker.Health().ToString("#0.#") }
                );
            }

            victim.ChatMessage(Lang("DeathByProjectileVictim", attacker.displayName, wpn.GetItem().info.displayName.english, dist, lastShotLoc, attacker.Health().ToString("#0.#")));
            attacker.ChatMessage(Lang("DeathByProjectileKiller", victim.displayName, wpn.GetItem().info.displayName.english, dist, lastShotLoc));
        }

        private void SendGlobalMessageExcluding(BasePlayer[] excluded, string LangKey, string[] args)
        {
            if (!excluded[0]) return;

            if (_config.GlobalRadius == 0)
            {
                foreach (IPlayer _iP in players.Connected)
                {
                    BasePlayer _p = (BasePlayer)_iP.Object;
                    if (!_p) continue;

                    if (excluded.Contains<BasePlayer>(_p)) continue;

                    _p.ChatMessage(Lang(LangKey, args));
                }
            }
            else
            {
                if (_config.UseRadius)
                {
                    Collider[] colliders = Physics.OverlapSphere(excluded[0].transform.position, _config.GlobalRadius, LayerMask.GetMask("Player (Server)"));

                    foreach (Collider c in colliders)
                    {
                        BasePlayer _temp = c.gameObject.GetComponent<BasePlayer>();

                        if (!_temp) continue;
                        if (_temp.IsNpc ||
                            !_temp.userID.IsSteamId() ||
                            _temp.UserIDString.Length != 17) continue;
                        if (excluded.Contains<BasePlayer>(_temp)) continue;

                        _temp.ChatMessage(Lang(LangKey, args));
                    }
                }
                else
                {
                    foreach (IPlayer _iP in players.Connected)
                    {
                        BasePlayer _p = (BasePlayer)_iP.Object;
                        if (!_p) continue;
                        if (_config.GlobalRadius > 0)
                        {
                            var distance = Vector3.Distance(excluded[0].transform.position, _p.transform.position);

                            if (distance > _config.GlobalRadius) continue;
                        }

                        if (excluded.Contains<BasePlayer>(_p)) continue;

                        _p.ChatMessage(Lang(LangKey, args));
                    }
                }
            }

        }
    }
}

