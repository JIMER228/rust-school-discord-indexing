using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("BetterSAMSiteAuth", "Shady14u // Farkas // Haggbart", "1.0.1")]
    public class BetterSAMSiteAuth : RustPlugin
    {
        private object OnSamSiteTarget(SamSite samSite, BaseCombatEntity target)
        {
            List<BaseVehicle.MountPointInfo> mountPoints = (target as BaseVehicle)?.mountPoints;
            if (!IsOccupied(target, mountPoints))
                return false;

            if (samSite.staticRespawn)
                return null;

            if (mountPoints != null)
            {
                foreach (var mountPoint in mountPoints)
                {
                    BasePlayer player = mountPoint.mountable.GetMounted();
                    if (player != null && IsFriendly(player, samSite.OwnerID))
                    {
                        return false;
                    }
                }
            }

            foreach (var child in target.children)
            {
                if (child is BasePlayer player)
                {
                    if (IsFriendly(player, samSite.OwnerID))
                        return false;
                }
            }

            return null;
        }

        private static bool IsOccupied(BaseCombatEntity entity, List<BaseVehicle.MountPointInfo> mountPoints)
        {
            if (mountPoints != null)
            {
                foreach (var mountPoint in mountPoints)
                {
                    BasePlayer player = mountPoint.mountable.GetMounted();
                    if (player != null) 
                        return true;
                }
            }

            foreach (var child in entity.children)
            {
                if (child is BasePlayer)
                    return true;
            }

            return false;
        }

        private static bool IsFriendly(BasePlayer player, ulong samSiteOwnerID)
        {
            if (player.Team == null)
            {
                if (player.userID == samSiteOwnerID)
                {
                    return true;
                }
            }
            else if (player.Team.members.Contains(samSiteOwnerID))
            {
                return true;
            }

            return false;
        }
    }
}