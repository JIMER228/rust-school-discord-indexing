using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Stash Fix", "Hougan", "0.0.1")]
    public class StashFix : RustPlugin
    {
        private bool? CanAffordUpgrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
        {
            if (!block.PrefabName.Contains("foundation")) return null;
            
            List<StashContainer> cont = new List<StashContainer>();
            Vis.Entities(block.transform.position, 3f, cont);

            if (cont.Count > 0)
            {
                player.ChatMessage("Вы не можете улучшать постройки возле тайников");
                return false;
            }
            
            return null;
        } 
    }
}