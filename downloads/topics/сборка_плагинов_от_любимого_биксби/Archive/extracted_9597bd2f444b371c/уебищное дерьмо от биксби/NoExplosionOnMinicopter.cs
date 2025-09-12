using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoExplosionOnMinicopter", "b1xbyy", "1.0.0")]
    class NoExplosionOnMinicopter : RustPlugin
    {
        object CanExplosiveStick(TimedExplosive explosive, BaseEntity entity)
        {
            if(entity is Minicopter || entity is ScrapTransportHelicopter)
            {
                return false;
            }
            return null;
        } 

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null) return;

            if (entity.ShortPrefabName == "explosive.timed.deployed")
            { 
                Vector3 position = entity.transform.position;
                RaycastHit hit;
                if (Physics.Raycast(position, Vector3.down, out hit, 1.0f))
                {
                    BaseEntity hitEntity = hit.GetEntity();
                    if (hitEntity != null && hitEntity is Minicopter)
                    {
                        entity.Kill();
                        player.GiveItem(ItemManager.CreateByName("explosive.timed", 1));
                    }
                }
            }
        } 
    }
}