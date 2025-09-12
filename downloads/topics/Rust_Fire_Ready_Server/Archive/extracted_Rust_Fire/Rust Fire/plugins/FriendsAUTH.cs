using System.Linq;
using ProtoBuf;

namespace Oxide.Plugins
{
    [Info("FriendsAUTH", "fermens", "0.0.1")]
    [Description("Чего?")]
    class FriendsAUTH : RustPlugin
    {
        private object OnTurretTarget(AutoTurret turret, BaseCombatEntity entity)
        {
            if (entity == null) return null;
            BasePlayer player = entity.ToPlayer();
            if (player == null) return null;
            ulong owner = turret.OwnerID;
            if (owner.Equals(0) || turret.authorizedPlayers.Where(z => z.userid.Equals(owner)).FirstOrDefault() == null) return null;
            if (player.Team != null && player.Team.members.Contains(owner))
            {
                turret.authorizedPlayers.Add(GetPlayerNameId(player));
                turret.SendNetworkUpdate();
                return false;
            }
            return null;
        }

        private static PlayerNameID GetPlayerNameId(BasePlayer player)
        {
            var playerNameId = new PlayerNameID()
            {
                userid = player.userID,
                username = player.displayName
            };
            return playerNameId;
        }

        private object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            if (player == null || baseLock == null || baseLock.GetEntity() == null || !baseLock.IsLocked()) return null;
            ulong ownerID = baseLock.GetEntity().OwnerID;
            if (ownerID.Equals(0)) return null;
            if (player.Team != null && player.Team.members.Contains(ownerID))
            {
                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab", baseLock.transform.position);
                return true;
            }
            return null;
        }
    }
}