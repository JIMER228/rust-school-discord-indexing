using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("DamageScaling", "Scrooge", "1.0.0")]
    class DamageScaling : RustPlugin
    {
        #region Classes
        #endregion
        
        #region Fields
        private Dictionary<ulong, string> _lastUsed = new Dictionary<ulong, string>();
        #endregion
        
        #region Hooks
        void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null)
                return;
            var itemHeldEntity = player.GetActiveItem()?.GetHeldEntity();
            if (itemHeldEntity == null)
                return;
            BaseProjectile projectile = null;
            if (!itemHeldEntity.TryGetComponent<BaseProjectile>(out projectile))
                return;
            if (projectile.primaryMagazine.ammoType == null)
                return;

            if (!_lastUsed.ContainsKey(player.userID))
                _lastUsed.Add(player.userID, string.Empty);
            _lastUsed[player.userID] = projectile.primaryMagazine.ammoType.shortname;
        }
        void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
            if (projectile == null || player == null)
                return;
            if (!player.userID.IsSteamId())
                return;
            if (projectile.primaryMagazine == null)
                return;
            if (projectile.primaryMagazine.ammoType == null)
                return;
            
            if (!_lastUsed.ContainsKey(player.userID))
                _lastUsed.Add(player.userID, string.Empty);
            _lastUsed[player.userID] = projectile.primaryMagazine.ammoType.shortname;
        }
        void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon item)
        {
            if (player == null || entity == null || item == null)
                return;
            if (!player.userID.IsSteamId())
                return;
            if (item.GetItem() == null)
                return;
            if (!_lastUsed.ContainsKey(player.userID))
                _lastUsed.Add(player.userID, string.Empty);
            _lastUsed[player.userID] = item.GetItem().info.shortname;
        }
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null)
                return null;

            if (info.InitiatorPlayer == null)
                return null;
            if (entity.ToPlayer() == null)
                return null;
            if (!entity.ToPlayer().userID.IsSteamId())
                return null;

            if (!info.InitiatorPlayer.userID.IsSteamId())
                return null;
            BasePlayer player = info.InitiatorPlayer;

            if (!_lastUsed.ContainsKey(player.userID))
                return null;

            string shortname = _lastUsed[player.userID];

            if (!cfg._ammoAndExplosivesScaleDamage.ContainsKey(shortname))
                return null;
            
            float scaling = cfg._ammoAndExplosivesScaleDamage[shortname];
            info.damageTypes.ScaleAll(scaling);
            
            return null;
        }
        #endregion
        
        #region Config
        private ConfigData cfg;
        public class ConfigData
        {
            [JsonProperty("Патроны, у которых уменьшается урон (1 - полный урон, 0 - полное отсутствие)", Order = 0)]
            public Dictionary<string, float> _ammoAndExplosivesScaleDamage;
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData()
            {
                _ammoAndExplosivesScaleDamage = new Dictionary<string, float>()
                {
                    {"grenade.f1", 0.5f},
                    {"ammo.rocket.basic", 0f},
                    {"ammo.rifle.explosive", 0.3f},
                    {"ammo.rocket.fire", 0f},
                    {"ammo.rocket.hv", 0f}
                },
            };
            SaveConfig(config);
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            cfg = Config.ReadObject<ConfigData>();
            SaveConfig(cfg);
        }
        void SaveConfig(object config) => Config.WriteObject(config, true);
        #endregion
    }
}