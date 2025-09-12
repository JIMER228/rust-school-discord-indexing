using UnityEngine;

namespace Oxide.Plugins
{
    [Info("WorkbenchPickup", "Ryamkk", "1.0.0")]
    public class WorkbenchPickup : RustPlugin
    {
        [ChatCommand("CMD.Workbench.Pickup")]
        void CMD_WorkbenchPickup(BasePlayer player)
        { 
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 2))
            {
                BaseCombatEntity entity = null;
                try { entity = hit.GetEntity() as BaseCombatEntity; }
                catch { return; }

                if (entity.ShortPrefabName.Contains("workbench1.deployed") || entity.ShortPrefabName.Contains("workbench2.deployed") || entity.ShortPrefabName.Contains("workbench3.deployed"))
                {
                    ItemDefinition itemDef = entity.pickup.itemTarget;
                    player.GiveItem(ItemManager.Create(itemDef, 1, entity.skinID));
                    entity.Kill();
                }
            }
        }
    }
}