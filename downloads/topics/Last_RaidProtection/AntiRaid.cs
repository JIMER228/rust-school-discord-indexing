using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using ProtoBuf;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("AntiRaid", "walkinrey", "1.0.5")]
    class AntiRaid : RustPlugin
    {
        #region Variables
        Configuration config;
        BuildingPrivlidge privilege;
        IPlayer playerOwner;
        BasePlayer initiator;
        Dictionary<ulong, float> lastNotifyTime = new Dictionary<ulong, float>();
        #endregion
        #region Config
        class Configuration
        {
            [JsonProperty("Сообщение если у игрока есть антирейд")] public string msgAntiRaid = "У этого игрока активен антирейд, его невозможно зарейдить.";
            [JsonProperty("Shortname дополнительных объектов, чтобы на них действовал антирейд")] public string[] shortnames = {"wall.frame.cell.gate", "wall.frame.cell", "wall.window.glass.reinforced", "wall.window.bars.toptier", "floor.grill", "floor.triangle.grill", "gates.external.high.stone", "wall.external.high.stone", "gates.external.high.wood", "wall.external.high"};
        }
        protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				config = Config.ReadObject<Configuration>();
				if(config == null) LoadDefaultConfig();
			}
			catch
			{
				LoadDefaultConfig();
			}
			Config.WriteObject(config, true);
        }
        protected override void LoadDefaultConfig() => config = new Configuration();
        #endregion
        #region Hooks
        private void Init() => permission.RegisterPermission("antiraid.use", this);
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if(entity == null || info == null) {SetNull(); return null;}
            if(entity.OwnerID.IsSteamId()) playerOwner = covalence.Players.FindPlayerById(entity.OwnerID.ToString());
            else {SetNull(); return null;}
            if(info != null) initiator = info?.InitiatorPlayer;
            if(initiator == null) {SetNull(); return null;}

            var initiatorEntity = info.Initiator;
            var initiatorPlayer = info.InitiatorPlayer;

            if (initiatorEntity == null && initiatorPlayer == null)
            {
                SetNull();
                return null;
            }

            bool isSuspiciousInitiator = false;
            if (initiatorEntity != null && !string.IsNullOrEmpty(initiatorEntity.ShortPrefabName))
            {
                string prefab = initiatorEntity.ShortPrefabName.ToLower();
                isSuspiciousInitiator = prefab.Contains("drone") || prefab.Contains("timed.explosive") || prefab.Contains("rocket") || prefab.Contains("fireball") || prefab.Contains("explosive") || prefab.Contains("bradleyapc") || prefab.Contains("patrolhelicopter");
            }

            if(entity is BuildingBlock || entity is Door || entity is Construction || entity is Item || config.shortnames.Contains(entity.ShortPrefabName.ToString()))
            {
                if(playerOwner == null) {SetNull(); return null;}
                bool isOwner = initiatorPlayer != null && playerOwner.Id.ToString() == initiatorPlayer.userID.ToString();
                bool isExplosionOrFire = info.damageTypes.Has(Rust.DamageType.Explosion) || info.damageTypes.Has(Rust.DamageType.Heat) || info.damageTypes.Has(Rust.DamageType.Generic);

                // Блокируем урон по защищённым объектам, если:
                // 1. Владелец имеет антирейд
                // 2. (инициатор не владелец) ИЛИ (тип урона взрыв/огонь/универсальный) ИЛИ (инициатор подозрительный)
                if (playerOwner.HasPermission("antiraid.use") && (!isOwner || isExplosionOrFire || isSuspiciousInitiator))
                {
                    if (initiatorPlayer != null)
                    {
                        float now = UnityEngine.Time.realtimeSinceStartup;
                        ulong id = initiatorPlayer.userID;
                        if (!lastNotifyTime.ContainsKey(id) || now - lastNotifyTime[id] > 60f)
                        {
                            SendReply(initiatorPlayer, config.msgAntiRaid);
                            lastNotifyTime[id] = now;
                        }
                    }
                    SetNull();
                    return false;
                }
            }
            SetNull(); 
            return null;
        }

        // Защита от уничтожения через OnEntityDeath
        object OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            if (!entity.OwnerID.IsSteamId()) return null;
            var playerOwner = covalence.Players.FindPlayerById(entity.OwnerID.ToString());
            if (playerOwner == null) return null;
            if (entity is BuildingBlock || entity is Door || entity is Construction || entity is Item || config.shortnames.Contains(entity.ShortPrefabName.ToString()))
            {
                if (playerOwner.HasPermission("antiraid.use"))
                {
                    Puts($"[AntiRaid] Blocked OnEntityDeath for {entity.ShortPrefabName} (owner: {playerOwner.Name})");
                    return false;
                }
            }
            return null;
        }

        // Защита от уничтожения через OnEntityKill
        object OnEntityKill(BaseNetworkable entity)
        {
            var baseEntity = entity as BaseEntity;
            if (baseEntity == null) return null;
            if (!baseEntity.OwnerID.IsSteamId()) return null;
            var playerOwner = covalence.Players.FindPlayerById(baseEntity.OwnerID.ToString());
            if (playerOwner == null) return null;
            if (baseEntity is BuildingBlock || baseEntity is Door || baseEntity is Construction || baseEntity is Item || config.shortnames.Contains(baseEntity.ShortPrefabName.ToString()))
            {
                if (playerOwner.HasPermission("antiraid.use"))
                {
                    Puts($"[AntiRaid] Blocked OnEntityKill for {baseEntity.ShortPrefabName} (owner: {playerOwner.Name})");
                    return false;
                }
            }
            return null;
        }
        #endregion
        #region Helpers
        void SetNull() {playerOwner = null; privilege = null; initiator = null;}
        #endregion
    }
}